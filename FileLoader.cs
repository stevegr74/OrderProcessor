using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System;
using Newtonsoft.Json.Schema;

namespace OrderProcessor
{
    public enum FILETYPE
    {
        unknown = 0,
        orders,
        orderitems,
        shipments
    }
    public abstract class baseFile
    {
        public string fileBuffer { get; set; }
        abstract public FILETYPE fileType { get; }
        abstract public T DeserializeObject<T>();

    }

    // Add differnt file types by derviving new formats fromthe base class
    public class jsonFile : baseFile
    {
        private JToken root { get; set; }
        private JToken rows { get; set; }

        public override FILETYPE fileType { get; }

        public override T DeserializeObject<T>()
        {
            return JsonConvert.DeserializeObject<T>(rows.ToString());
        }


        public jsonFile(string file)
        {
            try
            {
                fileType = FILETYPE.unknown;
                fileBuffer = file;
                root = JObject.Parse(fileBuffer);

                //TODO: finish this schema resolver - currently the code will partial process files as validation not in place.
                JsonSchema schema = JsonSchema.Parse(fileBuffer);
                if (root.IsValid(schema))
                {
                    if (root["orders"] != null)
                    {
                        fileType = FILETYPE.orders;
                        rows = root["orders"];
                    }
                    else if (root["order items"] != null)
                    {
                        fileType = FILETYPE.orderitems;
                        rows = root["order items"];
                    }
                    else if (root["shipments"] != null)
                    {
                        fileType = FILETYPE.shipments;
                        rows = root["shipments"];
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Exception caught: " + ex.Source + " : " + ex.Message);
            }
        }
    }

    public class FileLoader
    {
        private static bool abortWork = false;

        public static bool AbortWork
        {
            set
            {
                abortWork = value;
            }
        }

        public void processFiles()
        {
            string[] fileList = Directory.GetFiles(Helper.ReadSetting("InputFilePath"));
            string processingFolder = Helper.ReadSetting("InputFilePath").TrimEnd('\\') + "\\Processing\\";
            string errorFolder = Helper.ReadSetting("InputFilePath").TrimEnd('\\') + "\\Error\\";
            string processedFolder = Helper.ReadSetting("InputFilePath").TrimEnd('\\') + "\\Processed\\";
            string[] stuckFileList = Directory.GetFiles(processingFolder);

            try
            {
                //ensure sub dirs exist
                Directory.CreateDirectory(processingFolder);
                Directory.CreateDirectory(errorFolder);
                Directory.CreateDirectory(processedFolder);

                //move stuck files to error folder
                foreach (string filename in stuckFileList)
                {
                    try
                    {
                        string errorFilename = errorFolder + Path.GetFileName(filename);
                        File.Move(filename, errorFilename); // Try to move stuck file to the error folder
                    }
                    catch
                    {
                        // couldnt move file 
                        Logger.Log("Error moving stuck file to error folder: " + filename);
                    }
                    if (abortWork)
                        break;

                }

                if (!abortWork)
                {
                    // process the files
                    foreach (string filename in fileList)
                    {
                        try
                        {
                            string processFilename = processingFolder + Path.GetFileName(filename);
                            File.Move(filename, processFilename); // Try to move to the processing folder
                            if (processFile(processFilename))
                            {
                                try
                                {
                                    string processedFilename = processedFolder + Path.GetFileName(filename);
                                    File.Move(processFilename, processedFilename); // Try to move to the processed folder
                                }
                                catch
                                {
                                    Logger.Log("Error moving processed file to processed folder: " + processFilename);
                                    // TODO: this might be a problem. the file has been processed, but we couldnt move it, so it will automatically get moved to the error file later.
                                }
                            }
                        }
                        catch
                        {
                            // couldnt move file - still being written?...
                            // code will auto try again next loop round
                        }
                        if (abortWork)
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Exception caught: " + ex.Source + " : " + ex.Message);
            }
        }

        private bool processFile(string filename)
        {
            bool ret = false;

            try
            {
                string fileBuffer = null;

                using (StreamReader r = new StreamReader(filename))
                {
                    fileBuffer = r.ReadToEnd();
                }

                if (fileBuffer != null)
                {
                    if (fileBuffer.Length > 0)
                    {
                        List<Order> orders;
                        List<OrderItem> orderItems;
                        List<Shipment> shipments;

                        baseFile file = null;

                        string fileFormat = Path.GetExtension(filename); // for now, use the file extention to identify files.

                        switch(fileFormat)
                        {
                            case ".json":
                                file = new jsonFile(fileBuffer);
                                break;
                            default:
                                // unknown
                                break;
                        }

                        if (file != null)
                        {
                            switch (file.fileType)
                            {
                                case FILETYPE.orders:
                                    orders = file.DeserializeObject<List<Order>>();
                                    foreach (Order order in orders)
                                    {
                                        order.CommitToDatabase();
                                    }
                                    ret = true;
                                    break;
                                case FILETYPE.orderitems:
                                    orderItems = file.DeserializeObject<List<OrderItem>>();
                                    foreach (OrderItem orderItem in orderItems)
                                    {
                                        orderItem.CommitToDatabase();
                                    }
                                    ret = true;
                                    break;
                                case FILETYPE.shipments:
                                    shipments = file.DeserializeObject<List<Shipment>>();
                                    foreach (Shipment shipment in shipments)
                                    {
                                        shipment.CommitToDatabase();
                                    }
                                    ret = true;
                                    break;
                                default:
                                    break;
                            }
                        }
                        else
                        {
                            Logger.Log("Error processing file");
                        }
                    }
                    else
                    {
                        Logger.Log("File is empty");
                    }
                }
                else
                {
                    Logger.Log("Error loading file");
                    // UNKNOWN FILE/ERROR
                }
            }
            catch(Exception ex)
            {
                Logger.Log("Exception caught: " + ex.Source + " : " + ex.Message);
            }
            return ret;
        }
    }
}
