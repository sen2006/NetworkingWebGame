

namespace shared {
    public class LoginAttemptMessage : ISerializable {
        public string password { get; private set; }

        public LoginAttemptMessage() { }
        public LoginAttemptMessage(string password) { 
            this.password = password;
        }

        public void Serialize(Packet pPacket) {
            pPacket.Write(password);
        }

        public void Deserialize(Packet pPacket) {
            password = pPacket.ReadString();
        }
    }
}
