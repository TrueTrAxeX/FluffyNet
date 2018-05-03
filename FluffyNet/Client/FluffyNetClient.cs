using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluffyNet.Crypto;
using FluffyNet.Packets;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;


namespace FluffyNet.Client
{    
    public delegate U PacketParser<U>(Type t);

    public delegate void PacketDelegate<T>(ref int packetId, PacketParser<T> parser, FluffyNetClient client) where T : Packet;
    
    public class FluffyNetClient
    {
        private readonly object Locker = new object();
        
        private int _port;
        private string _address;
        private TcpClient _client;
        
        public TcpClient TcpClient => _client;

        // Будет срабатывать если пришли новая информация в байтах
        public event Action<byte[]> NewData;

        // Будет срабатвыать, если пришел новый пакет
        public event PacketDelegate<Packet> NewPacket;
        
        public event Action Disconnected;
    
        private DataReceiveService _drs;

        public byte[] Aes256Key { get; set; } = null;
        
        public FluffyNetClient(string address, int port)
        {
            _port = port;
            _address = address;
        }
        
        private int _packetSequence = 0;

        /// <summary>
        /// Ожидание указанного пакета
        /// </summary>
        /// <param name="timeout">Таймаут</param>
        /// <param name="withUniqueId">Если указать, то будет ждать пакет с уникальным ID</param>
        /// <typeparam name="U">Тип ожидаемого пакета</typeparam>
        /// <returns></returns>
        public U WaitPacket<U>(TimeSpan? timeout = null, int? withUniqueId = null) where U : Packet
        {
            if (timeout == null) timeout = TimeSpan.FromSeconds(20);
            
            U data = null;

            var u = (U) Activator.CreateInstance(typeof(U));
            
            this.NewPacket += (ref int id, PacketParser<Packet> parser, FluffyNetClient xFluffyNetClient) =>
            {
                if (id == u.Id)
                {
                    var _data = (U) parser.Invoke(typeof(U));

                    if (withUniqueId == null)
                        data = _data;
                    else
                    {
                        if (withUniqueId.Equals(_data.UniqueId))
                        {
                            data = _data;
                        }
                    }
                }
            };
            
            DateTime startDate = DateTime.Now;
            
            while (data == null)
            {
                Thread.Sleep(1);

                if (startDate + timeout < DateTime.Now)
                {
                    return null;
                }
            }

            return data;
        }
        
        /// <summary>
        /// Посылает пакет и ждет ответа от сервера
        /// </summary>
        /// <param name="packet">Посылаемый пакет</param>
        /// <param name="timeout">Таймаут ожидаения ответа</param>
        /// <typeparam name="T">Тип пакета запроса</typeparam>
        /// <typeparam name="U">Тип пакета ответа</typeparam>
        /// <returns>Ответ от сервера</returns>
        public U SendPacketAndGetResponse<T, U>(T packet, TimeSpan? timeout = null) 
            where T : Packet
            where U : Packet
        {
            if (timeout == null) timeout = TimeSpan.FromSeconds(20);
        
            packet.UniqueId = ++_packetSequence;

            U data = null;

            var u = (U) Activator.CreateInstance(typeof(U));
        
            this.NewPacket += (ref int id, PacketParser<Packet> parser, FluffyNetClient xFluffyNetClient) =>
            {
                if (id == u.Id)
                {
                    var _data = (U) parser.Invoke(typeof(U));

                    if (_data.UniqueId == packet.UniqueId)
                    {
                        data = _data;
                    }
                }
            };
        
            this.SendPacket(packet);

            DateTime startDate = DateTime.Now;
        
            while (data == null)
            {
                Thread.Sleep(1);

                if (startDate + timeout < DateTime.Now)
                {
                    return null;
                }
            }

            return data;
      
        }

        
        public void CloseConnect()
        {
            lock (Locker)
            {
                if (_client?.Connected == false) return;
                
                try
                {
                    Console.WriteLine("Connection closed ");
                    _client?.Close();
                    _client?.Client.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + " " + e.StackTrace);
                }
            }
        }
        
        public bool Connect()
        {
            if (_client?.Connected == true) return true;

            try
            {
                _client = new TcpClient();
                _client.Connect(_address, _port);
                
                _drs = new DataReceiveService(_client, this);


                Task.Factory.StartNew(() =>
                {
                    _drs.Disconnected += client =>
                    {
                        Disconnected?.Invoke();
                    };
                    
                    _drs.NewData += (data) =>
                    {
                        NewData?.Invoke(data);

                        try
                        {
                            object packet = null;
                        
                            int packetId = BitConverter.ToInt32(data.Take(4).ToArray(), 0);
                                
                            NewPacket?.Invoke(ref packetId, (t) =>
                            {
                                using (MemoryStream ms = new MemoryStream(data.Skip(4).ToArray()))
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
                    };
                    
                    _drs.Run();
                });
        
                var waitPacket = SendPacketAndGetResponse<ConnectionRequest, ConnectionConfig>(new ConnectionRequest());

                if (waitPacket?.CryptoEnabled == true)
                {
                    RsaClientSide rsaClientSide = new RsaClientSide();
                        
                    var publicKey = rsaClientSide.PublicKeyString;

                    var rsaHandshakeResponse =
                        SendPacketAndGetResponse<RsaHandshakeRequest, RsaHandshakeResponse>(
                            new RsaHandshakeRequest() {PublicKey = publicKey});
                        
                    if (rsaHandshakeResponse == null)
                    {
                        throw new Exception();
                    }
                        
                    var decryptedData = rsaClientSide.Step2(rsaHandshakeResponse.EncryptedData);

                    Console.WriteLine($"Receive decrypted AES-256 Key: {Convert.ToBase64String(decryptedData)}");

                    Aes256Key = decryptedData;
                    
                    var pinger = new PingService(this);
                    pinger.StartAsync();
                }
                else
                {
                    var pinger = new PingService(this);
                    pinger.StartAsync();
                }
            }
            catch (Exception e)
            {
                return false;
            }
            
            return true;
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
    }
}