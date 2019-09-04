using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFXIV_QueuePop_Plugin.Notifier.Entity
{
    internal class NotificationSettings
    {
        public string FFXIVAPISettings { get; set; }
        public string TelegramChatId { get; set; }
        public NotificationMode NotificationMode { get; set; }
        public string GetURL { get; set; }
    }
}
