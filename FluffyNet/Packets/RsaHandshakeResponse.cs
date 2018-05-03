using Newtonsoft.Json;

namespace FluffyNet.Packets
{
    public class RsaHandshakeResponse : Packet
    {
        public RsaHandshakeResponse()
        {
            Id = (int) -122031;
        }
        
        [JsonProperty(PropertyName = "EncryptedData")]
        public string EncryptedData { get; set; }
    }
}