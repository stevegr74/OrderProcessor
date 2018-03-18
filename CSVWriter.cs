using System;
using System.Configuration;
using System.Data.SqlClient;

namespace OrderProcessor
{
    class CSVWriter
    {
        private static bool abortWork = false;

        public static bool AbortWork
        {
            set
            {
                abortWork = value;
            }
        }

        private bool WriteToCSVFile(string orderReference, string orderMarketplace, string csvLine)
        {
            bool ret = false;
            try
            {
                string csvFilename = Helper.ReadSetting("OutputFilePath").TrimEnd('\\') + "\\ORDER_" + orderReference + "_" + orderMarketplace + ".csv";

                // This will automatically create new files for new orders and append to existing orders as required.
                System.IO.File.AppendAllText(csvFilename, csvLine + Environment.NewLine);
                ret = true;
            }
            catch (Exception ex)
            {
                Logger.Log("Exception caught: " + ex.Source + " : " + ex.Message);
            }

            return ret;
        }

        private bool StampRecords(string orderReference, string orderMarketplace)
        {
            bool ret = false;
            using (SqlConnection sql = new SqlConnection(ConfigurationManager.ConnectionStrings["DB"].ConnectionString))
            {
                try
                {
                    //TODO: should parameterize this
                    SqlCommand sqlCommand = new SqlCommand("update tblOrder set Processed=1 where OrderReference='" + orderReference + "' and OrderMarketplace='" + orderMarketplace + "'", sql);
                    sqlCommand.Connection.Open();
                    sqlCommand.ExecuteNonQuery();
                    ret = true;
                }
                catch (Exception ex)
                {
                    Logger.Log("Exception caught: " + ex.Source + " : " + ex.Message);
                }
            }
            return ret;

        }

        public void createFiles()
        {
            // this query will only return rowws if there is data in all 3 tables for the order.
            string sqlQuery = "SELECT   tblOrder.OrderReference, tblOrder.OrderMarketplace, tblOrder.OrderFirstname, tblOrder.OrderSurname, tblOrderItem.OrderItemNumber, tblOrderItem.SKU, tblOrderItem.PricePerUnit, tblOrderItem.Quantity,";
            sqlQuery += "             tblShipment.ShippingService, tblShipment.Postcode ";
            sqlQuery += "FROM     tblOrder INNER JOIN";
            sqlQuery += "             tblOrderItem ON tblOrder.OrderReference = tblOrderItem.OrderReference INNER JOIN";
            sqlQuery += "             tblShipment ON tblOrder.OrderReference = tblShipment.OrderReference ";
            sqlQuery += "WHERE    tblOrder.Processed = 0 ";
            sqlQuery += "ORDER BY tblOrder.OrderReference,tblOrderItem.OrderItemNumber";

            using (SqlConnection sql = new SqlConnection(ConfigurationManager.ConnectionStrings["DB"].ConnectionString))
            {
                SqlCommand command = new SqlCommand(sqlQuery, sql);
                sql.Open();
                SqlDataReader reader = command.ExecuteReader();

                while (!abortWork && reader.Read())
                {
                    string OrderReference = reader[0].ToString();
                    string OrderMarketplace = reader[1].ToString();
                    string OrderFirstname = reader[2].ToString();
                    string OrderSurname = reader[3].ToString();
                    string OrderItemNumber = reader[4].ToString();
                    string SKU = reader[5].ToString();
                    double PricePerUnit = Helper.ToDouble(reader[6].ToString());
                    int Quantity = Helper.ToInt(reader[7].ToString());
                    string ShippingService = reader[8].ToString();
                    string Postcode = reader[9].ToString();

                    string csvLine = "\"" + OrderReference + "\",";
                    csvLine += "\"" + OrderMarketplace + "\",";
                    csvLine += "\"" + OrderFirstname + "\",";
                    csvLine += "\"" + OrderSurname + "\",";
                    csvLine += "\"" + OrderItemNumber + "\",";
                    csvLine += "\"" + SKU + "\",";
                    csvLine += PricePerUnit.ToString("0.00") + ",";
                    csvLine += Quantity.ToString() + ",";
                    csvLine += "\"" + ShippingService + "\",";
                    csvLine += "\"" + Postcode + "\"";

                    if (WriteToCSVFile(OrderReference, OrderMarketplace, csvLine))
                    {
                        if(!StampRecords(OrderReference, OrderMarketplace))
                        {
                            Logger.Log("Failed to stamp records as processed for Order Ref: " + OrderReference + " Marketplace: " + OrderMarketplace);
                        }
                    }
                    else
                    { 
                        Logger.Log("CSV file commit failed for Order Ref: " + OrderReference + " Marketplace: " + OrderMarketplace);
                        Logger.Log("CSV data: " + csvLine);
                    }
                }

            }
        }
    }
}
