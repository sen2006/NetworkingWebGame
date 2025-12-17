using System;
using System.Collections.Generic;
using System.Text;

namespace shared {
    public class ButtonClickMessage : ISerializable{
        public void Deserialize(Packet pPacket) { }

        public bool HasReturnMessage() {
            return false;
        }

        public void Serialize(Packet pPacket) { }
    }
}
