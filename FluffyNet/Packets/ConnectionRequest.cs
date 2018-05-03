using System.Data.Common;

namespace FluffyNet.Packets
{
    public class ConnectionRequest : Packet
    {
        public ConnectionRequest()
        {
            Id = (int) -42142;
        }
    }
}