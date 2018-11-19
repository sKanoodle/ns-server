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
            listener.Start(HandleMessage).Wait();
        }

        private static string HandleMessage(string message)
        {
            var split = message.Split(": ");
            (string method, string data) = (split[0], split[1]);

            switch (method)
            {
                case "login":
                    if (Config.Instance.IsPasswordMatch(data))
                        return "wrong password\n\n";
                    return "ok\n\n";
                case "change-ip":
                    // TODO: error handling?
                    ChangeConfig(data);
                    return "ok\n\n";
                default:
                    return "invalid message\n\n";
            }
        }

        private static void ChangeConfig(string newIP)
        {
            string zone = File.ReadAllText(Config.Instance.ZoneFilePath);

            string serialRegex = @"(SOA.*\n\W+)(\d+)";
            string recordRegex = "(" + Config.Instance.EntryToChange + @"\W+IN\W+A\W+)(\d{1,3}\.){3}\d{1,3}";

            zone = Regex.Replace(zone, serialRegex,m => $"{m.Groups[1].Value}{int.Parse(m.Groups[2].Value) + 1}");
            zone = Regex.Replace(zone, recordRegex, m => $"{m.Groups[1].Value}{newIP}");
            File.WriteAllText(Config.Instance.ZoneFilePath, zone);

            var proc = new Process() { StartInfo = new ProcessStartInfo("/bin/bash", "service bind9 reload") };
            proc.Start();
            proc.WaitForExit();
        }
    }
}
