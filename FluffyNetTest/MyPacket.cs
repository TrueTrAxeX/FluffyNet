using FluffyNet.Packets;
using Newtonsoft.Json;

namespace FluffyNetTest
{
    public class MyPacket : Packet
    {
        [JsonProperty(PropertyName = "suka")]
        public string Suka = "pizdec";
        
        public MyPacket()
        {
            Id = 100000;
        }
    }
}