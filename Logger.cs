using System;
using System.IO;
using System.Threading;


namespace OrderProcessor
{
    /// <summary>
    /// Class to provide file logging functionality.
    /// </summary>
    class Logger
    {
        private static string logpath = Helper.ReadSetting("LogPath", "C:").TrimEnd('\\');
        private static bool logEnabled = Helper.ReadSetting("LogEnabled", "N") == "Y" ? true : false;
        private static bool logToConsole = Helper.ReadSetting("LogToConsole", "N") == "Y" ? true : false;

        /// <summary>
        /// This method writes to the log file, it creates a new file for each day automatically
        /// </summary>
        async public static void Log(string data)
        {
            DateTime dt = DateTime.Now;
            string message = string.Empty;
            message = dt.ToString("dd/MM/yyyy HH:mm:ss") + " | " + Thread.CurrentThread.ManagedThreadId.ToString() + " | " + data;

            try
            {
                if (logEnabled)
                {
                    StreamWriter sw = File.AppendText(logpath + "\\OrderProcessorLog_" + DateTime.Now.ToString("yyyyMMdd") + ".txt");
                    await sw.WriteLineAsync(message);
                    if (logToConsole)
                    {
                        Console.WriteLine(message);
                    }
                    sw.Close();
                }
            }
            catch
            {
                logEnabled = false; // TODO: Should abort if cant write to log? - perhaps send an email, or use some common alerter class...
                //Email.Send("ERROR: Unable to write to log file: " + message);
            }
        }
    }
}