namespace shared{
    public class DataUpdateMessage : ISerializable {
        public GameData data {  get; private set; }

        public DataUpdateMessage() { }
        public DataUpdateMessage(GameData data) { 
            this.data = data;
        }

        public void Serialize(Packet pPacket) {
            pPacket.Write(data);
        }

        public void Deserialize(Packet pPacket) {
            data = (GameData)pPacket.ReadObject();
        }
    }
}
