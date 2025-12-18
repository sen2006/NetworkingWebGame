using System.Net;
using shared;


class Server
{
	enum GameState {
		WAITING,
		RUNNING,
		FINNISHED
	}

	private static Thread commandThread = new Thread(handleConsoleCommands);

	internal static long messageCount = 0;
    internal static HttpListener listener = new HttpListener();
    internal static int port = 55555;

	public static GameData _gameData = new GameData();
    internal static string savePath = "./save";

    public static void Main() {
		listener.Start();
		Console.WriteLine("Listener Started");
        Console.WriteLine("");

        listener.Prefixes.Add($"http://localhost:{port}/");
		listener.Prefixes.Add($"http://127.0.0.1:{port}/");
		listener.Prefixes.Add($"http://+:{port}/");
		listener.Prefixes.Add($"http://*:{port}/");

		loadSaveData(savePath);
		commandThread.Start();

		while (true) {
			HttpListenerContext context = listener.GetContext();
			HttpListenerRequest request = context.Request;

			HttpListenerResponse response = context.Response;
			response.AddHeader("Access-Control-Allow-Credentials", "true");
			response.AddHeader("Access-Control-Allow-Headers", "Accept, X-Access-Token, X-Application-Name, X-Request-Sent-Time, Content-Type");
			response.AddHeader("Access-Control-Allow-Methods", "PUT, GET, POST, OPTIONS");
			response.AddHeader("Access-Control-Allow-Origin", "*");


			if (request.HttpMethod == "GET" || request.HttpMethod == "OPTIONS") {
				Packet packet = new Packet();
				AcceptClientMessage message = new AcceptClientMessage(0);
				packet.Write(message);
				byte[] buffer = packet.GetBytes();
                Console.WriteLine("Get RequestRecieved, sending response");
                Console.WriteLine("");
                response.ContentLength64 = buffer.Length;
				Stream output = response.OutputStream;
				output.Write(buffer, 0, buffer.Length);
				output.Close();
			}
			if (request.HttpMethod == "PUT") {
				try {
                    Console.WriteLine("PUT Message Recieved");
                    byte[] inLenthBuffer = new byte[4];
					request.InputStream.Read(inLenthBuffer, 0, 4);
					byte[] inBuffer = new byte[BitConverter.ToInt32(inLenthBuffer)];
					request.InputStream.Read(inBuffer, 0, inBuffer.Length);

					Packet inPacket = new Packet(inBuffer);
					ISerializable inMessage = inPacket.ReadObject();
					Console.WriteLine(inMessage.GetType().Name);
					request.InputStream.Close();

					ISerializable returnMessage = handleMessage(inMessage);

					Packet packet = new Packet();
					packet.Write(returnMessage);
					byte[] buffer = packet.GetBytes();
					response.ContentLength64 = buffer.Length;
					Stream output = response.OutputStream;
					output.Write(buffer, 0, buffer.Length);
					output.Close();

					CheckSaveDataDirty();
					Console.WriteLine($"PUT MessageID: {++messageCount}");
                    Console.WriteLine("");
                } catch (Exception e) {
					Console.WriteLine("Ran into Error reading incoming message: ");
					Console.WriteLine(e);
                    Console.WriteLine("");
                }
			}
		}
		listener.Stop();
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
				return loginResult;
			} else if (message is RequestDataUpdateMessage buttonClickMessage) {
                Console.WriteLine("DataUpdateRequestRecieved, sending update");
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

}


