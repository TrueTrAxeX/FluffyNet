using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace FluffyNet.Client
{
    public class PingService
    {
        private static readonly byte[] PingData = new byte[] {15, 255, 11, 17};
        private static readonly byte[] PongData = new byte[] {15, 255, 11, 18};
        
        private FluffyNetClient _fluffyNetClient;
        private int _pingInterval;
        
        private static bool ByteArrayCompare(byte[] a1, byte[] a2) 
        {
            return StructuralComparisons.StructuralEqualityComparer.Equals(a1, a2);
        }
        
        public PingService(FluffyNetClient fluffyNetClient, int pingInterval = 5000)
        {
            _fluffyNetClient = fluffyNetClient;
            _pingInterval = pingInterval;
        }

        public Task StartAsync()
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    Console.WriteLine("Ping service started");
                    
                    while (_fluffyNetClient.TcpClient.Connected)
                    {
                        Thread.Sleep(_pingInterval);

                        _fluffyNetClient.Send(PingData);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Ping service interrupted");
                    _fluffyNetClient.CloseConnect();
                }
            });
        }
    }
}