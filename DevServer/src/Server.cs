using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Threading;
using shared;

class Server {
    enum GameState { WAITING, RUNNING, FINNISHED }

    public static TcpListener server;
    public static List<ClientConnection> clients = new List<ClientConnection>();
    static Queue<Message> messageQueue = new Queue<Message>();

    static X509Certificate2 certificate = new X509Certificate2("./server.pfx", "password");
    private static Thread commandThread = new Thread(handleConsoleCommands);

    private static int port = 55555;
    public static GameData _gameData = new GameData();
    internal static string savePath = "./save";

    public static void Main() {
        server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine($"Server started on port {port}");
        Console.WriteLine($"Cert has private key: {certificate.HasPrivateKey}");
        Console.WriteLine($"Cert subject: {certificate.Subject}");
        Console.WriteLine();

        loadSaveData(savePath);
        commandThread.Start();

        // main loop
        while (true) {
            AcceptNewClients();
            SendMessages();
            Thread.Sleep(50); // small sleep to reduce CPU usage
        }
    }

    private static void AcceptNewClients() {
        if (!server.Pending()) return;

        TcpClient tcpClient = server.AcceptTcpClient();
        Console.WriteLine("Client connected");

        SslStream sslStream = new SslStream(tcpClient.GetStream(), false);
        try {
            sslStream.AuthenticateAsServer(certificate, false, System.Security.Authentication.SslProtocols.Tls12, false);
        } catch (Exception e) {
            Console.WriteLine("SSL Authentication failed: " + e.Message);
            tcpClient.Close();
            return;
        }

        ClientConnection client = new ClientConnection(tcpClient, sslStream);
        clients.Add(client);

        Thread clientThread = new Thread(() => HandleClient(client));
        clientThread.Start();
    }

    private static async void HandleClient(ClientConnection client) {
        try {
            byte[] buffer = new byte[4096];
            while (true) {
                Console.WriteLine($"buffer: {buffer}");
                //if (client.stream.Length > 0) {
                    int bytesRead = await client.stream.ReadAsync(buffer, 0, buffer.Length);
                    Console.WriteLine($"buffer: {buffer}");
                    if (bytesRead == 0) break; // disconnected

                    byte[] data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);

                    // check if this is an initial handshake
                    string text = Encoding.UTF8.GetString(data);
                    if (Regex.IsMatch(text, "^GET", RegexOptions.IgnoreCase)) {
                        PerformWebSocketHandshake(client, text);
                        messageQueue.Enqueue(new Message(new AcceptClientMessage(1), client));
                        continue;
                    }


                    // decode WebSocket frames
                    List<byte[]> messages = WebSocketHelper.DecodeFrames(client, data);
                    foreach (byte[] msg in messages) {
                        Packet packet = new Packet(msg);
                        ISerializable obj = packet.ReadObject();
                        if (obj != null) {
                            ISerializable response = HandleMessage(obj);
                            if (response != null) {
                                messageQueue.Enqueue(new Message(response, client));
                            }
                        }
                    }
                //}
            }
        } catch (Exception) {
            // client disconnected
        } finally {
            Console.WriteLine("Client disconnected");
            clients.Remove(client);
            client.client.Close();
        }
    }

    private static void PerformWebSocketHandshake(ClientConnection client, string request) {
        Console.WriteLine("===== Handshake =====\n" + request);

        string swk = Regex.Match(request, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
        string acceptKey = Convert.ToBase64String(
            System.Security.Cryptography.SHA1.Create().ComputeHash(
                Encoding.UTF8.GetBytes(swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")
            )
        );

        string response = "HTTP/1.1 101 Switching Protocols\r\n" +
                          "Connection: Upgrade\r\n" +
                          "Upgrade: websocket\r\n" +
                          "Sec-WebSocket-Accept: " + acceptKey + "\r\n\r\n";

        byte[] respBytes = Encoding.UTF8.GetBytes(response);
        client.stream.Write(respBytes, 0, respBytes.Length);
    }

    private static ISerializable HandleMessage(ISerializable message) {
        bool lockTaken = false;
        try {
            Monitor.Enter(_gameData, ref lockTaken);

            if (message is LoginAttemptMessage loginAttempt) {
                Console.WriteLine($"Client login attempt: {loginAttempt.password}");
                if (_gameData.teamExists(loginAttempt.password)) {
                    GameTeam team = _gameData.GetTeamData(loginAttempt.password);
                    Console.WriteLine($"Team found: {team.name}");
                    return new LoginResultMessage(team, loginAttempt.password);
                }
                Console.WriteLine("Team not found");
                return new LoginResultMessage();
            } else if (message is RequestDataUpdateMessage) {
                Console.WriteLine("Data update request received");
                return new DataUpdateMessage(_gameData);
            }

            throw new Exception($"Unhandled message: {message.GetType()}");
        } finally {
            if (lockTaken) Monitor.Exit(_gameData);
        }
    }

    static private void SendMessages() {
        while (messageQueue.Count > 0) {
            Message message = messageQueue.Dequeue();
            message.Send();
        }
    }

    private static void handleConsoleCommands() {
        while (true) {
            string input = Console.ReadLine();
            bool lockTaken = false;
            try {
                Monitor.Enter(_gameData, ref lockTaken);
                ConsoleCommands.HandleCommand(input);
                CheckSaveDataDirty();
            } finally {
                if (lockTaken) Monitor.Exit(_gameData);
            }
        }
    }

    public static void loadSaveData(string path) {
        if (!File.Exists(path)) return;
        byte[] data = File.ReadAllBytes(path);
        Packet packet = new Packet(data);
        _gameData = (GameData)packet.ReadObject();
    }

    public static void saveGameData(string path) {
        Packet save = new Packet();
        save.Write(_gameData);
        _gameData.updated = false;
        File.WriteAllBytes(path, save.GetBytes());
    }

    public static void CheckSaveDataDirty() {
        if (_gameData.updated) {
            Console.WriteLine("Saving...");
            saveGameData(savePath);
            Console.WriteLine("Save complete");
        }
    }

    public static void SendMessageToClient(ClientConnection client, ISerializable msg) {

        Packet packet = new Packet();
        packet.Write(msg);
        byte[] frame = WebSocketHelper.EncodeFrame(packet.GetBytes(), true);
        client.stream.Write(frame, 0, frame.Length);
    }
}

// --- helper classes ---
public class ClientConnection {
    public TcpClient client { get; private set; }
    public SslStream stream { get; private set; }

    public ClientConnection(TcpClient c, SslStream s) { client = c; stream = s; }
}

internal class Message {
    readonly List<ClientConnection> recipients = new List<ClientConnection>();
    readonly ISerializable toSend;

    public Message(ISerializable msg, ClientConnection recipient) {
        toSend = msg;
        recipients.Add(recipient);
    }

    public Message(ISerializable msg, List<ClientConnection> recipients) {
        toSend = msg;
        this.recipients.AddRange(recipients);
    }

    public void Send() {
        foreach (ClientConnection c in recipients) {
            try {
                Server.SendMessageToClient(c, toSend);
            } catch {
                Server.clients.Remove(c);
            }
        }
    }
}

// --- WebSocket frame helper ---
public static class WebSocketHelper {
    public static List<byte[]> DecodeFrames(ClientConnection connection,byte[] bytes) {
        List<byte[]> messages = new List<byte[]>();
        int i = 0;

        while (i < bytes.Length) {
            bool fin = (bytes[i] & 0b10000000) != 0;
            int opcode = bytes[i] & 0b00001111;
            i++;

            bool mask = (bytes[i] & 0b10000000) != 0;
            ulong payloadLen = (ulong)(bytes[i] & 0b01111111);
            i++;

            if (payloadLen == 126) {
                payloadLen = (ulong)BitConverter.ToUInt16(new byte[] { bytes[i + 1], bytes[i] }, 0);
                i += 2;
            } else if (payloadLen == 127) {
                payloadLen = BitConverter.ToUInt64(bytes, i);
                i += 8;
            }

            byte[] payload = new byte[payloadLen];
            if (mask) {
                byte[] maskingKey = new byte[4] { bytes[i], bytes[i + 1], bytes[i + 2], bytes[i + 3] };
                i += 4;
                for (ulong j = 0; j < payloadLen; j++)
                    payload[j] = (byte)(bytes[i + (int)j] ^ maskingKey[j % 4]);
            } else {
                Array.Copy(bytes, i, payload, 0, (int)payloadLen);
            }

            i += (int)payloadLen;
            switch (opcode) {
                case 0x1: // text
                case 0x2: // binary
                    messages.Add(payload);
                    break;

                case 0x9: // PING
                    Console.WriteLine("pinged, sending pong");
                    SendPong(connection, payload);
                    break;

                case 0xA: // PONG
                          // ignore
                    break;

                case 0x8: // CLOSE
                    throw new Exception("Client closed connection");
            }
        }

        return messages;
    }

    static void SendPong(ClientConnection client, byte[] payload) {
        byte[] frame = EncodeControlFrame(0xA, payload);
        client.stream.Write(frame, 0, frame.Length);
    }

    public static byte[] EncodeFrame(byte[] payload, bool isBinary = true) {
        List<byte> frame = new List<byte>();
        frame.Add((byte)((isBinary ? 0x82 : 0x81)));

        if (payload.Length < 126) {
            frame.Add((byte)payload.Length);
        } else if (payload.Length <= ushort.MaxValue) {
            frame.Add(126);
            frame.AddRange(BitConverter.GetBytes((ushort)IPAddress.HostToNetworkOrder((short)payload.Length)));
        } else {
            frame.Add(127);
            frame.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((long)payload.Length)));
        }

        frame.AddRange(payload);
        return frame.ToArray();
    }

    public static byte[] EncodeControlFrame(byte opcode, byte[] payload = null) {
        payload ??= Array.Empty<byte>();

        if (payload.Length > 125)
            throw new ArgumentException("Control frame payload too large");

        List<byte> frame = new List<byte>();

        // FIN = 1, RSV = 0, OPCODE = opcode
        frame.Add((byte)(0x80 | (opcode & 0x0F)));

        // Server frames are NOT masked
        frame.Add((byte)payload.Length);

        if (payload.Length > 0)
            frame.AddRange(payload);

        return frame.ToArray();
    }
}