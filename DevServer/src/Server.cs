using System.Net.Sockets;
using System.Net;
using shared;


class Server
{
	internal static long messageCount = 0;
    internal static HttpListener listener = new HttpListener();
    internal static int port = 55555;

	public static void Main() {
		listener.Start();
		Console.WriteLine("Listener Started");


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
			response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS, PUT");
			response.AddHeader("Access-Control-Allow-Origin", "*");


			if (request.HttpMethod == "GET") {
				Packet packet = new Packet();
				AcceptClientMessage message = new AcceptClientMessage(0);
				packet.Write(message);
				byte[] buffer = packet.GetBytes();
				Console.WriteLine("RequestRecieved, sending response");
				response.ContentLength64 = buffer.Length;
				Stream output = response.OutputStream;
				output.Write(buffer, 0, buffer.Length);
				output.Close();
			}
			if (request.HttpMethod == "PUT") {
				try {
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


					Console.WriteLine($"MessageID: {messageCount++}");
				} catch (Exception e) {
					Console.WriteLine("Ran into Error reading incoming message: ");
					Console.WriteLine(e);
				}
			}
		}
		listener.Stop();
	}

	private static ISerializable handleMessage(ISerializable message) {


		return new AcceptClientMessage(2);
	}

}


