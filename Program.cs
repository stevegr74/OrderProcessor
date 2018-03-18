using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.Reflection;
using System.ServiceProcess;

namespace OrderProcessor
{
    class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                if (args.Length == 0)
                {
                    StartController();
                }
                else
                {
                    switch (args[0].ToLower())
                    {
                        case "-?":
                        case "/?":
                            Console.WriteLine("Usage: OrderProcessor <fileLocation> <CSVOutputLocation>");
                            break;
                        case "-i":
                        case "/i":
                            InstallService();
                            break;
                        case "-u":
                        case "/u":
                            UninstallService();
                            break;
                        default:
                            if (args.Length == 2)
                            {
                                // This is a work around to deal with the way the service works, reading values from the app.config
                                // so this updates the app.config with the new locations.
                                Helper.WriteSetting("InputFilePath", args[0].ToLower());
                                Helper.WriteSetting("OutputFilePath", args[1].ToLower());
                            }
                            StartController();
                            break;
                    }
                }
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                    new Controller()
                };
                ServiceBase.Run(ServicesToRun);
            }
        }

        private static void StartController()
        {
            Controller serviceObject = new Controller();
            serviceObject.StartAsConsoleApplication();
            serviceObject.Dispose();
            serviceObject = null;
        }

        private static bool InstallService()
        {
            try
            {
                // install the service with the Windows Service Control Manager (SCM)
                ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
                Console.WriteLine("Service installed");
                return true;
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null && ex.InnerException.GetType() == typeof(Win32Exception))
                {
                    Win32Exception wex = (Win32Exception)ex.InnerException;
                    Console.WriteLine("Error(0x{0:X}): Service already installed", wex.ErrorCode);
                }
                else
                {
                    Console.WriteLine(ex.ToString());
                }
            }
            return false;
        }

        private static bool UninstallService()
        {
            try
            {
                // uninstall the service from the Windows Service Control Manager (SCM)
                ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
                Console.WriteLine("Service uninstalled");
                return true;
            }
            catch (Exception ex)
            {
                if (ex.InnerException.GetType() == typeof(Win32Exception))
                {
                    Win32Exception wex = (Win32Exception)ex.InnerException;
                    Console.WriteLine("Error(0x{0:X}): Service not installed", wex.ErrorCode);
                }
                else
                {
                    Console.WriteLine(ex.ToString());
                }
            }
            return false;
        }
    }

}
