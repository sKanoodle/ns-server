using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NSServer
{
    class Program
    {
        private static readonly object Lock = new object();

        static void Main(string[] args)
        {
            Listener listener = new Listener();
            listener.Start(CreateMessageHandler).Wait();
        }

        private static Func<string, string> CreateMessageHandler()
        {
            string entryToChange = null;
            bool isAuthenticated = false;

            string login(string data)
            {
                var split = data.Split("\n", StringSplitOptions.RemoveEmptyEntries);
                if (split.Length != 2)
                    return "malformed message\n\n";

                (string user, string password) = (split[0], split[1]);

                Config.ClientGroup client = Config.Instance.Clients.FirstOrDefault(c => c.EntryToChange == user);
                if (client == default)
                    return "wrong user or password\n\n";

                if (!client.IsPasswordMatch(password))
                {
                    entryToChange = null;
                    isAuthenticated = false;
                    return "wrong user or password\n\n";
                }

                entryToChange = client.EntryToChange;
                isAuthenticated = true;
                return "ok\n\n";
            }

            string changeConfig(string newIP)
            {
                if (!Regex.IsMatch(newIP, @"^(\d{1,3}\.){3}\d{1,3}$"))
                    return "wrong IP format\n\n";

                if (String.IsNullOrEmpty(entryToChange))
                    return "given entry is empty\n\n";

                lock (Lock)
                {
                    string zone = File.ReadAllText(Config.Instance.ZoneFilePath);

                    string serialRegex = @"(SOA.*\n\W+)(\d+)";
                    string recordRegex = "(\n" + Regex.Escape(entryToChange) + @"\W+IN\W+A\W+)((\d{1,3}\.){3}\d{1,3})";

                    if (Regex.Match(zone, recordRegex).Groups[2].Value == newIP)
                        return "ok\nno change\n\n";

                    zone = Regex.Replace(zone, serialRegex, m => $"{m.Groups[1].Value}{int.Parse(m.Groups[2].Value) + 1}");
                    zone = Regex.Replace(zone, recordRegex, m => $"{m.Groups[1].Value}{newIP}");
                    File.WriteAllText(Config.Instance.ZoneFilePath, zone);

                    var proc = new Process() { StartInfo = new ProcessStartInfo("/bin/bash", "service bind9 reload") };
                    proc.Start();
                    proc.WaitForExit();
                }
                return "ok\n\n";
            }

            string messageHandler(string message)
            {
                var split = message.Split(": ");
                (string method, string data) = (split[0], split[1]);

                switch (method)
                {
                    case "login":                               return login(data);
                    case "change-ip" when isAuthenticated:      return changeConfig(data);
                    default:                                    return "invalid message\n\n";
                }
            }
            return messageHandler;
        }
    }
}
