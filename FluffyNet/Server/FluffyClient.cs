using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluffyNet.Client;
using FluffyNet.Crypto;
using FluffyNet.Packets;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace FluffyNet.Server
{
    public delegate U PacketParser<U>(Type t);

    public delegate void PacketDelegate<T>(ref int packetId, PacketParser<T> parser, FluffyClient client) where T : Packet;

    public class FluffyClient
    {
        public event Action<FluffyClient> Disconnected;

        private FluffyServer _server;
        
        private static readonly object Locker = new object();
        
        public TcpClient TcpClient { get; set; }

        public object Store { get; set; }
        
        public event Action<byte[]> NewData;

        public int ReceiveBufferLength = 4096;
        
        private const int HeaderLength = 6;

        private byte[] Aes256Key { get; set; } = null;
        
        // Будет срабатвыать, если пришел новый пакет
        public event PacketDelegate<Packet> NewPacket;
        
        public FluffyClient(TcpClient client, FluffyServer server)
        {
            _server = server;
            
            TcpClient = client;

            Task.Factory.StartNew(() =>
            {
                ReceiveDataStart(ReceiveBufferLength);
            });
            
            _disconnectTimer = new System.Timers.Timer()
            {
                AutoReset = false,
                Interval = server.MaxWaitPingTime.TotalMilliseconds
            };
            
            NewPacket += (ref int id, PacketParser<Packet> parser, FluffyClient fluffyClient) =>
            {
                switch (id)
                {
                    case -42142:
                        
                        var pk = (ConnectionRequest) parser.Invoke(typeof(ConnectionRequest));
                        
                        ConnectionConfig cc = new ConnectionConfig()
                        {
                            CryptoEnabled = server.MessageEncryptionEnabled
                        };
        
                        SendResponse(pk, cc);
                        break;
                    
                    case -122030:

                        var packet = (RsaHandshakeRequest) parser.Invoke(typeof(RsaHandshakeRequest));

                        var rsaServerSide = new RsaServerSide(((RsaHandshakeRequest) packet).PublicKey);

                        this.SendPacket(new RsaHandshakeResponse()
                        {
                            EncryptedData = rsaServerSide.EncryptedText,
                            UniqueId = packet.UniqueId
                        });
                
                        this.Aes256Key = rsaServerSide.GeneratedAeskey;
                        
                        break;
                }
                
            };
            
        }
        
        private static readonly byte[] PingData = new byte[] {15, 255, 11, 17};
        private static readonly byte[] PongData = new byte[] {15, 255, 11, 18};
        
        private static bool ByteArrayCompare(byte[] a1, byte[] a2) 
        {
            return StructuralComparisons.StructuralEqualityComparer.Equals(a1, a2);
        }

        private readonly System.Timers.Timer _disconnectTimer;
        
        private void ReceiveDataStart(int receiveBufferLength)
        {
            try
            {
                _disconnectTimer.Stop();
                _disconnectTimer.Start();
                
                _disconnectTimer.Elapsed += (sender, args) =>
                {
                    Console.WriteLine("Client disconnected by timeout");
                    CloseConnect();
                };

                using (NetworkStream stream = TcpClient.GetStream())
                {
                    while (TcpClient.Connected)
                    {
                        if (TcpClient.Available < HeaderLength)
                        {
                            Thread.Sleep(5);
                            continue;
                        }
                        
                        int messageLength = ReceiveHeaderData(stream);

                        if (messageLength > _server.MaxReceivePacketLength)
                        {
                            CloseConnect();
                            return;
                        }
                        
                        int remaining = messageLength;

                        byte[] finalDataBuffer = new byte[messageLength];

                        int index = 0;
                        
                        while (remaining > 0)
                        {
                            if (remaining < receiveBufferLength) receiveBufferLength = remaining;

                            while (TcpClient.Available < receiveBufferLength)
                            {
                                Thread.Sleep(5);
                            }

                            byte[] buffer = new byte[receiveBufferLength];

                            stream.Read(buffer, 0, receiveBufferLength);

                            for(int i=0; i<buffer.Length; i++)
                            {
                                finalDataBuffer[index++] = buffer[i];
                            }
                          
                            remaining -= receiveBufferLength;
                        }

                        if (Aes256Key != null)
                        {
                            finalDataBuffer = MyAes.DecryptBytes(finalDataBuffer, Aes256Key, MyAes.Iv);
                        }
                        
                        Console.WriteLine("Readed from client bytes length " + (finalDataBuffer.Length));
                        
                        if(ByteArrayCompare(finalDataBuffer, PingData))
                        {
                            Send(PongData);
                            
                            _disconnectTimer.Stop();
                            _disconnectTimer.Start();
                            
                            Console.WriteLine("Ping data received " + (messageLength+HeaderLength) + " bytes. Pong data sent");
                        }
                        else
                        {
                            NewData?.Invoke(finalDataBuffer);
                            
                            try
                            {
                                object packet = null;
                    
                                int packetId = BitConverter.ToInt32(finalDataBuffer.Take(4).ToArray(), 0);

                                if (_server.MessageEncryptionEnabled)
                                {
                                    if (this.Aes256Key == null)
                                    {
                                        if (packetId != -122030 && packetId != -42142)
                                        {
                                            Console.WriteLine("Пришел пакет, который не ожидался");
                                            continue;
                                        }
                                    }
                                }
                                
                                NewPacket?.Invoke(ref packetId, (t) =>
                                {
                                    using (MemoryStream ms = new MemoryStream(finalDataBuffer.Skip(4).ToArray()))
                                    using (BsonReader reader = new BsonReader(ms))
                                    {
                                        JsonSerializer serializer = new JsonSerializer();

                                        packet = serializer.Deserialize(reader, t);
                                    }

                                    return (Packet) packet;
                                }, this);
                    
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.Message + " " + e.StackTrace);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Debug
                Console.WriteLine(e.Message + " " + e.StackTrace);
            }
            finally
            {
                Disconnected?.Invoke(this);

                CloseConnect();
            }
        }

        /// <summary>
        /// Отправляет ответ клиенту на присланный запрос
        /// </summary>
        /// <param name="requestPacket">Пакет запроса от клиента</param>
        /// <param name="responsePacket">Ответный пакет, который должен генерировать сервер</param>
        public void SendResponse(Packet requestPacket, Packet responsePacket)
        {
            responsePacket.UniqueId = requestPacket.UniqueId;

            SendPacket(responsePacket);
        }
        
        /// <summary>
        /// Отправляет пакет на сервер
        /// </summary>
        /// <param name="packet"></param>
        public void SendPacket(Packet packet)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BsonWriter writer = new BsonWriter(ms))
            {
                JsonSerializer serializer = new JsonSerializer();
                    
                serializer.Serialize(writer, packet);
                
                Send(BitConverter.GetBytes(packet.Id).Concat(ms.ToArray()).ToArray());
            }
        }
        
        /// <summary>
        /// Отправляет сырые данные на сокет
        /// </summary>
        /// <param name="rawData">Последовательность байт</param>
        /// <summary>
        /// Отправляет сырые данные на сокет
        /// </summary>
        /// <param name="rawData">Последовательность байт</param>
        public void Send(byte[] rawData)
        {
            lock (Locker)
            {
                byte[] identityPacketData = new byte[]
                {
                    (byte) 196, (byte) 12
                };
                
                if (Aes256Key != null)
                {
                    rawData = MyAes.EncryptBytes(rawData, Aes256Key, MyAes.Iv);
                }
                
                byte[] lengthBytes = BitConverter.GetBytes(rawData.Length);

                TcpClient.GetStream().Write(identityPacketData, 0, identityPacketData.Length);
                
                TcpClient.GetStream().Write(lengthBytes, 0, lengthBytes.Length);
                
                // Если работает шифрование, то шифруем все сообщение
                
                TcpClient.GetStream().Write(rawData, 0, rawData.Length);

                TcpClient.GetStream().Flush();
            }
        }

        /// <summary>
        /// Закрывает соединение с сервером
        /// </summary>
        public void CloseConnect()
        {
            try
            {
                TcpClient.GetStream().Close();
                TcpClient.Close();
            }
            finally
            {
                lock (_server.ConnectedClientsLocker)
                {
                    _server.ConnectedClients.Remove(this);
                }    
            }
            
        }
        
        /// <summary>
        /// Читает заголовок посылаемых данных
        /// </summary>
        /// <param name="stream">Стрим</param>
        /// <returns>Заголовок, в котором содержится длина данных, которые будут поступать после</returns>
        private int ReceiveHeaderData(NetworkStream stream)
        {
            byte b1 = (byte) stream.ReadByte();

            bool flag = false;
            
            if (b1 == 196)
            {
                byte b2 = (byte) stream.ReadByte();
                
                if (b2 == 12)
                {
                    flag = true;
                }
            }

            if (flag == false) return -1;
            
            byte[] buffer = new byte[4];

            int bytes = stream.Read(buffer, 0, buffer.Length);

            if (bytes == 4)
            {
                return BitConverter.ToInt32(buffer, 0);
            }
            else
            {
                return -1;
            }
        }
    }
}