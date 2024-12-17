using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Common;
using Newtonsoft.Json;

namespace Client
{
    internal class Program
    {
        private static TcpClient client;
        private static IPAddress IPAddress;
        private static Guid Token;
        static void Main(string[] args)
        {
            takeIp:
            {
                Console.WriteLine("Напиши Ip:");
                string adress = Console.ReadLine();
                if (!IPAddress.TryParse(adress, out IPAddress))
                {
                    Console.WriteLine("Некорректный формат Ip!");
                    goto takeIp;
                }
            }
            int Port;
            takePort:
            {
                Console.WriteLine("Напиши порт:");
                string port = Console.ReadLine();
                if (!int.TryParse(port, out Port))
                {
                    Console.WriteLine("Некорректный формат!");
                    goto takePort;
                }
            }
            client = new TcpClient();
            client.Connect(IPAddress, Port);

            var stream = client.GetStream();

            byte[] data = new byte[1024];
            int bytes = stream.Read(data, 0, data.Length);
            string message = Encoding.UTF8.GetString(data, 0, bytes);

            Console.WriteLine(message);

            while (client.Connected)
            {
                Task.Run(() => ClientWork());
            }
            Console.WriteLine("Вы отключены");

        }
        private static async Task ClientWork()
        {
            var stream = client.GetStream();
            string commandText = Console.ReadLine();

            Command command = new Command()
            {
                Message = commandText,
                id = Token
            };

            byte[] data = new byte[1024];
            data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(command));
            await stream.WriteAsync(data, 0, data.Length);

            byte[] receive = new byte[10000];
            int bytes = stream.Read(receive, 0, receive.Length);
            string message = Encoding.UTF8.GetString(receive, 0, bytes);

            if (commandText.StartsWith("/register"))
            {

                if (message.Contains("token:"))
                {
                    message = message.Replace("token:", "");
                    Token = Guid.Parse(message);
                }
                Console.WriteLine($"{message}");
            }
            else if (commandText.StartsWith("/auth"))
            {
                if (message.Contains("token:"))
                {
                    message = message.Replace("token:", "");
                    Token = Guid.Parse(message);
                }

                Console.WriteLine($"{message}");
            }
            else if (commandText.StartsWith("/gettoken"))
            {
                if (message.Contains("token:"))
                {
                    message = message.Replace("token:", "");
                    Token = Guid.Parse(message);
                }

                Console.WriteLine($"{message}");
            }
            else if (commandText.StartsWith("/getinfo"))
            {
                Console.Write($"{message}\n");
            }
            else if (commandText.StartsWith("/updatekey"))
            {
                if (message.Contains("token:"))
                {
                    message = message.Replace("token:", "");
                    Token = Guid.Parse(message);
                }

                Console.WriteLine($"{message}");
            }
            else if (commandText.StartsWith("/disconnect"))
            {
                client.Close();
            }
        }
    }
}
