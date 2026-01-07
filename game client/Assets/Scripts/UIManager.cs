using NUnit.Framework;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    
    enum ScreenState {
        SEARCHING_SERVER,
        LOGIN,
        GAME
    }

    [SerializeField] MainNetworking network;

    [SerializeField] GameObject loginScreen;
    [SerializeField] ScreenState screenState = ScreenState.SEARCHING_SERVER;

    [SerializeField] Button loginButton;
    [SerializeField] TMP_InputField loginField;
    [SerializeField] TextMeshProUGUI loginDebugText;

    [SerializeField] GameObject gameScreen;
    [SerializeField] TextMeshProUGUI teamNameText;
    [SerializeField] TextMeshProUGUI basicGameInfo;

    private void Start() {
        loginButton.onClick.AddListener(TryLogin);
        loginDebugText.text = "";
    }
    void Update()
    {
        loginScreen.SetActive(screenState==ScreenState.LOGIN);
        gameScreen.SetActive(screenState==ScreenState.GAME);
    }

    void TryLogin() {
        network.attemptLogin(loginField.text);
    }

    public void ServerFound() {
        screenState = ScreenState.LOGIN;
    }

    public void LoggedInWrongPass() {
        loginDebugText.text = "Wrong Password";
    }

    public void LoggedIn() {
        screenState=ScreenState.GAME;
    }


    public void SetTeamName(string teamName) {
        string finalTeamName = teamName.Replace("_", " ");

        teamNameText.text = $"Team Name: {finalTeamName}";
    }

    public void UpdateGameDataToScreen(GameData gameData) {
        int teamCount = gameData.TeamAmount();
        int taskCount = gameData.TaskAmount();
        basicGameInfo.text = 
            $"Teams: {teamCount}\n" +
            $"Tasks: {taskCount}";
    }
}
