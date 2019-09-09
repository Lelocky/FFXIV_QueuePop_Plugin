using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFXIV_QueuePop_Plugin.Logger
{
    internal static class Log
    {
        public static void Write(LogType logType, string Message)
        {
            _ = WriteToFile(string.Format("{0} - {1}: {2} ", DateTime.Now, logType, Message));
        }

        public static void Write(LogType logType, Exception ex)
        {

            _ = WriteToFile(string.Format("{0} - {1}:\nException:\n{2}", DateTime.Now, logType, ex));
        }

        public static void Write(LogType logType, string Message, Exception ex)
        {
           _ = WriteToFile(string.Format("{0} - {1}: {2}\nException:\n {3}", DateTime.Now, logType, Message, ex));
        }

        private static async Task WriteToFile(string message)
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Advanced Combat Tracker/FFXIV_QueuePop_Plugin");

            if (CheckForLog(logPath))
            {
                StringBuilder sb = new StringBuilder();

                sb.Append("\n" + message);
                File.AppendAllText(logPath + "/default.log", sb.ToString());
                sb.Clear();
            }
        }

        private static bool CheckForLog(string logFolderPath)
        {
            if (!Directory.Exists(logFolderPath))
            {
                Directory.CreateDirectory(logFolderPath);
            }

            string logFilePath = logFolderPath + "/default.log";

            if (!File.Exists(logFilePath))
            {
                File.Create(logFilePath).Dispose();
            }
            else
            {
                long filesize = new FileInfo(logFilePath).Length;

                if (filesize > 5000000)
                {
                    File.Delete(logFilePath);
                    File.Create(logFilePath).Dispose();
                }
            }

            return File.Exists(logFilePath);
        }
    }
}
