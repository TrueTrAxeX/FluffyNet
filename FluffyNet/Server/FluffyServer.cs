using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using FluffyNet.Client;
using FluffyNet.Packets;

namespace FluffyNet.Server
{
    public class FluffyServer
    {
        public int Port { get; }
        
        public TimeSpan MaxWaitPingTime { get; }
        public int MaxReceivePacketLength { get; }
        public bool MessageEncryptionEnabled { get; }

        private readonly TcpListener _listener;

        public readonly object ConnectedClientsLocker = new object();

        public event Action<FluffyClient> StoreInit;
        
        public readonly List<FluffyClient> ConnectedClients = new List<FluffyClient>();

        // Будет срабатвыать, если пришел новый пакет
        public event PacketDelegate<Packet> NewPacket;
        
        /// <summary>
        /// Добавляет клиента в список
        /// </summary>
        /// <param name="client">Подключенный клиент</param>
        public void AddConnectedClient(FluffyClient client)
        {
            lock(ConnectedClientsLocker)
                ConnectedClients.Add(client);
        }

        /// <summary>
        /// Удаляет клиента из списка
        /// </summary>
        /// <param name="client">Подключенный клиент</param>
        public void RemoveConnectedClient(FluffyClient client)
        {
            lock(ConnectedClientsLocker)
                ConnectedClients.Remove(client);   
        }
        
        /// <summary>
        /// Удаляет пользователя с сервера
        /// </summary>
        /// <param name="client"></param>
        public void Kick(FluffyClient client)
        {
            client.CloseConnect();
        }

        public FluffyServer(int port, 
            int receiveBuffferSize = 2048, 
            TimeSpan? maxWaitPingTime = null, 
            int maxReceivePacketLength = 2400000,
            bool messageEncryptionEnabled = false)
        {
            MaxReceivePacketLength = maxReceivePacketLength;

            MessageEncryptionEnabled = messageEncryptionEnabled;
            
            if (maxWaitPingTime != null)
            {
                MaxWaitPingTime = maxWaitPingTime.Value;
            }
            else
            {
                MaxWaitPingTime = TimeSpan.FromSeconds(30);
            }
            
            Port = port;
            
            _listener = new TcpListener(IPAddress.Any, port);

            _listener.Server.ReceiveBufferSize = receiveBuffferSize;
        }

        public event Action<FluffyClient, byte[]> NewData;
        public event Action<FluffyClient> NewClientConnected;
        public event Action<FluffyClient> ClientDisconnected;

        /// <summary>
        /// Асинхронный запуск сервера
        /// </summary>
        /// <returns></returns>
        public Task StartAsync()
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    if(MessageEncryptionEnabled)
                        Console.WriteLine($"Server run on {Port} with encryption channel");
                    else
                        Console.WriteLine($"Server run on {Port}");
                
                    _listener.Start();
                
                    while (true)
                    {
                        TcpClient client = _listener.AcceptTcpClient();

                        Task.Factory.StartNew(() =>
                        {
                            var fluffyClient = new FluffyClient(client, this);

                            AddConnectedClient(fluffyClient);
                        
                            StoreInit?.Invoke(fluffyClient);

                            NewClientConnected?.Invoke(fluffyClient);

                            fluffyClient.Disconnected += client1 => { ClientDisconnected?.Invoke(client1); };

                            fluffyClient.NewData += (data) =>
                            {
                                NewData?.Invoke(fluffyClient, data);
                            };

                            fluffyClient.NewPacket += (ref int id, PacketParser<Packet> parser, FluffyClient xFuffyClient) =>
                            {
                                NewPacket?.Invoke(ref id, parser, xFuffyClient);
                            };
                        });
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + " " + e.StackTrace);
                }
                
            });
        }
        
    }

}