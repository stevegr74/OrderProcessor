using System;
using System.Threading;
using System.ComponentModel;

namespace SACProcessor
{
    class Worker
    {
        private static int workers = 0;
        private static bool stopWork = false;

        public static int Workers
        {
            get
            {
                return workers;
            }
        }

        private static string claimID = Thread.CurrentThread.ManagedThreadId.ToString();
        private static string workloadSize = Helper.ReadSetting("WorkloadSize", "100");
        private static string noLock = Helper.ReadSetting("NOLOCK", "N") == "Y" ? "WITH (NOLOCK) " : "";
        private object thisLock = new object();

        private bool getWorkLoad(ref DB db, ref baseDB.DataReader dr)
        {
            bool ret = false;
            Logger.Log("Getting workload (" + workloadSize + " records requested)");

            try
            {
                try
                {
                    lock (thisLock) // critical section, only let one thread claim database records at a time.
                    {
                        baseDB.DB3.SQLRequest claimSql = new baseDB.DB3.SQLRequest();
                        claimSql.unmanagedSQL = "update TOP(" + workloadSize + ") t SET SACClaimed='" + claimID + "' FROM tblTransactions AS t inner join tblSupplierSettings as s on t.SupplierID = s.SupplierID where t.SACClaimed is NULL and isNull(t.SACApplied,0) = 0 and s.SACSupplier = 1 and SUBSTRING(t.AliasName,1,3) in (select Code from tblSACLocationCodes)";
                        db.ExecuteDataWriter(claimSql);
                    }

                }
                catch // carry on with the operation anyway.
                {
                    Logger.Log("Stamp workload failed, checking for outstanding");
                }

                baseDB.DB3.SQLRequest sql = new baseDB.DB3.SQLRequest();
                sql.unmanagedSQL = "select t.*, r.Rate from tblTransactions t " + noLock + "inner join tblSACRates r on SUBSTRING(t.AliasName,5,2) = r.Code where t.SACClaimed='" + claimID + "'";
                dr = db.OpenDataReader(sql);
                if (dr.hasRows)
                {
                    Logger.Log("Workload assigned (" + dr.rowCount + " records)");
                    ret = true;
                }
                else
                {
                    Logger.Log("No workload available");
                }
            }
            catch
            {
                Logger.Log("Work denied");
                Email.Send("");
            }

            return ret;
        }

        public void StartWork()
        {
            Interlocked.Increment(ref workers);
            int recordsProcessed = 0;
            int recordsFailed = 0;

            Logger.Log("Work started");

            // do proceesing in here until nothing left to process, or threads are stopped....
            DB db = new DB();
            if (db.open())
            {
                baseDB.DataReader dr = new baseDB.DataReader();
                while (!stopWork && getWorkLoad(ref db, ref dr))
                {
                    Logger.Log("Processing workload");
                    while (!stopWork && dr.Read()) // TODO: actually check the response from dr.Read and abort if fails?
                    {
                        if (ProcessSAC(ref db, ref dr)) // TODO: stop if this fails? or carry on
                        {
                            recordsProcessed++;
                        }
                        else
                        {
                            recordsFailed++;
                        }
                        Thread.Sleep(50); // need to give processor some time!
                    }
                    Logger.Log("Workload complete");
                    Thread.Sleep(50); // need to give processor some time!
                }

                if(!stopWork)
                {
                    //log workload completed
                    Logger.Log("Work completed");
                }
                else
                {
                    //log workload stopped
                    Logger.Log("Work aborted");
                }
                Logger.Log("S:" + recordsProcessed.ToString() + " F:" + recordsFailed.ToString());

                if (recordsFailed > 0)
                {
                    Email.Send("Worker " + Thread.CurrentThread.ManagedThreadId.ToString() + " experienced " + recordsFailed.ToString() + " processing errors, review the logs for more detail");
                }
            }
            else
            {
                Logger.Log("Work aborted: DB open failed");
                Email.Send("ERROR: Unable to connect to the database");
            }

            Interlocked.Decrement(ref workers);
        }


        private static string commodityCode = Helper.ReadSetting("SACCommodityCode", "6760");
        private static string commodityDescription = Helper.ReadSetting("SACCommodityDescription", "FINANCIAL ADMINISTRATION SERVICES");
        private static string unitOfMeasure = Helper.ReadSetting("SACUnitOfMeasure","EA");
        private static double vatRate = Helper.ToDouble(Helper.ReadSetting("SACVATRate", "20"));

        public bool ProcessSAC(ref DB db, ref baseDB.DataReader dr)
        {
            bool ret = false;

            try
            {
                string txnType = dr.field("TransactionType").strValue;

                if (txnType.Contains("SAL") || txnType.Contains("REF"))
                {
                    baseDB.DB3.SQLRequestList requestList = new baseDB.DB3.SQLRequestList();

                    int sacRate = dr.field("Rate").intValue;
                    if (sacRate != 0)
                    {
                        double SACTotalAmount = 0.0;
                        int txnAmount = 0;
                        bool SACRefund = false;

                        if (txnType.Contains("SAL"))
                        {
                            txnType = txnType.Replace("SAL", "REF");
                            SACRefund = true;
                        }
                        else
                        {
                            txnType = txnType.Replace("REF", "SAL");
                        }

                        double SACRate = sacRate / 100.0;

                        string reference = "SAC" + dr.field("Reference").strValue;
                        DateTime now = DateTime.Now;

                        string addendum = dr.field("Addendum").strValue;
                        if (string.IsNullOrEmpty(addendum))
                        {
                            double amount = dr.field("Amount").intValue / 100.0;
                            SACTotalAmount = (amount * (SACRate / 100.0)) + 0.00005;
                            txnAmount = (int)(SACTotalAmount * 100.0);
                        }
                        else
                        {
                            //create addendum
                            xmlElement invoice = new xmlElement(addendum);
                            xmlElement SACAddendum = new xmlElement("<Invoice/>");

                            //modify InvoiceHeader
                            xmlElement invHead = invoice.getElement("InvoiceHeader");
                            xmlElement SACInvoiceHeader = SACAddendum.newElement("InvoiceHeader");
                            SACInvoiceHeader.import(invHead.getElement("InvoiceType"));
                            SACInvoiceHeader.import(invHead.getElement("InvoiceStatus"));
                            SACInvoiceHeader.newElement("TaxTreatment").setAttribute("stdValue", "NIL");
                            SACInvoiceHeader.import(invHead.getElement("InvoiceTreatment"));
                            SACInvoiceHeader.newElement("InvoiceNumber").Text = "SAC" + invHead.getElement("InvoiceNumber").Text;
                            SACInvoiceHeader.newElement("InvoiceDate").Text = string.Format("{0:s}", now);
                            SACInvoiceHeader.newElement("TaxPointDate").Text = string.Format("{0:s}", now);
                            SACInvoiceHeader.import(invHead.getElement("Currency"));
                            System.Collections.ArrayList parties = invHead.getElements("Party");
                            foreach (xmlElement party in parties)
                            {
                                SACInvoiceHeader.import(party);
                            }

                            SACInvoiceHeader.import(invHead.getElement("PONum"));

                            //InvoiceHeader refs
                            if (SACRefund)
                            {
                                //create "IV" ref containing original invoice number
                                xmlElement SACInvoiceHeaderRef = SACInvoiceHeader.newElement("Ref");
                                SACInvoiceHeaderRef.setAttribute("stdValue", "IV");
                                SACInvoiceHeaderRef.Text = invHead.getElement("InvoiceNumber").Text;
                            }
                            System.Collections.ArrayList invRefs = invHead.getElements("Ref");
                            foreach (xmlElement invRef in invRefs)
                            {
                                if (invRef.getAttribute("stdValue") != "IV")
                                {
                                    SACInvoiceHeader.import(invRef);
                                }
                            }

                            SACInvoiceHeader.import(invHead.getElement("Date"));

                            //create new InvoiceDetails for each line
                            System.Collections.ArrayList invoiceDetails = invoice.getElements("InvoiceDetails");
                            foreach (xmlElement invDet in invoiceDetails)
                            {
                                xmlElement SACInvoiceDetails = SACAddendum.newElement("InvoiceDetails");
                                xmlElement SACBaseItemDetail = SACInvoiceDetails.newElement("BaseItemDetail");
                                xmlElement baseItemDetail = invDet.getElement("BaseItemDetail");

                                SACBaseItemDetail.import(baseItemDetail.getElement("LineItemNum"));

                                //create Commodity code PartNumDetail
                                xmlElement SACPartNumDetailForCC = SACBaseItemDetail.newElement("PartNumDetail");
                                SACPartNumDetailForCC.setAttribute("stdValue", "CC");
                                SACPartNumDetailForCC.newElement("PartNum").Text = commodityCode;
                                SACPartNumDetailForCC.newElement("PartDesc").Text = commodityDescription;

                                System.Collections.ArrayList partNumDetails = baseItemDetail.getElements("PartNumDetail");
                                foreach (xmlElement partNumDet in partNumDetails)
                                {
                                    if (partNumDet.getAttribute("stdValue") == "VP")    //vendor(supplier)'s product code & description
                                    {
                                        //modify supplier's product description (prefix with "SAC")
                                        xmlElement SACPartNumDetailForVP = SACBaseItemDetail.newElement("PartNumDetail");
                                        SACPartNumDetailForVP.setAttribute("stdValue", "VP");
                                        SACPartNumDetailForVP.newElement("PartNum").Text = partNumDet.getElement("PartNum").Text;
                                        SACPartNumDetailForVP.newElement("PartDesc").Text = "SAC" + partNumDet.getElement("PartDesc").Text;
                                    }
                                }

                                //Quantity
                                xmlElement SACQuantity = SACBaseItemDetail.newElement("Quantity");
                                xmlElement SACQty = SACQuantity.newElement("Qty");
                                SACQty.Text = "1.0000";
                                xmlElement SACUnitOfMeasure = SACQuantity.newElement("UnitOfMeasure");
                                SACUnitOfMeasure.setAttribute("stdValue", unitOfMeasure);

                                //calculate the SAC amount
                                double lineTotal = double.Parse(invDet.getElement("LineItemSubtotal").Text);
                                double SACAmount = (lineTotal * (SACRate / 100.0)) + 0.00005;

                                //set unit price & line item sub total = SAC amount
                                SACInvoiceDetails.newElement("UnitPrice").Text = string.Format("{0:0.0000}", SACAmount);
                                SACInvoiceDetails.newElement("LineItemSubtotal").Text = string.Format("{0:0.00}", SACAmount);

                                SACTotalAmount += SACAmount;

                                //Tax
                                createTaxElement(SACInvoiceDetails);
                            }

                            //InvoiceSummary
                            xmlElement SACInvoiceSummary = SACAddendum.newElement("InvoiceSummary");

                            //create one TaxSummary using standard VAT rate
                            xmlElement SACTaxSummary = SACInvoiceSummary.newElement("TaxSummary");
                            xmlElement SACTax = createTaxElement(SACTaxSummary);

                            //create TaxableAmount and TaxAmount
                            double taxRate = double.Parse(SACTax.getElement("TaxPercent").Text);

                            SACTax.newElement("TaxableAmount").Text = string.Format("{0:0.00}", SACTotalAmount);

                            //calculate the TaxAmount
                            double taxAmount = (SACTotalAmount * (taxRate / 100.0)) + 0.00005;
                            SACTax.newElement("TaxAmount").Text = string.Format("{0:0.00}", taxAmount);

                            //InvoiceTotals
                            txnAmount = (int)((SACTotalAmount + 0.005) * 100.0) + (int)((taxAmount + 0.005) * 100.0);
                            double SACGrossAmount = (double)txnAmount / 100.0;
                            if (txnAmount != 0)
                            {
                                xmlElement SACInvoiceTotals = SACInvoiceSummary.newElement("InvoiceTotals");
                                SACInvoiceTotals.newElement("NetValue").Text = string.Format("{0:0.00}", SACTotalAmount);
                                SACInvoiceTotals.newElement("TaxValue").Text = string.Format("{0:0.00}", taxAmount);
                                SACInvoiceTotals.newElement("GrossValue").Text = string.Format("{0:0.00}", SACGrossAmount);

                                //ActualPayment
                                xmlElement SACActualPayment = SACInvoiceSummary.newElement("ActualPayment");
                                xmlElement SACPaymentAmount = SACActualPayment.newElement("PaymentAmount");
                                SACPaymentAmount.newElement("LocalCurrencyAmt").Text = string.Format("{0:0.00}", SACGrossAmount);
                                SACActualPayment.newElement("PaymentMean").setAttribute("stdValue", "ZZZ");
                                SACActualPayment.newElement("PaymentDate").Text = string.Format("{0:s}", now);

                                //CardInfo
                                SACActualPayment.import(invoice.getElement("InvoiceSummary").getElement("ActualPayment").getElement("CardInfo"));

                                addendum = SACAddendum.rootXml;
                            }
                        }

                        if (txnAmount != 0)
                        {
                            //create SAC record
                            baseDB.DB3.SQLRequest sqlSAC = new baseDB.DB3.SQLRequest();
                            sqlSAC.operation = baseDB.DB3.SQLRequest.Operation.Insert;
                            sqlSAC.fields.Add("SupplierID").value = dr.field("SupplierID").strValue;
                            sqlSAC.fields.Add("Reference").value = reference;
                            sqlSAC.fields.Add("Status").value = dr.field("Status").value;
                            sqlSAC.fields.Add("Source").value = "ITSSAC";
                            sqlSAC.fields.Add("Acquirer").value = dr.field("Acquirer").strValue;
                            //don't add SettlementID as this needs to be null
                            sqlSAC.fields.Add("TerminalID").value = dr.field("TerminalID").intValue;
                            sqlSAC.fields.Add("TransactionType").value = txnType;
                            sqlSAC.fields.Add("DateTime").value = now;
                            sqlSAC.fields.Add("SchemeName").value = dr.field("SchemeName").strValue;
                            sqlSAC.fields.Add("PAN").value = dr.field("PAN").strValue;
                            sqlSAC.fields.Add("IssueNumber").value = dr.field("IssueNumber").strValue;
                            sqlSAC.fields.Add("StartDate").value = dr.field("StartDate").strValue;
                            sqlSAC.fields.Add("ExpiryDate").value = dr.field("ExpiryDate").strValue;
                            sqlSAC.fields.Add("CustomerPresent").value = dr.field("CustomerPresent").boolValue;
                            sqlSAC.fields.Add("InputMethod").value = dr.field("InputMethod").strValue;
                            sqlSAC.fields.Add("CurrencyCode").value = dr.field("CurrencyCode").strValue;
                            sqlSAC.fields.Add("CountryCode").value = dr.field("CountryCode").strValue;
                            sqlSAC.fields.Add("Amount").value = txnAmount;
                            sqlSAC.fields.Add("Cashback").value = 0;
                            sqlSAC.fields.Add("AuthMethod").value = "OFFLINE";
                            sqlSAC.fields.Add("AuthCode").value = "";
                            sqlSAC.fields.Add("AddendumType").value = dr.field("AddendumType").strValue;
                            sqlSAC.fields.Add("Addendum").value = addendum;
                            sqlSAC.fields.Add("ICCData").value = "";
                            sqlSAC.fields.Add("UserReference").value = "";
                            sqlSAC.fields.Add("ReceiptNumber").value = dr.field("ReceiptNumber").value;
                            sqlSAC.fields.Add("Processed").value = now;
                            sqlSAC.fields.Add("ESFeed").value = dr.field("ESFeed").strValue;
                            sqlSAC.fields.Add("ContractNumber").value = dr.field("ContractNumber").strValue;
                            sqlSAC.fields.Add("CVV").value = "";
                            sqlSAC.fields.Add("TerminalType").value = dr.field("TerminalType").strValue;
                            sqlSAC.fields.Add("XMLBIN").value = dr.field("XMLBIN").strValue;
                            sqlSAC.fields.Add("AddendumAcquirer").value = dr.field("AddendumAcquirer").strValue;
                            sqlSAC.fields.Add("AddendumSettlementID").value = "";
                            sqlSAC.fields.Add("SOCRefNumber").value = dr.field("SOCRefNumber").value;
                            sqlSAC.fields.Add("UserSource").value = "";
                            sqlSAC.fields.Add("TicketNumber").value = dr.field("TicketNumber").strValue;
                            sqlSAC.fields.Add("EncryptedPAN").value = dr.field("EncryptedPAN").strValue;
                            sqlSAC.fields.Add("AliasName").value = dr.field("AliasName").strValue;
                            sqlSAC.fields.Add("POSEntryMode").value = "";
                            sqlSAC.fields.Add("UserName").value = "";
                            sqlSAC.fields.Add("OriginalReference").value = "";
                            sqlSAC.fields.Add("SchemeReferenceData").value = "";
                            sqlSAC.fields.Add("AcquirerReferenceData").value = "";
                            sqlSAC.fields.Add("NIUtilityType").value = "";
                            sqlSAC.fields.Add("NINarration").value = "";
                            sqlSAC.fields.Add("SACApplied").value = 1;

                            sqlSAC.table = "tblTransactions";
                            sqlSAC.transactAcceptCriteria = baseDB.DB3.TransactAcceptCriteria.RowsAffectedIs1;
                            requestList.AddRequest(sqlSAC);
                        }
                    }

                    //update tblTransactions set SACApplied = 'True' and SACClaimed = null where ID = ID of SAC eligible txn
                    baseDB.DB3.SQLRequest sqlTxn = new baseDB.DB3.SQLRequest();
                    sqlTxn.operation = baseDB.DB3.SQLRequest.Operation.Update;
                    sqlTxn.fields.Add("SACApplied").value = 1;
                    sqlTxn.fields.Add("SACClaimed").value = null;
                    sqlTxn.table = "tblTransactions";
                    sqlTxn.whereClause.basic.Add("ID", baseDB.DB3.Compare.Equal, dr.field("ID").intValue);
                    sqlTxn.transactAcceptCriteria = baseDB.DB3.TransactAcceptCriteria.RowsAffectedIs1;
                    requestList.AddRequest(sqlTxn);

                    requestList.Transact = true;    //execute within a transaction
                    baseDB.DB3.SQLResponseList responseList = new baseDB.DB3.SQLResponseList();
                    responseList = db.ExecuteBatch(requestList);
                    if (responseList.Success == true)
                    {
                        ret = true;
                    }
                    else
                    {
                        //log an error
                        Logger.Log("ProcessSAC: DB ExecuteBatch failed. " + responseList.Exception);
                    }
                }
                else
                {
                    //log unknown txn type
                    Logger.Log("ProcessSAC: Unrecognized transaction type: " + txnType + " in txn ID: " + dr.field("ID").intValue);
                }
            }

            catch (Exception ex)
            {
                if (ex.InnerException != null && ex.InnerException.GetType() == typeof(Win32Exception))
                {
                    Win32Exception wex = (Win32Exception)ex.InnerException;
                    Logger.Log(string.Format("ProcessSAC: Exception, error(0x{0:X})", wex.ErrorCode));
                }
                else
                {
                    Logger.Log("ProcessSAC: " + ex.ToString());
                }
            }

            if (!ret)
            {
                //update tblTransactions set SACClaimed = "ERROR" where ID = ID of SAC eligible txn
                baseDB.DB3.SQLRequest sqlTxn = new baseDB.DB3.SQLRequest();
                sqlTxn.operation = baseDB.DB3.SQLRequest.Operation.Update;
                sqlTxn.fields.Add("SACClaimed").value = "ERROR";
                sqlTxn.table = "tblTransactions";
                sqlTxn.whereClause.basic.Add("ID", baseDB.DB3.Compare.Equal, dr.field("ID").intValue);
                sqlTxn.transactAcceptCriteria = baseDB.DB3.TransactAcceptCriteria.RowsAffectedIs1;
                if (db.ExecuteDataWriter(sqlTxn).RowsAffected != 1)
                {
                    Logger.Log("ProcessSAC: Failed to set SACClaimed = ERROR for txn ID: " + dr.field("ID").intValue);
                }
            }
            return ret;
        }

        /// <summary>
        /// This static method sets a flag to stop all of the worker threads
        /// </summary>
        public static void StopWork()
        {
            Logger.Log("Stop work signalled");
            stopWork = true;
        }

        public class DB : baseDB.SQLServer
        {
            public override void Alert(string message, bool DBOK)
            {
            }

            public DB()
            {
                ConnectionParameters = Helper.ReadConnectionString("DB");
            }

            public DB(string key)
            {
                ConnectionParameters = Helper.ReadConnectionString(key);
            }
        }

        private xmlElement createTaxElement(xmlElement SACElement)
        {
            xmlElement SACTax = SACElement.newElement("Tax");
            xmlElement SACTaxFunction = SACTax.newElement("TaxFunction");
            SACTaxFunction.setAttribute("stdValue", "7");
            xmlElement SACTaxType = SACTax.newElement("TaxType");
            SACTaxType.setAttribute("stdValue", "VAT");
            xmlElement SACTaxCategory = SACTax.newElement("TaxCategory");
            SACTaxCategory.setAttribute("stdValue", "S");
            xmlElement SACTaxPercent = SACTax.newElement("TaxPercent");
            SACTaxPercent.Text = string.Format("{0:0.0000}", vatRate);
            return SACTax;
        }
    }
}
