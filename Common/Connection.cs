using System;
using System.Net;
using System.Net.Sockets;
using System.Timers;

namespace Common
{
    public class Connection
    {
        public TcpClient Client;
        public User User { get; set; }
        public Guid Token { get; set; } = Guid.NewGuid();
        private Timer Timer;
        public DateTime ConnectionTime { get; set; } = DateTime.Now;
        public Connection(TcpClient client, int disconnectTime) 
        {
            Client = client;
            Timer = new Timer();
            Timer.Interval = TimeSpan.FromSeconds(disconnectTime).TotalMilliseconds;
            Timer.AutoReset = false;
            Timer.Elapsed += OnTimerElapsed;
            Timer.Start();
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Disconnect();
        }

        public void Disconnect()
        {
            Timer.Stop();
            Client.Close();
        }

        public override string ToString()
        {
            return $"Дата и время: {ConnectionTime}, Время подключения:{DateTime.Now - ConnectionTime}, Адрес и порт: {IPAddress.Parse(((IPEndPoint)Client.Client.RemoteEndPoint).Address.ToString())}:{((IPEndPoint)Client.Client.RemoteEndPoint).Port.ToString()}";
        }
    }
}
