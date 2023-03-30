using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using YouTubeApiLib;

namespace YouTube_downloader_server_console
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Run();
        }

        private static void Run()
        {
            const int serverPort = 80;
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, serverPort);
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            server.Bind(endPoint);
            server.Listen(0);

            Console.WriteLine($"Server started on port {serverPort}");

            while (true)
            {
                Socket client = null;
                try
                {
                    client = server.Accept();
                    Console.WriteLine($"{client.RemoteEndPoint} is connected");

                    byte[] buffer = new byte[1024];
                    int bytesRead = client.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                    if (bytesRead == 0)
                    {
                        Console.WriteLine($"Zero bytes received from {client.RemoteEndPoint}");
                        DisconnectClient(client);
                        continue;
                    }

                    string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"{client.RemoteEndPoint} sent:\n<<<\n{msg}\n>>>\n");

                    string[] strings = msg.Split('\n');
                    string[] req = strings[0].Split(' ');
                    if (req[0] == "GET")
                    {
                        if (req[1].StartsWith("/videoinfo/"))
                        {
                            string id = req[1].Substring(11);
                            YouTubeApi api = new YouTubeApi();
                            YouTubeVideo video = api.GetVideo(new VideoId(id));
                            if (video != null)
                            {
                                string body = video.RawInfo?.RawData.ToString();
                                if (!string.IsNullOrEmpty(body))
                                {
                                    string answer = GenerateResponse(200, "OK", body);
                                    SendMessage(client, answer);
                                }
                                else
                                {
                                    SendMessage(client, GenerateResponse(404, "Not found", null));
                                }
                            }
                            else
                            {
                                SendMessage(client, GenerateResponse(404, "Not found", null));
                            }
                        }
                        else
                        {
                            SendMessage(client, GenerateResponse(400, "Bad request", null));
                        }
                    }
                    else
                    {
                        SendMessage(client, GenerateResponse(400, "Unsupported method", null));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }

                DisconnectClient(client);
            }

            StopServer(server);
        }

        private static void SendMessage(Socket client, string msg)
        {
            byte[] msgBytes = Encoding.UTF8.GetBytes(msg);
            client.Send(msgBytes);
        }

        private static void DisconnectClient(Socket client)
        {
            Console.WriteLine($"{client.RemoteEndPoint} is disconnected");
            client.Shutdown(SocketShutdown.Both);
            client.Close();
        }

        private static void StopServer(Socket socket)
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                if (ex is SocketException)
                {
                    System.Diagnostics.Debug.WriteLine($"Socket error {(ex as SocketException).ErrorCode}");
                }
                socket.Close();
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        private static string GenerateResponse(int errorCode, string msg, string body)
        {
            string t = $"HTTP/1.1 {errorCode} {msg}\r\n";
            if (!string.IsNullOrEmpty(body))
            {
                t += "Content-Type: application/json\r\n" +
                    "Access-Control-Allow-Origin: *\r\n" +
                    $"Content-Length: {body.Length}\r\n\r\n{body}";
            }
            return t;
        }
    }
}
