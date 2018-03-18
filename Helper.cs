using System;
using System.Configuration;
using System.Globalization;

namespace OrderProcessor
{
    /// <summary>
    /// Class of static functions for resusable common functions unrelated to a specific class
    /// </summary>
    public class Helper
    {
        public static string ReadSetting(string key, string defaultValue)
        {
            string result = defaultValue;
            try
            {
                if (ConfigurationManager.AppSettings[key] != null)
                    result = ConfigurationManager.AppSettings[key].ToString();
            }
            catch (ConfigurationErrorsException)
            {
                Console.WriteLine("Error reading app settings");
            }
            return result;
        }

        public static string ReadSetting(string key)
        {
            return ReadSetting(key, string.Empty);
        }

        public static int ReadSettingInt(string key, int defaultValue)
        {
            int result = defaultValue;
            int.TryParse(ReadSetting(key, defaultValue.ToString()), out result);
            return result;
        }

        public static int ReadSettingInt(string key)
        {
            return ReadSettingInt(key, 0);
        }

        public static short ReadSettingShort(string key, short defaultValue)
        {
            short result = defaultValue;
            short.TryParse(ReadSetting(key, defaultValue.ToString()), out result);
            return result;
        }

        public static short ReadSettingShort(string key)
        {
            return ReadSettingShort(key, 0);
        }

        public static bool WriteSetting(string key, string value)
        {
            bool ret = false;
            try
            {
                ConfigurationManager.AppSettings[key] = value;
                ret = true;
            }
            catch (ConfigurationErrorsException)
            {
                Console.WriteLine("Error writting app settings");
            }
            return ret;
        }

        public static short ToShort(string numericString)
        {
            short ret = 0;
            short.TryParse(numericString, out ret);
            return ret;
        }

        public static int ToInt(string numericString)
        {
            int ret = 0;
            int.TryParse(numericString, out ret);
            return ret;
        }
        public static double ToDouble(string numericString)
        {
            double ret = 0;
            double.TryParse(numericString, out ret);
            return ret;
        }
    }
}
