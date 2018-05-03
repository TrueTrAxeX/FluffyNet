using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FluffyNet.Packets
{
    [Serializable]
    public class Packet : IPacket
    {
        [JsonProperty(PropertyName = "id")]
        public int Id { get; set; }

        [JsonProperty(PropertyName = "uid")] 
        public int? UniqueId { get; set; } = null;
        
        public override bool Equals(object obj)
        {
            if (obj is Packet packet)
            {
                if (packet.Id.Equals(Id))
                {
                    return true;
                }
            }

            return false;
        }
    }
}