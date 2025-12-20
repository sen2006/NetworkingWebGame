using shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using static System.Net.WebRequestMethods;

public class MainNetworking : MonoBehaviour {
    private static Queue<ISerializable> networkMessageQueue = new Queue<ISerializable>();


    //TODO web does not support threads, rework this:
    //private static Thread heartbeatThread = new Thread(HeartBeat);

    public UIManager _UIManager;

    [SerializeField] private string[] serverAddresses;
    [SerializeField] private bool isHTTPS;
    [SerializeField] private string cachedAddress = null;
    [SerializeField] private int port = 55555;
    [SerializeField] private int maxSearchTimeout = 10;

    private static TcpClient client;
    private int ID = -1;
    private bool accepted;

    // game data

    [SerializeField] string cashedTeamPassword;
    GameTeam cashedTeam;
    [SerializeField] bool loggedIn = false;
    GameData cashedGameData;

    private void Start()
    {
        StartCoroutine(searchServer());
    }

    private void Update() {
        handleMessageSending();
        if (accepted) { 
        
        }
        //button.gameObject.SetActive(accepted);
       
    }

    private IEnumerator searchServer() {
        foreach (string adress in serverAddresses) {
            try {
                string httpType = isHTTPS ? "https" : "http";
                Debug.Log("Searching Server On: " + $"{httpType}://{adress}:{port}/");
                UnityWebRequest www = UnityWebRequest.Get($"{httpType}://{adress}:{port}/");
                www.timeout = maxSearchTimeout;
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success) {
                    Debug.Log("Could not connect to: " + $"{httpType}://{adress}:{port}/\nReason: {www.error}");
                } else {
                    // Show results as text
                    ISerializable message = new Packet(www.downloadHandler.data).ReadObject();

                    if (message is AcceptClientMessage acm) {

                        Debug.Log("ServerFound on: " + $"{httpType}://{adress}:{port}/, cashing IP");

                        cachedAddress = adress;
                        accepted = true;
                        ID = acm.GetId();
                        _UIManager.ServerFound();
                        yield break;
                    } else { throw new Exception("Recieved wrong message when expecting accept message"); }
                }
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
            ISerializable returnMessage = new Packet(www.downloadHandler.data).ReadObject();
            HandleMessage(returnMessage);
        }
    }

    private void HandleMessage(ISerializable message) {
        if (message is AcceptClientMessage) {
            Debug.Log("Accept Recieved");
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

}
