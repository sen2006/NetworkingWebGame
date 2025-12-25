using shared;
using System.Collections;
using System.Collections.Generic;
//using System.Net.Sockets;
using UnityEngine;

using NativeWebSocket;
using System.Threading.Tasks;

public class MainNetworking : MonoBehaviour {
    private static Queue<ISerializable> networkMessageQueue = new Queue<ISerializable>();

    NativeWebSocket.WebSocket websocket;

    //TODO web does not support threads, rework this:
    //private static Thread heartbeatThread = new Thread(HeartBeat);

    public UIManager _UIManager;

    [SerializeField] private string[] serverAddresses;
    [SerializeField] private bool isHTTPS;
    [SerializeField] private string cachedAddress = null;
    [SerializeField] private int port = 55555;
    [SerializeField] private int maxSearchTimeout = 10;

    //private static TcpClient client;
    private int ID = -1;
    private bool connected;

    // game data

    [SerializeField] string cashedTeamPassword;
    GameTeam cashedTeam;
    [SerializeField] bool loggedIn = false;
    GameData cashedGameData;

    private async void Start()
    {
        await connectServerAsync();
    }

    private async void Update() {
        if (!connected && websocket == null) {
            await connectServerAsync();
        } else if (websocket != null) {
#if !UNITY_WEBGL || UNITY_EDITOR
            websocket.DispatchMessageQueue();
#endif
            handleMessageSending();
        }
    }

    private async Task connectServerAsync() {
        foreach (string adress in serverAddresses) {
            try {
                Debug.Log($"Attempting to connect to: {adress}:{port}");
                websocket = new WebSocket($"ws://{adress}:{port}");
                //websocket = new WebSocket("ws://echo.websocket.events");
                websocket.OnOpen += () =>
                {   
                    Debug.Log("Connection open!");
                    connected = true;
                };

                websocket.OnError += (e) =>
                {
                    Debug.Log("Error! " + e);
                };

                websocket.OnClose += (e) =>
                {
                    loggedIn = false;
                    connected = false;
                    Debug.Log("Connection closed!");
                };

                websocket.OnMessage += (bytes) =>
                {
                    Debug.Log("OnMessage!");
                    //Debug.Log(bytes);
                    Packet packet = new Packet(bytes);
                    HandleMessage(packet.ReadObject());
                };

                // waiting for messages
                await websocket.Connect();
                return;
            } finally {
            }
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
        yield return websocket.Send(packet.GetBytes());

        /*
        UnityWebRequest www = UnityWebRequest.Put($"http://{cachedAddress}:{port}/", data);

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success) {
            Debug.Log("Error when sending webRequest");
        } else {
            ISerializable returnMessage = new Packet(www.downloadHandler.data).ReadObject();
            HandleMessage(returnMessage);
        }*/
    }

    private void HandleMessage(ISerializable message) {
        if (message is AcceptClientMessage) {
            Debug.Log("Accept Recieved");
            _UIManager.ServerFound();
            return;
        } else if (message is LoginResultMessage loginResultMessage) {
            if (loginResultMessage.success) {
                cashedTeam = loginResultMessage.team;
                cashedTeamPassword = loginResultMessage.password;
                loggedIn = true;
                _UIManager.LoggedIn();
                _UIManager.SetTeamName(cashedTeam.name);
                WriteObject(new RequestDataUpdateMessage());
                Debug.Log($"Logged in as {cashedTeam.name}");
            } else {
                _UIManager.LoggedInWrongPass();
                Debug.Log("Wrong password");
            }
            return;
        } else if (message is DataUpdateMessage dataUpdateMessage) {
            cashedGameData = dataUpdateMessage.data;
            _UIManager.UpdateGameDataToScreen(cashedGameData);
        }
    }

    public void attemptLogin(string pass) {
        WriteObject(new LoginAttemptMessage(pass));
    }

    private void WriteObject<T>(T pObject) where T : ISerializable {
       networkMessageQueue.Enqueue(pObject);
    }

    private async void OnApplicationQuit() {
        await websocket.Close();
    }
}
