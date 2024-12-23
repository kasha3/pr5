﻿using Common;
using Newtonsoft.Json;
using Server.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    public class Program
    {
        private static int MaxClients = 5;
        private static int DisconnectIntervalSeconds = 3600;
        private static int Port;
        public static ApplicationContext db;
        public static IPAddress IPAddress;
        public static List<Connection> ActiveUsers = new List<Connection>();
        public static TcpListener ServerListener;

        private static void Main(string[] args)
        {
            db = new ApplicationContext(new Microsoft.EntityFrameworkCore.DbContextOptions<ApplicationContext>());
            IPAddress = GetIpAddress();
            Port = GetPort();
            MaxClients = GetMaxClients();
            DisconnectIntervalSeconds = GetDisconnectTime();
            ServerListener = new TcpListener(IPAddress, Port);
            ServerListener.Start();
            Console.WriteLine($"IP: {IPAddress}, Port: {Port}, Max Clients: {MaxClients}, Disconnect Time: {DisconnectIntervalSeconds} seconds");
            Thread commandThread = new Thread(ServerCommands);
            commandThread.Start();

            while (true)
            {
                TcpClient client = ServerListener.AcceptTcpClient();
                Console.WriteLine("Client Connected: " + client.Client.RemoteEndPoint);
                NetworkStream stream = client.GetStream();
                byte[] data;
                if (ActiveUsers.Count >= MaxClients)
                {
                    Console.WriteLine("Maximum number of clients reached...");
                    data = Encoding.UTF8.GetBytes("Error: Maximum number of clients reached.");
                    stream.Write(data, 0, data.Length);
                    client.Close();
                    continue;
                }
                data = Encoding.UTF8.GetBytes("Connected.");
                stream.Write(data, 0, data.Length);
                Task.Run(() => HandleClient(client));
            }
        }

        public static void ServerCommands()
        {
            try
            {
                while (true)
                {
                    string command = Console.ReadLine();
                    if (command == "/status")
                    {
                        Console.WriteLine("Server status: OK");
                    }
                    else if (command == "/help")
                    {
                        Console.WriteLine("List of commands: \n/status - Server status\n/kick - For kick user\n/blacklist - For display blacklist users\n/blacklist 'Login' - For add user to blacklist\n ");
                    }
                    else if (command == "/list")
                    {
                        Console.WriteLine($"Online users: {ActiveUsers.Count}");
                        for (int i = 0; i < ActiveUsers.Count; i++)
                        {
                            Console.WriteLine($"{i}: {ActiveUsers[i]}");
                        }
                    }
                    else if (command.StartsWith("/kick"))
                    {
                        if (command.Length < 6)
                        {
                            Console.WriteLine("Using /kick with token!");
                        }
                        else
                        {
                            string token = command.Split(' ')[1];
                            if (Guid.TryParse(token, out Guid guidToken))
                            {
                                var connection = ActiveUsers.FirstOrDefault(c => c.Token == guidToken);
                                if (connection != null)
                                {
                                    connection.Disconnect();
                                    ActiveUsers.Remove(connection);
                                    Console.WriteLine("User has been disconnected");
                                }
                                else
                                {
                                    Console.WriteLine("User with specified token not found.");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Invalid token format.");
                            }
                        }
                    }
                    else if (command.StartsWith("/blacklist"))
                    {
                        string[] args = command.Split(' ');
                        if (args.Length == 1)
                        {
                            var blacklistUsers = db.Users.Where(u => u.IsBlocked == true).ToList();
                            if (blacklistUsers.Any())
                            {
                                Console.WriteLine("Blacklisted users:");
                                foreach (var user in blacklistUsers)
                                {
                                    Console.WriteLine($"Login: {user.Login}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("No users in the blacklist.");
                            }
                        }
                        else if (args.Length == 2)
                        {
                            string login = args[1];
                            var user = db.Users.FirstOrDefault(u => u.Login == login);
                            if (user != null)
                            {
                                user.IsBlocked = true;
                                db.Update(user);
                                db.SaveChanges();
                                var activeUser = ActiveUsers.FirstOrDefault(u => u.User.Login == login);
                                if (activeUser != null)
                                {
                                    activeUser.Disconnect();
                                    ActiveUsers.Remove(activeUser);
                                }
                                Console.WriteLine($"User {user.Login} has been disconnected and added to blacklist");
                            }
                            else
                            {
                                Console.WriteLine($"User with login '{login}' not found.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid command format. Use /blacklist or /blacklist login.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex}");
            }
        }

        private static async Task HandleClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                while (client.Connected)
                {
                    byte[] data = new byte[1024];
                    int bytesRead = 0;

                    try
                    {
                        bytesRead = await stream.ReadAsync(data, 0, data.Length);
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"Error reading from client: {ex.Message}");
                        break;
                    }

                    if (bytesRead == 0)
                    {
                        Console.WriteLine("Client disconnected.");
                        break;
                    }

                    string message = Encoding.UTF8.GetString(data, 0, bytesRead).ToLower();
                    Console.WriteLine($"User {client.Client.RemoteEndPoint.ToString()} sent: " + message);

                    Command command = JsonConvert.DeserializeObject<Command>(message);
                    if (command != null && command.Message != null)
                    {
                        command.Message = command.Message.ToLower();

                        if (command.Message.StartsWith("/register"))
                        {
                            string[] userdata = command.Message.Split(' ');
                            if (userdata.Length == 3)
                            {
                                var newUser = new User
                                {
                                    Login = userdata[1],
                                    Password = userdata[2],
                                };
                                db.Users.Add(newUser);
                                await db.SaveChangesAsync();
                                var connection = new Connection(client, DisconnectIntervalSeconds)
                                {
                                    User = newUser,
                                };
                                ActiveUsers.Add(connection);
                                string response = $"Token: {connection.Token}";
                                byte[] responseData = Encoding.UTF8.GetBytes(response);
                                await stream.WriteAsync(responseData, 0, responseData.Length);
                            }
                            else
                            {
                                Console.WriteLine("Invalid registration data.");
                            }
                        }
                        else if (command.Message.StartsWith("/auth"))
                        {
                            string[] userdata = command.Message.Split(' ');
                            if (userdata.Length == 3)
                            {
                                User user = db.Users.FirstOrDefault(x => x.Login == userdata[1] && x.Password == userdata[2]);
                                if (user != null)
                                {
                                    if (!user.IsBlocked)
                                    {
                                        var connection = new Connection(client, DisconnectIntervalSeconds)
                                        {
                                            User = user
                                        };
                                        ActiveUsers.Add(connection);
                                        string response = $"Token: {connection.Token}";
                                        byte[] responseData = Encoding.UTF8.GetBytes(response);
                                        await stream.WriteAsync(responseData, 0, responseData.Length);
                                    }
                                    else
                                    {
                                        string response = "You are in the blacklist.";
                                        byte[] responseData = Encoding.UTF8.GetBytes(response);
                                        await stream.WriteAsync(responseData, 0, responseData.Length);
                                        client.Close();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("Invalid login or password");
                                }
                            }
                        }
                        else if (command.Message.StartsWith("/gettoken"))
                        {
                            var connection = ActiveUsers.FirstOrDefault(x => x.Token == command.id);
                            if (connection != null)
                            {
                                string response = $"Token: {connection.Token}";
                                byte[] responseData = Encoding.UTF8.GetBytes(response);
                                await stream.WriteAsync(responseData, 0, responseData.Length);
                            }
                            else
                            {
                                Console.WriteLine("Connection not found.");
                            }
                        }
                        else if (command.Message.StartsWith("/getinfo"))
                        {
                            var connection = ActiveUsers.FirstOrDefault(x => x.Token == command.id);
                            if (connection != null)
                            {
                                string response = $"Info: {connection}";
                                byte[] responseData = Encoding.UTF8.GetBytes(response);
                                await stream.WriteAsync(responseData, 0, responseData.Length);
                            }
                            else
                            {
                                Console.WriteLine("Connection not found.");
                            }
                        }
                        else if (command.Message.StartsWith("/updatekey"))
                        {
                            var connection = ActiveUsers.FirstOrDefault(x => x.Token == command.id);
                            if (connection != null)
                            {
                                connection.Token = Guid.NewGuid();
                                string response = $"Token: {connection.Token}";
                                byte[] responseData = Encoding.UTF8.GetBytes(response);
                                await stream.WriteAsync(responseData, 0, responseData.Length);
                            }
                            else
                            {
                                Console.WriteLine("Connection not found.");
                            }
                        }
                        else if (command.Message.StartsWith("/disconnect"))
                        {
                            var connection = ActiveUsers.FirstOrDefault(x => x.Token == command.id);
                            if (connection != null)
                            {
                                connection.Disconnect();
                                ActiveUsers.Remove(connection);
                                string response = "Disconnected.";
                                byte[] responseData = Encoding.UTF8.GetBytes(response);
                                await stream.WriteAsync(responseData, 0, responseData.Length);
                            }
                            else
                            {
                                Console.WriteLine("Connection not found.");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid command or message is null.");
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine($"User has been disconnected");
            }
            finally
            {
                var connection = ActiveUsers.FirstOrDefault(x => x.Client == client);
                if (connection != null)
                {
                    connection.Disconnect();
                    ActiveUsers.Remove(connection);
                }
                client.Close();
            }
        }

        static IPAddress GetIpAddress()
        {
            while (true)
            {
                Console.Write("Ip: ");
                string address = Console.ReadLine();
                if (IPAddress.TryParse(address, out IPAddress ipAddress))
                {
                    return ipAddress;
                }
                Console.WriteLine("Invalid IP Format!");
            }
        }

        static int GetPort()
        {
            while (true)
            {
                Console.Write("Port: ");
                string port = Console.ReadLine();
                if (int.TryParse(port, out int portNumber) && portNumber > 0)
                {
                    return portNumber;
                }
                Console.WriteLine("Invalid Port Format!");
            }
        }

        static int GetMaxClients()
        {
            while (true)
            {
                Console.Write("Max Clients: ");
                string maxClients = Console.ReadLine();
                if (int.TryParse(maxClients, out int maxClientsNumber) && maxClientsNumber > 0)
                {
                    return maxClientsNumber;
                }
                Console.WriteLine("Invalid Format!");
            }
        }

        static int GetDisconnectTime()
        {
            while (true)
            {
                Console.Write("Disconnect Time: ");
                string disconnectTime = Console.ReadLine();
                if (int.TryParse(disconnectTime, out int disconnectIntervalSeconds) && disconnectIntervalSeconds > 0)
                {
                    return disconnectIntervalSeconds;
                }
                Console.WriteLine("Invalid Format!");
            }
        }
    }
}
