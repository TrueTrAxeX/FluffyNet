using Newtonsoft.Json;

namespace FluffyNet.Packets
{
    public class RsaHandshakeRequest : Packet
    {
        public RsaHandshakeRequest()
        {
            Id = (int) -122030;
        }
        
        [JsonProperty(PropertyName = "pk")]
        public string PublicKey { get; set; }
    }
}