using shared;
using System.Net.Sockets;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;

class ClientData
{
    private static readonly int heartbeatAmount = 40;

    int heartbeat;

    readonly TcpClient client;  


    public ClientData(TcpClient pClient)
    {
        heartbeat = heartbeatAmount;
        client = pClient;
    }

    public TcpClient GetClient() => client;
    public int Available => client.Available;
    public NetworkStream GetStream() => client.GetStream();
    public void SendHeartbeat() => heartbeat = heartbeatAmount;
    public void HeartbeatTick() => heartbeat--;
    public bool HeartbeatFailed() => false;//heartbeat < 0;
    public TcpClient GetRawClient() => client;
}

