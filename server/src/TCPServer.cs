using System.Net.Sockets;
using System.Net;
using shared;


class TCPServer
{

    private static int nextID = 0;
    public static void Main(string[] args)
	{
		TCPServer server = new TCPServer();
		server.run();
	}

	internal static TcpListener listener;
	internal static Dictionary<ClientData, Avatar> clients = new Dictionary<ClientData, Avatar>();
	internal static Queue<Message> pendingMessages = new Queue<Message>();

	private void run()
	{
		Console.WriteLine("Server started on port 55555");

		listener = new TcpListener(IPAddress.Any, 55555);
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
		while (listener.Pending())
		{
			TcpClient acceptedClient = listener.AcceptTcpClient();
			ClientData acceptedClientData = new ClientData(acceptedClient);
			Avatar newAvatar = new Avatar(nextID++, new Random().Next(0, 1000));

            clients.Add(acceptedClientData, newAvatar);

            pendingMessages.Enqueue(new Message(new AcceptClientMessage(newAvatar.GetID()), acceptedClientData));

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
				if (TCPServer.clients.Keys.Contains(client))
					StreamUtil.WriteObject(client.GetStream(), pObject);
			}
			catch (Exception e)
			{
				Console.WriteLine("an error occured when trying to write an object to client, removing client:");
				Console.WriteLine(e.Message);
				TCPServer.clients.Remove(client);
			}
		}
	}
}


