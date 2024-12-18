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
        private static int Port;
        private static IPAddress IPAddress;
        private static Guid Token = Guid.Empty;
        private static NetworkStream stream;

        static async Task Main(string[] args)
        {
            await ConnectToServer();
        }

        private static async Task ConnectToServer()
        {
            while (true)
            {
                Console.WriteLine("Введите IP:");
                string address = Console.ReadLine();
                if (IPAddress.TryParse(address, out IPAddress))
                    break;
                Console.WriteLine("Некорректный формат IP!");
            }
            int port;
            while (true)
            {
                Console.WriteLine("Введите порт:");
                string portInput = Console.ReadLine();
                if (int.TryParse(portInput, out port))
                    break;
                Console.WriteLine("Некорректный формат порта!");
            }

            try
            {
                client = new TcpClient();
                await client.ConnectAsync(IPAddress, port);
                Console.WriteLine("Успешно подключен к серверу.");
                stream = client.GetStream();

                byte[] data = new byte[1024];
                int bytes = await stream.ReadAsync(data, 0, data.Length);
                string message = Encoding.UTF8.GetString(data, 0, bytes);
                Console.WriteLine(message);

                while (client.Connected)
                {
                    await ClientWork();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка подключения: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Соединение разорвано.");
            }
        }

        private static async Task ClientWork()
        {
            Console.WriteLine("Введите команду:");
            string commandText = Console.ReadLine();

            // Проверяем, если команда требует регистрации или авторизации,
            // то токен будет установлен после получения от сервера.
            Command command = new Command()
            {
                Message = commandText,
                id = Token  // Передаем актуальный токен
            };

            // Отправляем команду на сервер
            string jsonCommand = JsonConvert.SerializeObject(command);
            byte[] data = Encoding.UTF8.GetBytes(jsonCommand);
            await stream.WriteAsync(data, 0, data.Length);

            // Получаем ответ от сервера
            byte[] receive = new byte[10000];
            int bytes = await stream.ReadAsync(receive, 0, receive.Length);
            string message = Encoding.UTF8.GetString(receive, 0, bytes);

            // Если это команда на регистрацию или авторизацию,
            // токен обновляется.
            if (commandText.StartsWith("/register") || commandText.StartsWith("/auth"))
            {
                if (message.Contains("token:"))
                {
                    Token = Guid.Parse(message.Replace("token:", "").Trim());
                    Console.WriteLine($"Получен новый токен: {Token}");
                }
                Console.WriteLine($"Ответ: {message}");
            }
            // Команда для получения токена
            else if (commandText.StartsWith("/gettoken"))
            {
                if (message.Contains("token:"))
                {
                    Token = Guid.Parse(message.Replace("token:", "").Trim());
                    Console.WriteLine($"Получен новый токен: {Token}");
                }
                Console.WriteLine($"Ответ: {message}");
            }
            else
            {
                Console.WriteLine($"Ответ: {message}");
            }

            // Закрытие соединения
            if (commandText.StartsWith("/disconnect"))
            {
                client.Close();
            }
        }

    }
}
