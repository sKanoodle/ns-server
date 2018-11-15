using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace NSServer
{
    class Program
    {
        static void Main(string[] args)
        {
            TcpListener listener = new TcpListener(IPAddress.Parse(Config.Instance.ListenIP), int.Parse(Config.Instance.ListenPort));
            listener.Start();
            while (true)
                listener.AcceptTcpClientAsync().ContinueWith(t =>
                {
                    var client = t.Result;
                    var stream = new SslStream(client.GetStream(), false);

                    try
                    {
                        stream.AuthenticateAsServer(X509Certificate.CreateFromCertFile(Config.Instance.ServerCertificatePath), clientCertificateRequired: false, checkCertificateRevocation: true);

                        foreach (string message in ReadMessages(stream))
                            SendMessage(stream, HandleMessage(message));
                    }
                    catch (AuthenticationException e)
                    {
                        Console.WriteLine("Exception: {0}", e.Message);
                        if (e.InnerException != null)
                            Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                        Console.WriteLine("Authentication failed - closing the connection.");
                    }
                    finally
                    {
                        stream.Close();
                        client.Close();
                    }
                });
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
            zone = Regex.Replace(zone, $"({Config.Instance.EntryToChange}\tIN\tA\t)(d{{2,3}}.){{3}}d{{2,3}}", $"$1{newIP}");
            File.WriteAllText(Config.Instance.ZoneFilePath, zone);
            var proc = new Process() { StartInfo = new ProcessStartInfo("/bin/bash", "sudo service bind9 reload") };
            proc.Start();
            proc.WaitForExit();
        }

        private static void SendMessage(SslStream stream, string message)
        {
            Console.WriteLine($"sending message:\n{message}");
            byte[] bytes = Encoding.ASCII.GetBytes(message);
            int offset = 0;
            while (offset < bytes.Length)
            {
                stream.Write(bytes, offset, Math.Min(BUFFER_SIZE, bytes.Length - offset));
                offset += BUFFER_SIZE;
            }
        }

        private const int BUFFER_SIZE = 256;
        private static IEnumerable<string> ReadMessages(SslStream stream)
        {
            byte[] bytes = new byte[BUFFER_SIZE];
            StringBuilder sb = new StringBuilder();
            string result;

            int i;
            while ((i = stream.ReadAsync(bytes, 0, bytes.Length).Result) != 0)
            {
                string part = Encoding.ASCII.GetString(bytes, 0, i);

                int index;
                // messages end with double newlines, so we try to find out, if there is an ending in the middle of the string
                while ((index = part.IndexOf("\n\n")) > 0)
                {
                    sb.Append(part.Substring(0, index));
                    result = sb.ToString();
                    Console.WriteLine($"received message:\n{result}\n\n");
                    yield return result;
                    sb.Clear();

                    // end of message was at end of string, so quit from processing the string
                    if (index + 2 > part.Length)
                    {
                        part = null;
                        break;
                    }

                    // cut the just output part of the string and further process the rest
                    part = part.Substring(index + 2);
                }
                sb.Append(part);
            }

        }
    }
}
