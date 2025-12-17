using shared;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/**
 * The main ChatLobbyClient where you will have to do most of your work.
 * 
 * @author J.C. Wichman
 */
public class ChatLobbyClient : MonoBehaviour
{
    public Button button;
    public TextMeshProUGUI text;

    private static Queue<ISerializable> networkMessageQueue = new Queue<ISerializable>();
    
    //TODO web does not support threads, rework this:
    //private static Thread heartbeatThread = new Thread(HeartBeat);

    [SerializeField] private string serverAdress = "localhost";
    [SerializeField] private int port = 55555;

    private static TcpClient client;
    private int ID = -1;
    private bool accepted;

    private void Start()
    {
        button.onClick.AddListener(onClicked);
        connectToServer();
        //heartbeatThread.Start();
    }

    private void connectToServer()
    {
        accepted = false;
        ID = -1;
        try
        {
            client = new TcpClient();
            client.Connect(serverAdress, port);
            Debug.Log("Connected to server.");
        }
        catch (Exception e)
        {
            Debug.Log("Could not connect to server:");
            Debug.Log(e.Message);
        }
    }

    private void onClicked() {
        if (accepted) {
            WriteObject(new ButtonClickMessage());
        }
    }

    private void Update()
    {
        button.gameObject.SetActive(accepted);
        try
        {
            if (client.Available > 0)
            {
                ISerializable readObject = StreamUtil.ReadObject(client.GetStream());
                if (accepted == false) 
                { 
                    if (readObject is AcceptClientMessage acceptClientMessage)
                    {
                        ID = acceptClientMessage.GetId();
                        accepted = true;
                    }
                }
                else
                {
                    if (readObject is AcceptClientMessage serverChatMessage)
                    {
                        Debug.LogError("Received accept message while already accepted");
                    }
                }
            }
            handleMessageSending(client.GetStream());
        }
        catch (Exception e)
        {
            //for quicker testing, we reconnect if something goes wrong.
            Debug.Log(e.Message);
            client.Close();
            connectToServer();
        }
    }

    private void handleMessageSending(NetworkStream pStream) {
        while (networkMessageQueue.Count > 0) {
            ISerializable message = networkMessageQueue.Dequeue();
            StreamUtil.WriteObject(pStream, message);
        }
    }

    private static void HeartBeat()
    {
        while (true)
        {
            WriteObject(new HeartBeatMessage());
            Thread.Sleep(500);
        }
    }

    private static void WriteObject<T>(T pObject) where T : ISerializable {
       networkMessageQueue.Enqueue(pObject);
    }

}
