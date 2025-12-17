using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net.Sockets;

namespace shared
{
	/**
	 * StreamUtil class should be used whenever you want to send/receive bytes over a TcpClient connection.
	 * The reason is that communication across a TcpClient does not preserve message boundaries.
	 * In other words 1 send MIGHT equal one receive, but due to whatever network conditions, 
	 * 1 send might also equal 2+ receives and 2+ sends might end up being seen as 1 receive.
	 * 
	 * In other words: 
	 * We need to overlay a mechanism over the TcpConnection to detect these boundaries ourselves.
	 * 
	 * That mechanism is very simple: we send the size of our message first so that we know how many bytes
	 * make up our single message on the receiving end.
	 */
	public static class StreamUtil
	{
		/**
		 * Writes the size of the given byte array into the stream and then the bytes themselves.
		 */
		private static void WriteBytes(NetworkStream pStream, byte[] pMessage)
		{
			if (pMessage.Length == 0) return;

			//convert message length to 4 bytes and write those bytes into the stream
			pStream.Write(BitConverter.GetBytes(pMessage.Length), 0, 4);
			//now send the bytes of the message themselves
			pStream.Write(pMessage, 0, pMessage.Length);
		}

		/**
		 * Reads the amount of bytes to receive from the stream and then the bytes themselves.
		 */
		private static byte[] ReadBytes(NetworkStream pStream)
		{

			//get the message size first
			int byteCountToRead = BitConverter.ToInt32(Read(pStream, 4), 0);
			//then read that amount of bytes
			return Read(pStream, byteCountToRead);
		}

		public static void WriteObject<T>(NetworkStream pStream, T pObject) where T : ISerializable
		{
			Packet packet = new Packet();
			packet.Write(pObject.GetType().FullName);
			pObject.Serialize(packet);
			WriteBytes(pStream,packet.GetBytes());
		}

		public static ISerializable ReadObject(NetworkStream pStream)
		{
			byte[] bytes = ReadBytes(pStream);
			Packet packet = new Packet(bytes);
            return packet.ReadObject();
			
		}

		public static T ReadObject<T>(NetworkStream pStream) where T : ISerializable
		{
			ISerializable obj = ReadObject(pStream);
            if (obj is T toReturn)
				return toReturn;
            return default;
		}

        /**
		 * Read the given amount of bytes from the stream
		 */
        private static byte[] Read(NetworkStream pStream, int pByteCount)
		{
			//create a buffer to hold all the requested bytes
			byte[] bytes = new byte[pByteCount];
			//keep track of how many bytes we read last read operation
			int bytesRead = 0;
			//and keep track of how many bytes we've read in total
			int totalBytesRead = 0;

			try
			{
				//keep reading bytes until we've got what we are looking for or something bad happens.
				while (
					totalBytesRead != pByteCount &&
					(bytesRead = pStream.Read(bytes, totalBytesRead, pByteCount - totalBytesRead)) > 0
				)
				{
					totalBytesRead += bytesRead;
				}
			}
			catch { }

			return (totalBytesRead == pByteCount) ? bytes : null;
		}
	}

}


