using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
            const int serverPort = 5555;
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, serverPort);
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            server.Bind(endPoint);
            server.Listen((int)SocketOptionName.MaxConnections);

            Console.WriteLine($"Server started on port {serverPort}");

            while (true)
            {
                Socket client = server.Accept();
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"{DateTime.Now:dd.MM.yyyy HH:mm:ss}> {client.RemoteEndPoint} is connected");

                ProcessClient(client);

                DisconnectClient(client);
            }

            StopServer(server);
        }

        private static void ProcessClient(Socket client)
        {
            byte[] buffer = new byte[1024];
            int bytesRead = client.Receive(buffer, 0, buffer.Length, SocketFlags.None);
            if (bytesRead == 0)
            {
                Console.WriteLine($"{DateTime.Now:dd.MM.yyyy HH:mm:ss}> Zero bytes received from {client.RemoteEndPoint}");
                return;
            }

            string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            string[] strings = msg.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            Console.Write($"{DateTime.Now:dd.MM.yyyy HH:mm:ss}> {client.RemoteEndPoint} sent: {strings[0]}");

            string[] req = strings[0].Split(new char[] { ' ' }, 3);
            if (req[0] == "GET")
            {
                if (req[1].StartsWith("/videoinfo/"))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(" Accepted!");

                    string id = req[1].Substring(11);
                    YouTubeApi api = new YouTubeApi();
                    YouTubeVideo video = api.GetVideo(new VideoId(id));
                    if (video != null)
                    {
                        string body = video.RawInfo?.RawData.ToString();
                        if (!string.IsNullOrEmpty(body))
                        {
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.Write($"{DateTime.Now:dd.MM.yyyy HH:mm:ss}> Sending a video info ");
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write(id);
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine($" to the client {client.RemoteEndPoint}");

                            string answer = GenerateResponse(200, "OK", body);
                            SendMessage(client, answer);
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.Write($"{DateTime.Now:dd.MM.yyyy HH:mm:ss}> ");
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write("ERROR!");
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine($" Video {id} not found!");

                            SendMessage(client, GenerateResponse(404, "Not found", null));
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write("{DateTime.Now:dd.MM.yyyy HH:mm:ss}> ");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("ERROR!");
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($" Video {id} not found!");

                        SendMessage(client, GenerateResponse(404, "Not found", null));
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(" Rejected!");

                    SendMessage(client, GenerateResponse(400, "Bad request", null));
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(" Rejected!");

                SendMessage(client, GenerateResponse(400, "Unsupported method", null));
            }
        }

        private static void SendMessage(Socket client, string msg)
        {
            byte[] msgBytes = Encoding.UTF8.GetBytes(msg);
            client.Send(msgBytes);
        }

        private static void DisconnectClient(Socket client)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"{DateTime.Now:dd.MM.yyyy HH:mm:ss}> {client.RemoteEndPoint} is disconnected");
            client.Shutdown(SocketShutdown.Both);
            client.Close();
        }

        private static void StopServer(Socket serverSocket)
        {
            try
            {
                serverSocket.Shutdown(SocketShutdown.Both);
                serverSocket.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                if (ex is SocketException)
                {
                    System.Diagnostics.Debug.WriteLine($"Socket error {(ex as SocketException).ErrorCode}");
                }
                serverSocket.Close();
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        private static string GenerateResponse(int errorCode, string msg, string body)
        {
            string t = $"HTTP/1.1 {errorCode} {msg}\r\n";
            if (!string.IsNullOrEmpty(body))
            {
                byte [] bodyBytes = Encoding.UTF8.GetBytes(body);
                t += "Content-Type: application/json\r\n" +
                    "Access-Control-Allow-Origin: *\r\n" +
                    $"Content-Length: {bodyBytes.Length}\r\n\r\n{body}";
            }
            else
            {
                t += "\r\n";
            }
            return t;
        }
    }
}
