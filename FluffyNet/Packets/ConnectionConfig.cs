using Newtonsoft.Json;

namespace FluffyNet.Packets
{
    public class ConnectionConfig : Packet
    {
        public ConnectionConfig()
        {
            Id = -2991299;
        }

        [JsonProperty(PropertyName = "crypto_enabled")]
        public bool CryptoEnabled { get; set; } = false;
    }
}