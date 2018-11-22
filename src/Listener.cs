using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace NSServer
{
    class Listener
    {
        private const int BUFFER_SIZE = 256;

        // will not work on widnows. to run on windows either
        //      - create cert with 'openssl pkcs12 -export -out cert.p12 -in cert.pem -inkey privkey.pem'
        //      - or wrap certificate a second time like in https://github.com/dotnet/corefx/issues/24454#issuecomment-388231655
        //      - or hope the bug is already fixed at this time
        private X509Certificate2 Certificate => new X509Certificate2(Config.Instance.ServerCertificatePath).CopyWithPrivateKey(PemKeyLoader.DecodeRSAPkcs8(Config.Instance.ServerPrivateKeyPath));

        public async Task Start(Func<Func<string, string>> createMessageHandler)
        {
            TcpListener listener = new TcpListener(IPAddress.Parse(Config.Instance.ListenIP), int.Parse(Config.Instance.ListenPort));
            listener.Start();
            while (true)
            {
                Console.WriteLine($"listening for new client on {Config.Instance.ListenIP}:{Config.Instance.ListenPort}...");
                await listener.AcceptTcpClientAsync().ContinueWith(async t =>
                {
                    Console.WriteLine("client connected!");
                    var handleMessage = createMessageHandler();
                    var client = t.Result;
                    var stream = new SslStream(client.GetStream(), false);

                    try
                    {
                        stream.AuthenticateAsServer(Certificate, clientCertificateRequired: false, checkCertificateRevocation: true);

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
                    // async eats exceptions
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                    finally
                    {
                        stream.Close();
                        client.Close();
                    }
                });
            }
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

            int i;
            while ((i = stream.ReadAsync(bytes, 0, bytes.Length).Result) != 0)
            {
                string part = Encoding.ASCII.GetString(bytes, 0, i);

                // check if double newline was spread over multiple mesasges
                if (sb.Length > 0 && sb[sb.Length - 1] == '\n' && part[0] == '\n')
                {
                    sb.Remove(sb.Length - 1, 1);
                    yield return generateOutput();
                    part = part.Substring(1);
                }

                int index;
                // messages end with double newlines, so we try to find out, if there is an ending in the middle of the string
                while ((index = part.IndexOf("\n\n")) > 0)
                {
                    sb.Append(part.Substring(0, index));
                    yield return generateOutput();

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

            string generateOutput()
            {
                string result = sb.ToString();
                Console.WriteLine($"received message:\n{result}\n\n");
                sb.Clear();
                return result;
            }

        }
    }
}
