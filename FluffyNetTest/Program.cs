using System;
using System.Threading;
using FluffyNet.Client;
using FluffyNet.Packets;
using FluffyNet.Server;

namespace FluffyNetTest
{
    internal class Program
    {
        public class Bag
        {
            public string Test { get; set; } = "Privet";
        }
        
        public static void ServerTest()
        {
            // Сервер
            FluffyServer server = new FluffyServer(
                port: 42444, 
                maxWaitPingTime: TimeSpan.FromSeconds(45), 
                messageEncryptionEnabled: false);

            server.StartAsync();
            
            Thread.Sleep(2000);
            
            server.StoreInit += client =>
            {
                client.Store = new Bag();
            };
//            
//            server.NewPacket += (ref int id, FluffyNet.Server.PacketParser<Packet> parser, FluffyClient client) =>
//            {
//                switch (id)
//                {
//                    case 1239219:
//                        TestPacket myPacket = (TestPacket) parser.Invoke(typeof(TestPacket));
//                        
//                        client.SendResponse(myPacket, new TestPacket2() {Data = new byte[234]});
//                    break;
//                }
//            };
//            
            // Клиент
            FluffyNetClient cl = new FluffyNetClient("127.0.0.1", 42444);

            
            if (cl.Connect())
            {
                Console.WriteLine("Connected");
//                int c = 0;
//            
                cl.NewData += bytes =>
                {
                    Console.WriteLine("Client New data received: " + bytes.Length);

                };
//
//                cl.Disconnected += () =>
//                {
//                    Console.WriteLine("Disconnected. Коннектимся снова");
//                    Thread.Sleep(2000);
//
//                    if (cl.Connect())
//                    {
//                        Console.WriteLine("true");
//                    }
//                };
//                    
//                int counter = 0;
//                
                while (true)
                {
                    // p = cl.SendPacketAndGetResponse<TestPacket, TestPacket2>();

                    cl.SendPacket(new MyPacket());
                    Thread.Sleep(220);
                    //Console.WriteLine(++counter + " Ответ: " + p?.Id);
                }
            }
            Console.ReadLine();
        }
        
        public static void Main(string[] args)
        {
            ServerTest();
        }
    }
}