using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NSServer
{
    class Listener
    {
        private const int BUFFER_SIZE = 256;

        public async Task Start(Func<string, string> handleMessage)
        {
            TcpListener listener = new TcpListener(IPAddress.Parse(Config.Instance.ListenIP), int.Parse(Config.Instance.ListenPort));
            listener.Start();
            while (true)
                await listener.AcceptTcpClientAsync().ContinueWith(async t =>
                {
                    Console.WriteLine("listening for new client");
                    var client = t.Result;
                    var stream = new SslStream(client.GetStream(), false);

                    try
                    {
                        stream.AuthenticateAsServer(X509Certificate.CreateFromCertFile(Config.Instance.ServerCertificatePath), clientCertificateRequired: false, checkCertificateRevocation: true);

                        foreach (string message in ReadMessages(stream))
                            await SendMessage(stream, handleMessage(message));
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

        private async Task SendMessage(SslStream stream, string message)
        {
            Console.WriteLine($"sending message:\n{message}");
            byte[] bytes = Encoding.ASCII.GetBytes(message);
            int offset = 0;
            while (offset < bytes.Length)
            {
                await stream.WriteAsync(bytes, offset, Math.Min(BUFFER_SIZE, bytes.Length - offset));
                offset += BUFFER_SIZE;
            }
        }

        private IEnumerable<string> ReadMessages(SslStream stream)
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
