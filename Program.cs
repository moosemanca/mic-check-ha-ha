using System.Collections.Generic;
using System.Windows.Automation;
using System.Linq;
using System;
using System.Threading;
using System.Net.Http;
using System.Configuration;

namespace MicCheck
{
    static class AutomationElementHelpers
    {
        public static AutomationElement
        Find(this AutomationElement root, string name)
        {
            return root.FindFirst(
             TreeScope.Descendants,
             new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ToolBar), new PropertyCondition(AutomationElement.NameProperty, name)));

        }

        public static IEnumerable<AutomationElement>
        EnumChildButtons(this AutomationElement parent)
        {
            return parent == null ? Enumerable.Empty<AutomationElement>()
                                  : parent.FindAll(TreeScope.Children,
              new PropertyCondition(AutomationElement.ControlTypeProperty,
                                    ControlType.Button)).Cast<AutomationElement>();
        }
        public static bool
        InvokeButton(this AutomationElement button)
        {
            var invokePattern = button.GetCurrentPattern(InvokePattern.Pattern)
                               as InvokePattern;
            if (invokePattern != null)
            {
                invokePattern.Invoke();
            }
            return invokePattern != null;
        }
    }

    internal class Program
    {
        // I have two seperate webhooks for separate actions in HomeAssistant. If toggle is action, then set both app.configs to same value.
        //app.config entry for URL to post to when Mic is detected as in use
        private string webhookURLON =  ConfigurationManager.AppSettings["webhookurl-on"];
        //app.config entry for URL to post to when Mic is detected as NOT in use
        private string webhookURLOFF = ConfigurationManager.AppSettings["webhookurl-off"];
        static void Main(string[] args)
        {
            if(CheckForMicInUse())
            {
                Console.WriteLine("Mic currently in use. Monitoring for no more use");
                MonitorForNotInUse();
            }
            else
            {
                Console.WriteLine("Mic currently is NOT in use. Monitoring for more use");
                MonitorForUse();
            }
            
        }
        public static IEnumerable<AutomationElement> EnumNotificationIcons()
        {
            List <AutomationElement> result = new List<AutomationElement>();
            foreach (var button in AutomationElement.RootElement.Find(
                            "User Promoted Notification Area").EnumChildButtons())
            {
                result.Add(button);
            }

            return result;
        }
        
        /// <summary>
        /// check current Notification Area VISIBLE Icons for microphone icon that says is using your microphone
        /// </summary>
        /// <returns></returns>
        public static bool CheckForMicInUse()
        {
            bool micInUse = false;
            foreach (var icon in EnumNotificationIcons())
            {
                var name = icon.GetCurrentPropertyValue(AutomationElement.NameProperty)
                           as string;
                if (name.Contains("is using your microphone"))
                    micInUse = true;
            }
            return micInUse;
        }


        /// <summary>
        /// mediocre endless loop for checking if mic is in use. Once found, it calls webhook and initiates similar function checking for not in use
        /// </summary>
        public static void MonitorForUse()
        {
            bool isInUse = false;
            do
            {
                isInUse = CheckForMicInUse();
                Thread.Sleep(1000);
            } while (!isInUse);
            
            var result = CallWebHook("http://storagenode2:8123/api/webhook/-dNwnR5_OPElTTSdXsj8Cpnhr");
            Console.WriteLine("Microphone in use. Server notified:  " + result.ToString());
            MonitorForNotInUse();
        }

        /// <summary>
        /// mediocre endless loop for checking if mic is NOT in use. Once found, it calls webhook and initiates similar function checking for in use
        /// </summary>
        public static void MonitorForNotInUse()
        {
            bool isInUse = true;
            do
            {
                isInUse = CheckForMicInUse();
                Thread.Sleep(1000);
            } while (isInUse);
            var result = CallWebHook("http://storagenode2:8123/api/webhook/-FGmwAQrpJeP8HXhNS4Fx2dZb");
            Console.WriteLine("Microphone no longer in use.");
            MonitorForUse();
        }


        /// <summary>
        /// posting to webhook and returning success or not
        /// </summary>
        /// <returns>Boolean as to whether or not the call was successful. </returns>
        public static bool CallWebHook(string url)
        {
            HttpClient client = new HttpClient();
            var result =  client.PostAsync(url, null).Result;
            return result.IsSuccessStatusCode;
        }

    }
}
