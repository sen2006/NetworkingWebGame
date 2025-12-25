using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Text;
using shared;
using System.IO;
using System.Diagnostics;


class Server
{
	enum GameState {
		WAITING,
		RUNNING,
		FINNISHED
	}

	public static TcpListener server;
	public static List<TcpClient> clients = [];
	static Queue<Message> messageQueue = new();


    private static Thread commandThread = new Thread(handleConsoleCommands);

    private static int port = 55555;
	private static bool isHTTPS = false;

	public static GameData _gameData = new GameData();
    internal static string savePath = "./save";

    public static void Main() {
        server = new TcpListener(IPAddress.Any, 55555);

        server.Start();
        Console.WriteLine("Server has started on port 55555.");
        Console.WriteLine("");


		loadSaveData(savePath);
		commandThread.Start();

   


		while (true) {
			acceptNewClients();
            processExistingClients();
            sendMessages();
			Thread.Sleep(100);
        }
	}

	private static void acceptNewClients() {
        if (server.Pending()) {

            TcpClient acceptedClient = server.AcceptTcpClient();
            Console.WriteLine("A client connected.");

            NetworkStream stream = acceptedClient.GetStream();

            clients.Add(acceptedClient);
			messageQueue.Enqueue(new Message(new AcceptClientMessage(1),acceptedClient));

            Console.WriteLine("Accepted new client.");
            Console.WriteLine("");
        }
    }

	private static void processExistingClients() {
        foreach (TcpClient client in clients) {
			while (client.Available > 0) {
                NetworkStream stream = client.GetStream();
				ISerializable readMessage = null;
				try {
                    while (!stream.DataAvailable) ;
                    while (client.Available < 3) ; // match against "get"

                    byte[] bytes = new byte[client.Available];
                    stream.Read(bytes, 0, bytes.Length);
                    string s = Encoding.UTF8.GetString(bytes);

                    if (Regex.IsMatch(s, "^GET", RegexOptions.IgnoreCase)) {
                        Console.WriteLine("=====Handshaking from client=====\n{0}", s);

                        // 1. Obtain the value of the "Sec-WebSocket-Key" request header without any leading or trailing whitespace
                        // 2. Concatenate it with "258EAFA5-E914-47DA-95CA-C5AB0DC85B11" (a special GUID specified by RFC 6455)
                        // 3. Compute SHA-1 and Base64 hash of the new value
                        // 4. Write the hash back as the value of "Sec-WebSocket-Accept" response header in an HTTP response
                        string swk = Regex.Match(s, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                        string swkAndSalt = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                        byte[] swkAndSaltSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swkAndSalt));
                        string swkAndSaltSha1Base64 = Convert.ToBase64String(swkAndSaltSha1);

                        // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
                        byte[] response = Encoding.UTF8.GetBytes(
                            "HTTP/1.1 101 Switching Protocols\r\n" +
                            "Connection: Upgrade\r\n" +
                            "Upgrade: websocket\r\n" +
                            "Sec-WebSocket-Accept: " + swkAndSaltSha1Base64 + "\r\n\r\n");

                        stream.Write(response, 0, response.Length);
                    } else {
                        bool fin = (bytes[0] & 0b10000000) != 0,
                            mask = (bytes[1] & 0b10000000) != 0; // must be true, "All messages from the client to the server have this bit set"
                        int opcode = bytes[0] & 0b00001111; // expecting 1 - text message
                        ulong offset = 2,
                              msgLen = bytes[1] & (ulong)0b01111111;

                        if (msgLen == 126) {
                            // bytes are reversed because websocket will print them in Big-Endian, whereas
                            // BitConverter will want them arranged in little-endian on windows
                            msgLen = BitConverter.ToUInt16(new byte[] { bytes[3], bytes[2] }, 0);
                            offset = 4;
                        } else if (msgLen == 127) {
                            // To test the below code, we need to manually buffer larger messages — since the NIC's autobuffering
                            // may be too latency-friendly for this code to run (that is, we may have only some of the bytes in this
                            // websocket frame available through client.Available).
                            msgLen = BitConverter.ToUInt64(new byte[] { bytes[9], bytes[8], bytes[7], bytes[6], bytes[5], bytes[4], bytes[3], bytes[2] }, 0);
                            offset = 10;
                        }

                        if (msgLen == 0) {
                        } else if (mask) {
                            byte[] decoded = new byte[msgLen];
                            byte[] masks = new byte[4] { bytes[offset], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3] };
                            offset += 4;

                            for (ulong i = 0; i < msgLen; ++i)
                                decoded[i] = (byte)(bytes[offset + i] ^ masks[i % 4]);

                        Packet packet = new Packet(decoded);
                        readMessage = packet.ReadObject();
                        } else
                            Console.WriteLine("mask bit not set\n");

                    }
                } catch (Exception e) {
                    Console.WriteLine($"Error reading client message");
                    Console.WriteLine($"Kicking client");
                    Console.WriteLine($"");
                    clients.Remove(client);
                    client.Close();
                    continue;
                }
				if (readMessage != null) {
					ISerializable returnMessage = handleMessage(readMessage);
					if (returnMessage != null) {
						messageQueue.Enqueue(new Message(returnMessage, client));
					}
				}
            }
		}
	}

    static private void sendMessages() {
        while (messageQueue.Count > 0) {
            Message message = messageQueue.Dequeue();
            message.Send();
        }
    }

    private static ISerializable handleMessage(ISerializable message) {
		bool lockTaken = false;
		try {
			Monitor.Enter(_gameData, ref lockTaken);

			if (message is LoginAttemptMessage loginAttemptMessage) {
				string password = loginAttemptMessage.password.ToLower();
				LoginResultMessage loginResult = new LoginResultMessage();
				Console.WriteLine($"Client atempting login with pass: {password}");
				if (_gameData.teamExists(password)) {
					Console.WriteLine($"Team found, returning success");
					GameTeam team = _gameData.GetTeamData(password);
					loginResult = new LoginResultMessage(team, password);
				} else Console.WriteLine($"No Team found, returning faillure");
                Console.WriteLine();
                return loginResult;
			} else if (message is RequestDataUpdateMessage buttonClickMessage) {
                Console.WriteLine("Data Update Request Recieved, sending update");
                Console.WriteLine();
                return new DataUpdateMessage(_gameData);
			}

            throw new Exception($"UnHandledMessage: {message.GetType}");
        } 
		finally {
			if (lockTaken) Monitor.Exit(_gameData);
        }
    }

	//command thread
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
		if (File.Exists(path)) {
			byte[] data = File.ReadAllBytes(path);
			Packet packet = new Packet(data);
			_gameData = (GameData)packet.ReadObject();
		}
	}

    public static void saveGameData(string path) {
        Packet save = new Packet();
        save.Write(_gameData);
		_gameData.updated = false;
		File.Delete(path);
        File.WriteAllBytes(path, save.GetBytes());
    }

	public static void CheckSaveDataDirty() {
		if (_gameData.updated) {
			Console.WriteLine("Saving...");
			saveGameData(savePath);
			Console.WriteLine("Save complete");
			Console.WriteLine("");
        }
    }

    public static void SendMessageToClient(TcpClient client, ISerializable msg) {
        Console.WriteLine("Sending: " + msg.GetType());
        NetworkStream stream = client.GetStream();

        Packet packet = new Packet();
        packet.Write(msg);
        byte[] payload = packet.GetBytes();

        List<byte> frame = new List<byte>();

        // FIN + BINARY opcode
        frame.Add(0x82);

        // payload length (server never masks)
        if (payload.Length < 126) {
            frame.Add((byte)payload.Length);
        } else if (payload.Length <= ushort.MaxValue) {
            frame.Add(126);
            frame.AddRange(BitConverter.GetBytes(
                (ushort)IPAddress.HostToNetworkOrder((short)payload.Length)));
        } else {
            frame.Add(127);
            frame.AddRange(BitConverter.GetBytes(
                (ulong)IPAddress.HostToNetworkOrder((long)payload.Length)));
        }

        frame.AddRange(payload);

        stream.Write(frame.ToArray(), 0, frame.Count);
    }

}

internal class Message {
    readonly List<TcpClient> recipients = new List<TcpClient>();
    readonly ISerializable toSend;

    public Message(ISerializable sendObject, TcpClient recipient) {
        toSend = sendObject;
        recipients.Add(recipient);
    }

    public Message(ISerializable sendObject, List<TcpClient> recipients) {
        toSend = sendObject;
        this.recipients.AddRange(recipients);
    }

    public void AddRecipient(TcpClient recipient) {
        recipients.Add(recipient);
    }

    public void Send() {
        WriteObjectToAll(recipients, toSend);
    }

    static void WriteObjectToAll<T>(List<TcpClient> SendList, T pObject) where T : ISerializable {
        foreach (TcpClient client in SendList) {
            try {
                Server.SendMessageToClient(client, pObject);
            } catch (Exception e) {
                Console.WriteLine("an error occured when trying to write an object to client, removing client:");
                Console.WriteLine(e.Message);
                Server.clients.Remove(client);
            }
        }
    }
}


