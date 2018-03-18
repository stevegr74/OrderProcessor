using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;

namespace OrderProcessor
{
    public partial class Controller : ServiceBase
    {
        public Controller()
        {
            InitializeComponent();
        }

        /* 
         * To run as a console app for development:
         * Right click on the service project "OrderProcessor" and select "Properties".
         * In the "Application" tab under "Output type" select "Console Application" ("Windows Application" by default).
         * Save the changes. The app will now run in a console window.
         * Reverse this change back to "Windows Application" to build as a service again
         */

        //TODO: Running in console mode for debugging - return to a service for final release!

        internal void StartAsConsoleApplication()
        {
            Console.WriteLine("Press X to stop..");
            string[] args = null;
            OnStart(args);
            while (Console.ReadKey(true).Key != ConsoleKey.X);
            OnStop();
            Console.WriteLine("Program end - Press any key");
            Console.ReadKey();
        }

        // No real need to encapsulate these, only used here once at start up.
        private DateTime lastRun = DateTime.MinValue;
        private static string processingDays = Helper.ReadSetting("ProcessingDays", "0123456"); // 0 = Sunday
        private static short processingInterval = (short)Helper.ReadSettingInt("ProcessingInterval", 1); // Scan time in minutes

        private bool scanNow()
        {
            bool ret = false;
            DateTime now = DateTime.Now;

            if (processingDays.Contains(((int)now.DayOfWeek).ToString()))
            {
                if (lastRun == DateTime.MinValue || now >= lastRun.AddMinutes(processingInterval))
                {
                    lastRun = now;
                    ret = true;
                }
            }

            return ret;
        }

        protected override void OnStart(string[] args)
        { // start our 1 controller thread
            new Thread(delegate () {
                startController();
            }).Start();
        }

        private bool workAborted;
        private readonly object _sleepObject = new object();

        private void startController()
        {
            workAborted = false;
            Logger.Log("Controller has started");
            Logger.Log("Working days: " + processingDays);
            Logger.Log("Scan every " + processingInterval + " minutes");

            FileLoader fileLoader = new FileLoader();
            CSVWriter csvWriter = new CSVWriter();

            // currently 1 controller handling file import and csvcreation.
            // we have scope here to have a thread per input filetype if required, or even multiple thread per file type.
            while (!workAborted)
            {
                if (scanNow())
                {
                    fileLoader.processFiles();
                    csvWriter.createFiles(); // after we process any files in, lets see if we have all the data to create any csvs
                }

                lock (_sleepObject)// this is an interuptable sleep implementation.
                {
                    Monitor.Wait(_sleepObject, 5000); // sleep for a while betweeen checks.
                }
            }

            Logger.Log("Controller has stopped");
        }


        protected override void OnStop()
        {
            workAborted = true;
            // tell the workers to quit
            FileLoader.AbortWork = true;
            CSVWriter.AbortWork = true;
            lock (_sleepObject) // signal the sleeping controller to wake up
            {
                Monitor.Pulse(_sleepObject);
            }
        }
    }
}
