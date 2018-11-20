using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NSServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Listener listener = new Listener();
            listener.Start(CreateMessageHandler).Wait();
        }

        private static Func<string, string> CreateMessageHandler()
        {
            bool isAuthenticated = false;

            string messageHandler(string message)
            {
                var split = message.Split(": ");
                (string method, string data) = (split[0], split[1]);

                switch (method)
                {
                    case "login":
                        if (Config.Instance.IsPasswordMatch(data))
                        {
                            isAuthenticated = true;
                            return "ok\n\n";
                        }
                        isAuthenticated = false;
                        return "wrong password\n\n";
                    case "change-ip" when isAuthenticated:
                        return ChangeConfig(data);
                    default:
                        return "invalid message\n\n";
                }
            }
            return messageHandler;
        }

        private static string ChangeConfig(string newIP)
        {
            if (!Regex.IsMatch(newIP, @"^(\d{1,3}\.){3}\d{1,3}$"))
                return "wrong IP format\n\n";

            string zone = File.ReadAllText(Config.Instance.ZoneFilePath);

            string serialRegex = @"(SOA.*\n\W+)(\d+)";
            string recordRegex = "(\n" + Regex.Escape(Config.Instance.EntryToChange) + @"\W+IN\W+A\W+)(\d{1,3}\.){3}\d{1,3}";

            zone = Regex.Replace(zone, serialRegex,m => $"{m.Groups[1].Value}{int.Parse(m.Groups[2].Value) + 1}");
            zone = Regex.Replace(zone, recordRegex, m => $"{m.Groups[1].Value}{newIP}");
            File.WriteAllText(Config.Instance.ZoneFilePath, zone);

            var proc = new Process() { StartInfo = new ProcessStartInfo("/bin/bash", "service bind9 reload") };
            proc.Start();
            proc.WaitForExit();
            return "ok\n\n";
        }
    }
}
