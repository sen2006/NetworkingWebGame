namespace shared
{
    // the message that gets returned to the joining client


    public class AcceptClientMessage : ISerializable
    {
        int id;


        public AcceptClientMessage() { }
        public AcceptClientMessage(int id) { this.id = id; }

        public int GetId() { return id; }
        
        public void Serialize(Packet pPacket)
        {
            pPacket.Write(id);
        }

        public void Deserialize(Packet pPacket)
        {
            id = pPacket.ReadInt();
        }
    }
}
