using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using FluffyNet.Crypto;
using FluffyNet.Packets;


namespace FluffyNet.Client
{
    public class DataReceiveService
    {
        private TcpClient _client;
        private FluffyNetClient _fluffyNetClient;

        public event Action<byte[]> NewData;
        public event Action<FluffyNetClient> Disconnected;
        
        private const int HeaderLength = 6;
       
        public DataReceiveService(TcpClient client, FluffyNetClient fluffyNetClient)
        {
            _fluffyNetClient = fluffyNetClient;
            _client = client;
        }

        private static readonly byte[] PingData = new byte[] {15, 255, 11, 17};
        private static readonly byte[] PongData = new byte[] {15, 255, 11, 18};

        private static bool ByteArrayCompare(byte[] a1, byte[] a2) 
        {
            return StructuralComparisons.StructuralEqualityComparer.Equals(a1, a2);
        }

        public void Run(int receiveBufferLength = 4096)
        {
            try
            {
                using (NetworkStream stream = _client.GetStream())
                {
                    while (_client.Connected)
                    {
                        if (_client.Available < HeaderLength)
                        {
                            Thread.Sleep(5);
                            continue;
                        }
                        
                        int messageLength = ReceiveHeaderData(stream);

                        int remaining = messageLength;
                        
                        byte[] finalDataBuffer = new byte[messageLength];
                       
                        int index = 0;

                        while (remaining > 0)
                        {
                            if (remaining < receiveBufferLength) receiveBufferLength = remaining;

                            while (_client.Available < receiveBufferLength)
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
                        
                        if (_fluffyNetClient.Aes256Key != null)
                        {
                            finalDataBuffer = MyAes.DecryptBytes(finalDataBuffer, _fluffyNetClient.Aes256Key, MyAes.Iv);
                        }

                        Console.WriteLine("Readed data from socket " + finalDataBuffer.Length + " bytes");
                        
                        if (!ByteArrayCompare(PongData, finalDataBuffer))
                        {
                            NewData?.Invoke(finalDataBuffer);
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
                _fluffyNetClient.CloseConnect();
                
                Disconnected?.Invoke(_fluffyNetClient);
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