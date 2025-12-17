using shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/**
 * The main ChatLobbyClient where you will have to do most of your work.
 * 
 * @author J.C. Wichman
 */
public class ChatLobbyClient : MonoBehaviour {
    public Button button;
    public TextMeshProUGUI text;

    private static Queue<ISerializable> networkMessageQueue = new Queue<ISerializable>();

    //TODO web does not support threads, rework this:
    //private static Thread heartbeatThread = new Thread(HeartBeat);

    [SerializeField] private string[] serverAddresses;
    [SerializeField] private string cachedAddress = null;
    [SerializeField] private int port = 55555;
    [SerializeField] private int maxSearchTimeout = 10;

    private static TcpClient client;
    private int ID = -1;
    private bool accepted;

    private void Start()
    {
        button.onClick.AddListener(onClicked);
        button.gameObject.SetActive(false);
        StartCoroutine(searchServer());
        
        //connectToServer();
        //heartbeatThread.Start();
    }

    private void connectToServer()
    {
        accepted = false;
        ID = -1;
        try
        {
            client = new TcpClient();
            //client.Connect(serverAdress, port);
            Debug.Log("Connected to server.");
        }
        catch (Exception e)
        {
            Debug.Log("Could not connect to server:");
            Debug.Log(e.Message);
        }
    }

    private async void onClicked() {
        if (accepted) {
            int i = 0;
            while (i < 80) {
                WriteObject(new ButtonClickMessage());
                i++;
            }
        }
    }

    private IEnumerator searchServer() {
        foreach (string adress in serverAddresses) {
            try {
                Debug.Log("Searching Server On: " + $"http://{adress}:{port}/");
                UnityWebRequest www = UnityWebRequest.Get($"http://{adress}:{port}/");
                www.timeout = maxSearchTimeout;
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success) {
                    Debug.Log("ServerNotFound on: " + $"http://{adress}:{port}/");
                } else {
                    // Show results as text
                    ISerializable message = new Packet(www.downloadHandler.data).ReadObject();

                    Debug.Log("ServerFound on: " + $"http://{adress}:{port}/, cashing IP");

                    cachedAddress = adress;
                    accepted = true;
                    yield break;
                }
            } finally {
            }
        }
    }

    private void Update()
    {
        handleMessageSending();
        if (accepted) { button.gameObject.SetActive(true); }
        //button.gameObject.SetActive(accepted);
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
        }
        catch (Exception e)
        {
            //for quicker testing, we reconnect if something goes wrong.
            //Debug.Log(e.Message);
            //client.Close();
            //connectToServer();
        }
    }

    private void handleMessageSending() {
        while (networkMessageQueue.Count > 0) {
            ISerializable message = networkMessageQueue.Dequeue();
            StartCoroutine(SendMessage(message));
        }
    }

    private IEnumerator SendMessage(ISerializable message) {
        Packet packet = new Packet();
        packet.Write(message);
        byte[] length = BitConverter.GetBytes(packet.GetBytes().Length);

        byte[] data = new byte[length.Length + packet.GetBytes().Length];

        int i = 0;
        foreach (byte b in length) {
            data[i++] = b;
        }
        foreach (byte b in packet.GetBytes()) {
            data[i++] = b;
        }

        UnityWebRequest www = UnityWebRequest.Put($"http://{cachedAddress}:{port}/", data);

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success) {
            Debug.Log("Error when sending webRequest");
        } else {
            // Show results as text
            ISerializable returnMessage = new Packet(www.downloadHandler.data).ReadObject();
            HandleMessage(returnMessage);
        }
    }

    private static void HandleMessage(ISerializable message) {
        if (message is AcceptClientMessage) {
            Debug.Log("Accept Recieved");
        }
    }

    private static void WriteObject<T>(T pObject) where T : ISerializable {
       networkMessageQueue.Enqueue(pObject);
    }

}
