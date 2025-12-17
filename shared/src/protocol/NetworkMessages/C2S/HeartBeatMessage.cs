using System;
using System.Collections.Generic;
using System.Text;

namespace shared
{
    public class HeartBeatMessage : ISerializable
    {
        public HeartBeatMessage() { }
        public void Deserialize(Packet pPacket) { }
        public void Serialize(Packet pPacket) { }
    }
}
