using Advanced_Combat_Tracker;
using FFXIV_QueuePop_Plugin.Logger;
using FFXIV_QueuePop_Plugin.Notifier.Entity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace FFXIV_QueuePop_Plugin.Notifier
{
    internal static class NotificationSettingsHelper
    {
        public static NotificationSettings GetSettings()
        {
            string settingsFile = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, "Config\\FFXIV_QueuePop_Plugin.config.xml");
            NotificationSettings notificationSettings = new NotificationSettings();

            if (File.Exists(settingsFile))
            {
                FileStream fs = new FileStream(settingsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                XmlTextReader xReader = new XmlTextReader(fs);
                XmlTextReader xReader2 = xReader;

                try
                {
                    while (xReader.Read())
                    {
                        if (xReader.NodeType == XmlNodeType.Element)
                        {
                            if (xReader.LocalName == "SettingsSerializer")
                            {
                                using (var innerReader = xReader.ReadSubtree())
                                {
                                    while (innerReader.ReadToFollowing("TextBox"))
                                    {
                                        ProcessSettings(notificationSettings, xReader);
                                    }
                                }
                            }
                        }
                    }
                }

                catch (Exception ex)
                {
                    Log.Write(LogType.Error, ex);
                }

                xReader.Close();
            }

            return notificationSettings;
        }

        private static void ProcessSettings(NotificationSettings notificationSettings, XmlTextReader xReader)
        {
            if (xReader.GetAttribute("Name").Equals("txtApiKey"))
            {
                notificationSettings.FFXIVAPISettings = xReader.GetAttribute("Value");
            }

            if (xReader.GetAttribute("Name").Equals("txtTelegramChatId"))
            {
                notificationSettings.TelegramChatId = xReader.GetAttribute("Value");
            }

            if (xReader.GetAttribute("Name").Equals("cbMode"))
            {
                notificationSettings.NotificationMode = ConvertMode(xReader.GetAttribute("Value"));
            }

            if (xReader.GetAttribute("Name").Equals("txtGetURL"))
            {
                notificationSettings.GetURL = xReader.GetAttribute("Value");
            }
        }

        private static NotificationMode ConvertMode(string notificationMode)
        {
            switch (notificationMode)
            {
                case "Telegram":
                    return NotificationMode.Telegram;
                case "GET":
                    return NotificationMode.GET;
                default:
                    return NotificationMode.Default;
            }
        }
    }
}
