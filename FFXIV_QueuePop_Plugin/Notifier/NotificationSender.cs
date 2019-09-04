using Advanced_Combat_Tracker;
using FFXIV_QueuePop_Plugin.Logger;
using FFXIV_QueuePop_Plugin.Notifier.Entity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml;

namespace FFXIV_QueuePop_Plugin.Notifier
{
    internal class NotificationSender
    {

        public static async Task<bool> SendNotification()
        {
            try
            {
                NotificationSettings settings = NotificationSettingsHelper.GetSettings();
                //Default is set to telegram for now
                switch (settings.NotificationMode)
                {
                    case NotificationMode.Default:
                        return await SendTelegram(settings);
                    case NotificationMode.Telegram:
                        return await SendTelegram(settings);
                    case NotificationMode.GET:
                        return await SendGETUrl(settings);
                }
            }
            catch (Exception ex)
            {
                Log.Write(LogType.Error, "Notification sending error", ex);
            }

            return false;
        }


        private static async Task<string> GetAsync(string url)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

                using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    Log.Write(LogType.Info, "Sending notification");
                    return await reader.ReadToEndAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Write(LogType.Error, "Notification sending error - Get", ex);
            }

            return "";
        }

        private static async Task<bool> SendGETUrl(NotificationSettings settings)
        {
            string response = await GetAsync(settings.GetURL);

            //Write some check
            return true;
        }
        

        private static async Task<bool> SendTelegram(NotificationSettings settings)
        {
            try
            {
                if (settings.FFXIVAPISettings != null && settings.TelegramChatId != null)
                {
                    string url = string.Format("https://ffxivstats.com/notification/SendTelegramNotification?key={0}&chatId={1}", settings.FFXIVAPISettings, settings.TelegramChatId);
                    string response = await GetAsync(url);

                    //write some checks
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Write(LogType.Error, "Notification sending error - Telegram", ex);
            }

            return false;
        }
    }
}
