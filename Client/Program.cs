using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Common;
using Newtonsoft.Json;
using System.IO;

namespace Client
{
    internal class Program
    {
        private static string ServerIp;
        private static int ServerPort;
        private static TcpClient ClientInstance;
        private static NetworkStream Stream;
        private static Guid Token;
        private static DateTime ConnectionTime;

        private static void Main(string[] args)
        {
            ServerIp = GetServerIp();
            ServerPort = GetServerPort();

            Console.WriteLine($"Connecting to server {ServerIp}:{ServerPort}...");
            ClientInstance = new TcpClient();
            ClientInstance.Connect(ServerIp, ServerPort);
            Stream = ClientInstance.GetStream();

            Console.WriteLine("Connected to server.");
            ConnectionTime = DateTime.Now;

            Task.Run(() => ReceiveMessages());

            Console.WriteLine("List of commands: \n/register login password\n/auth login password\n/gettoken\n/updatekey\n/getinfo\n/disconnect");
            while (true)
            {
                string command = Console.ReadLine();
                if (command == "/disconnect")
                {
                    SendCommand(command);
                    Console.WriteLine("Disconnected from server. Press any key to exit.");
                    Console.ReadKey();
                    break;
                }
                SendCommand(command);
            }
        }

        private static void SendCommand(string command)
        {
            try
            {
                Command cmd = new Command
                {
                    Message = command,
                    id = Token
                };
                string jsonCommand = JsonConvert.SerializeObject(cmd);
                byte[] data = Encoding.UTF8.GetBytes(jsonCommand);
                Stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static async Task ReceiveMessages()
        {
            try
            {
                byte[] data = new byte[1024];
                int bytesRead;
                while (ClientInstance.Connected)
                {
                    try
                    {
                        bytesRead = await Stream.ReadAsync(data, 0, data.Length);
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"Connection lost: {ex.Message}");
                        break;
                    }

                    if (bytesRead == 0)
                    {
                        Console.WriteLine("Server closed the connection.");
                        break;
                    }
                    string response = Encoding.UTF8.GetString(data, 0, bytesRead);
                    Console.WriteLine("Server response: " + response);
                    if (response.StartsWith("You have been kicked from the server.") || response.StartsWith("You are in the blacklist."))
                    {
                        Console.WriteLine("Disconnected from server. Press any key to exit.");
                        Console.ReadKey();
                        Environment.Exit(0);
                    }

                    if (response.StartsWith("Token:"))
                    {
                        Token = Guid.Parse(response.Substring("Token: ".Length));
                        Console.WriteLine($"Received token: {Token}");
                    }
                    else if (response.StartsWith("Info:"))
                    {
                        Console.WriteLine(response.Substring("Info: ".Length));
                    }
                    else if (response.StartsWith("Error:"))
                    {
                        Console.WriteLine(response.Substring("Error: ".Length));
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("Connection has been closed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static string GetServerIp()
        {
            while (true)
            {
                Console.Write("Server IP: ");
                string address = Console.ReadLine();
                if (IPAddress.TryParse(address, out _))
                {
                    return address;
                }
                Console.WriteLine("Invalid IP Format!");
            }
        }
        private static int GetServerPort()
        {
            while (true)
            {
                Console.Write("Server Port: ");
                string port = Console.ReadLine();
                if (int.TryParse(port, out int portNumber) && portNumber > 0)
                {
                    return portNumber;
                }
                Console.WriteLine("Invalid Port Format!");
            }
        }
    }
}
