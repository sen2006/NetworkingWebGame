using System.Net.Sockets;
using System.Net;
using shared;


class Server
{
	public static void Main() {
		listener.Start();
		Console.WriteLine("Listener Started");

		int port = 55555;

		listener.Prefixes.Add($"http://localhost:{port}/");
		listener.Prefixes.Add($"http://127.0.0.1:{port}/");
		listener.Prefixes.Add($"http://+:{port}/");
		listener.Prefixes.Add($"http://*:{port}/");

		while (true) {
			HttpListenerContext context = listener.GetContext();
			HttpListenerRequest request = context.Request;


			HttpListenerResponse response = context.Response;
			response.AddHeader("Access-Control-Allow-Credentials", "true");
			response.AddHeader("Access-Control-Allow-Headers", "Accept, X-Access-Token, X-Application-Name, X-Request-Sent-Time");
			response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
			response.AddHeader("Access-Control-Allow-Origin", "*");
			// Construct a response.
			string responseString = "1";
			//byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
			Packet packet = new Packet();
			AcceptClientMessage message = new AcceptClientMessage(1);
            message.Serialize(packet);
            byte[] buffer = packet.GetBytes();
            // Get a response stream and write the response to it.
			Console.WriteLine("RequestRecieved, sending response");
            response.ContentLength64 = buffer.Length;
			Stream output = response.OutputStream;
			output.Write(buffer, 0, buffer.Length);
			// You must close the output stream.
			output.Close();
		}
		listener.Stop();
	}
    
    private static int nextID = 0;
    /*public static void Main(string[] args)
	{
		Server server = new Server();
		server.run();
	}*/
	
	internal static HttpListener listener = new HttpListener();
	internal static Dictionary<ClientData, Avatar> clients = new Dictionary<ClientData, Avatar>();
	internal static Queue<Message> pendingMessages = new Queue<Message>();

	private void run()
	{
		Console.WriteLine("Server started on port 55555");
        listener = new HttpListener();

        listener.Prefixes.Add("http://*:55555/");
		listener.Start();

		while (true)
		{
			processNewClients();
			processExistingClients();
			sendMessages();
			Thread.Sleep(100);
		}
	}

	private void removeClient(ClientData client)
	{
		Console.WriteLine("removed a client from the server");
        clients.Remove(client);
		try
		{
			client.GetRawClient().Close();
		}
		catch { }
	}

	private void processNewClients()
	{
		while (false)//listener.Pending())
		{
			//TcpClient acceptedClient = listener.AcceptTcpClient();
			//ClientData acceptedClientData = new ClientData(acceptedClient);
			Avatar newAvatar = new Avatar(nextID++, new Random().Next(0, 1000));

            //clients.Add(acceptedClientData, newAvatar);

            //pendingMessages.Enqueue(new Message(new AcceptClientMessage(newAvatar.GetID()), acceptedClientData));

			Console.WriteLine("Accepted new client.");
		}
	}

	private void processExistingClients()
	{
		foreach (ClientData client in clients.Keys)
		{
			if (client.Available > 0)
			{
				NetworkStream stream = client.GetStream();
				object readObject = null;

				// a try in case a client send something that could not be read
				try
				{
					readObject = StreamUtil.ReadObject(stream);
				}
				catch (Exception e)
				{
					Console.WriteLine($"error reading client message");
					continue;
				}
				if (readObject is HeartBeatMessage) {
					client.SendHeartbeat();
				} else if (readObject is ButtonClickMessage) {
					Console.WriteLine("Button Clicked");
				}
            } else {
                client.HeartbeatTick();
                if (client.HeartbeatFailed())
                {
                    Console.WriteLine("a client failed its heartbeat");
                    removeClient(client);
                }
            }
        }
	}

	private void sendMessages()
	{
		while (pendingMessages.Count > 0)
		{
			Message message = pendingMessages.Dequeue();
			message.Send();
		}
		pendingMessages.Clear();
	}
}

internal class Message
{
	readonly List<ClientData> recipients = new List<ClientData>();
	readonly ISerializable toSend;

	public Message(ISerializable sendObject, ClientData recipient)
	{
		toSend = sendObject;
		recipients.Add(recipient);
	}

	public Message(ISerializable sendObject, List<ClientData> recipients)
	{
		toSend = sendObject;
		this.recipients.AddRange(recipients);
	}

	public void AddRecipient(ClientData recipient)
	{
		recipients.Add(recipient);
	}

	public void Send()
	{
		WriteObjectToAll(recipients, toSend);
	}

	static void WriteObjectToAll<T>(List<ClientData> SendList, T pObject) where T : ISerializable
	{
		foreach (ClientData client in SendList)
		{
			try
			{
				if (Server.clients.Keys.Contains(client))
					StreamUtil.WriteObject(client.GetStream(), pObject);
			}
			catch (Exception e)
			{
				Console.WriteLine("an error occured when trying to write an object to client, removing client:");
				Console.WriteLine(e.Message);
				Server.clients.Remove(client);
			}
		}
	}
}


