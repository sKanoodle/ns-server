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
                        return "wrong password";
                    return "ok";
                case "change-ip":
                    // TODO: error handling?
                    ChangeConfig(data);
                    return "ok";
                default:
                    return "invalid message";
            }
        }

        private static void ChangeConfig(string newIP)
        {
            string zone = File.ReadAllText(Config.Instance.ZoneFilePath);

            string serialRegex = @"(SOA.*\n\W+)(\d+)";
            string recordRegex = "(" + Config.Instance.EntryToChange + @"\W+IN\W+A\W+)(\d{1,3}\.){3}\d{1,3}";

            var match = Regex.Match(zone, serialRegex);
            int serial = int.Parse(match.Groups[2].Value);

            zone = Regex.Replace(zone, serialRegex, $"$1{serial + 1}");
            zone = Regex.Replace(zone, recordRegex, $"$1{newIP}");
            File.WriteAllText(Config.Instance.ZoneFilePath, zone);

            var proc = new Process() { StartInfo = new ProcessStartInfo("/bin/bash", "service bind9 reload") };
            proc.Start();
            proc.WaitForExit();
        }
    }
}
