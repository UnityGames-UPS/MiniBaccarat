using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Newtonsoft.Json;
using Best.SocketIO;
using Best.SocketIO.Events;
using UnityEngine.UI;

public class SocketIOManager : MonoBehaviour
{
  [Header("User Token")]
  [SerializeField] private string TestToken;

  [Header("Managers")]
  [SerializeField] private UIManager uiManager;
  [SerializeField] internal JSFunctCalls JSManager;
  private Socket gameSocket;
  protected string NameSpace = "playground";
  protected string SocketURI = null;
  protected string TestSocketURI = "https://devrealtime.dingdinghouse.com/";
  protected string gameID = "OT-MBC";
  private SocketManager manager;
  private const int maxReconnectionAttempts = 6;
  private readonly TimeSpan reconnectionDelay = TimeSpan.FromSeconds(10);
  private string myAuth = null;
  internal GameData initialData = null;
  internal Root resultData = null;
  internal Player playerdata = null;
  internal bool isResultdone = false;
  internal bool SetInit = false;

  [Header("Extras")]
  [SerializeField] private GameObject RaycastBlocker;
  internal List<List<int>> LineData = null; //

  [Header("Ping Pong")]
  private bool isConnected = false; //Back2 Start.       //
  private bool hasEverConnected = false;          //
  private const int MaxReconnectAttempts = 5;     //
  private const float ReconnectDelaySeconds = 2f;     //

  private float lastPongTime = 0f;      //
  private float pingInterval = 2f;     //
  private bool waitingForPong = false;     //
  private int missedPongs = 0;            // 
  private const int MaxMissedPongs = 5;       //
  private Coroutine PingRoutine; //Back2 end       //

  private bool hasFocus = true;
  private float focusLostTime = 0f;
  private Coroutine focusCheckRoutine;
  private const float maxBackgroundTime = 60f;
  private bool isExiting = false;
  private bool isBeingDestroyed = false;

  private void Awake()
  {
    SetInit = false;
  }

  private void Start()
  {
    OpenSocket();
  }

  void ReceiveAuthToken(string jsonData)
  {
    Debug.Log("Received Auth Token Data: " + jsonData);
    // Parse the JSON data
    var data = JsonUtility.FromJson<AuthTokenData>(jsonData);
    SocketURI = data.socketURL;
    myAuth = data.cookie;
    NameSpace = data.nameSpace;
  }

  private void OpenSocket()
  {
    SocketOptions options = new SocketOptions(); //Back2 Start
    options.AutoConnect = false;
    options.Reconnection = false;
    options.Timeout = TimeSpan.FromSeconds(3); //Back2 end
    options.ConnectWith = Best.SocketIO.Transports.TransportTypes.WebSocket;

#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager.RegisterAuthTokenListener(gameObject.name); // listen for host's TokenReceived before asking
        JSManager.SendCustomMessage("authToken");
        StartCoroutine(WaitForAuthToken(options));
#else
    Func<SocketManager, Socket, object> authFunction = (manager, socket) =>
    {
      return new
      {
        token = TestToken
      };
    };
    options.Auth = authFunction;
    SetupSocketManager(options);
#endif
  }


  private IEnumerator WaitForAuthToken(SocketOptions options)
  {
    // Wait until myAuth is not null
    while (myAuth == null)
    {
      Debug.Log("My Auth is null");
      yield return null;
    }
    while (SocketURI == null)
    {
      Debug.Log("My Socket is null");
      yield return null;
    }
    Debug.Log("My Auth is not null");
    // Once myAuth is set, configure the authFunction
    Func<SocketManager, Socket, object> authFunction = (manager, socket) =>
    {
      return new
      {
        token = myAuth,
      };
    };
    options.Auth = authFunction;

    Debug.Log("Auth function configured with token: " + myAuth);

    // Proceed with connecting to the server
    SetupSocketManager(options);
  }

  private void SetupSocketManager(SocketOptions options)
  {
    // Create and setup SocketManager
#if UNITY_EDITOR
    // Debug.Log("yo-yo");
    this.manager = new SocketManager(new Uri(TestSocketURI), options);
#else
        this.manager = new SocketManager(new Uri(SocketURI), options);
#endif
    if (string.IsNullOrEmpty(NameSpace) | string.IsNullOrWhiteSpace(NameSpace))
    {
      gameSocket = this.manager.Socket;
    }
    else
    {
      Debug.Log("Namespace used :" + NameSpace);
      gameSocket = this.manager.GetSocket("/" + NameSpace);
    }
    // Set subscriptions
    gameSocket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
    gameSocket.On(SocketIOEventTypes.Disconnect, OnDisconnected); //Back2 Start
    gameSocket.On<Error>(SocketIOEventTypes.Error, OnError);
    gameSocket.On<string>("game:init", OnListenEvent);
    gameSocket.On<string>("result", OnListenEvent);
    gameSocket.On<bool>("socketState", OnSocketState);
    gameSocket.On<string>("internalError", OnSocketError);
    gameSocket.On<string>("alert", OnSocketAlert);
    gameSocket.On<string>("pong", OnPongReceived); //Back2 Start
    gameSocket.On<string>("AnotherDevice", OnSocketOtherDevice);
    gameSocket.On<string>("balance:sync", OnBalanceSync);

    manager.Open();
  }

  // Connected event handler implementation
  void OnConnected(ConnectResponse resp) //Back2 Start
  {
    Debug.Log("✅ Connected to server.");

    if (hasEverConnected)
    {
      uiManager.CheckAndClosePopups();
    }

    isConnected = true;
    hasEverConnected = true;
    waitingForPong = false;
    missedPongs = 0;
    lastPongTime = Time.time;
    SendPing();
  } //Back2 end  

  private void OnDisconnected() //Back2 Start
  {
    Debug.LogWarning("⚠️ Disconnected from server.");
    isConnected = false;
    uiManager.DisconnectionPopup();
    ResetPingRoutine();
  } //Back2 end

  private void OnPongReceived(string data) //Back2 Start
  {
    waitingForPong = false;
    missedPongs = 0;
    lastPongTime = Time.time;
  } //Back2 end

  internal void HandleFocusChange(bool focus)
  {
    hasFocus = focus;
    if (!focus)
    {
      focusLostTime = Time.time;
      if (focusCheckRoutine == null && !isExiting && !isBeingDestroyed)
        focusCheckRoutine = StartCoroutine(FocusTimeoutCheck());
    }
    else
    {
      if (focusCheckRoutine != null)
      {
        StopCoroutine(focusCheckRoutine);
        focusCheckRoutine = null;
      }
    }
  }

  private IEnumerator FocusTimeoutCheck()
  {
    while (!hasFocus && !isExiting && !isBeingDestroyed)
    {
      if (Time.time - focusLostTime >= maxBackgroundTime)
      {
        Debug.LogWarning("[SOCKET] Background timeout — closing connection");
        isConnected = false;
        ResetPingRoutine();

        if (manager != null)
        {
          try { manager.Close(); }
          catch (Exception e) { Debug.LogWarning($"[SOCKET] Focus close error: {e.Message}"); }
        }

        uiManager.DisconnectionPopup();
        focusCheckRoutine = null;
        yield break;
      }

      yield return new WaitForSecondsRealtime(1f);
    }

    focusCheckRoutine = null;
  }

  private void OnDestroy()
  {
    isBeingDestroyed = true;
  }

  private void OnError(Error err)
  {
    Debug.LogError("[ERROR] Socket error: " + err);
    if (!string.IsNullOrEmpty(err.message) && err.message.Contains("Session expired"))
    {
      Debug.LogWarning("Session expired detected");
      OnDisconnected();
#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("session_expired");
#endif
    }
    else
    {
#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("error");
#endif
    }
  }

  private void OnListenEvent(string data)
  {
    ParseResponse(data);
  }

  private void OnSocketState(bool state)
  {
    Debug.Log("Socket State: " + state);
  }

  private void OnSocketError(string data)
  {
    Debug.Log("Socket Error!: " + data);
  }

  private void OnSocketAlert(string data)
  {
    Debug.Log("Socket Alert!: " + data);
  }

  private void OnSocketOtherDevice(string data)
  {
    Debug.Log("Received Device Error with data: " + data);
    // _uiManager.ADfunction();
  }

  private void OnBalanceSync(string data)
  {
    BalanceSyncPayload syncPayload = JsonConvert.DeserializeObject<BalanceSyncPayload>(data);
    if (syncPayload == null) return;

    if (playerdata == null) playerdata = new Player();
    playerdata.balance = syncPayload.balance;

    uiManager.UpdateBalanceDisplay(syncPayload.balance);
  }

  private void SendPing() //Back2 Start
  {
    ResetPingRoutine();
    PingRoutine = StartCoroutine(PingCheck());
  }

  void ResetPingRoutine()
  {
    if (PingRoutine != null)
    {
      StopCoroutine(PingRoutine);
    }
    PingRoutine = null;
  }

  private IEnumerator PingCheck()
  {
    while (true)
    {
      if (missedPongs == 0)
      {
        uiManager.CheckAndClosePopups();
      }

      // If waiting for pong, and timeout passed
      if (waitingForPong)
      {
        if (missedPongs == 2)
        {
          uiManager.ReconnectionPopup();
        }
        missedPongs++;
        Debug.LogWarning($"⚠️ Pong missed #{missedPongs}/{MaxMissedPongs}");

        if (missedPongs >= MaxMissedPongs)
        {
          Debug.LogError("❌ Unable to connect to server — 5 consecutive pongs missed.");
          isConnected = false;
          uiManager.DisconnectionPopup();
          yield break;
        }
      }

      // Send next ping
      waitingForPong = true;
      lastPongTime = Time.time;
      // Debug.Log("📤 Sending ping...");
      SendDataWithNamespace("ping");
      yield return new WaitForSeconds(pingInterval);
    }
  } //Back2 end

  private void SendDataWithNamespace(string eventName, string json = null)
  {
    // Send the message
    if (gameSocket != null && gameSocket.IsOpen)
    {
      if (json != null)
      {
        gameSocket.Emit(eventName, json);
        Debug.Log("JSON data sent: " + json);
      }
      else
      {
        gameSocket.Emit(eventName);
      }
    }
    else
    {
      Debug.LogWarning("Socket is not connected.");
    }
  }

  void CloseGame()
  {
    Debug.Log("Unity: Closing Game");
    StartCoroutine(CloseSocket());
  }

  internal IEnumerator CloseSocket() //Back2 Start
  {
    isExiting = true;
    RaycastBlocker.SetActive(true);
    ResetPingRoutine();

    Debug.Log("Closing Socket");

    manager?.Close();
    manager = null;

    Debug.Log("Waiting for socket to close");

    yield return new WaitForSeconds(0.5f);

    Debug.Log("Socket Closed");

#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("OnExit"); //Telling the react platform user wants to quit and go back to homepage
#endif
  } //Back2 end

  private void ParseResponse(string jsonObject)
  {
    Debug.Log(jsonObject);
    Root myData = new();
    myData = JsonConvert.DeserializeObject<Root>(jsonObject);

    string id = myData.id;
    playerdata = myData.player;

    switch (id)
    {
      case "initData":
        {
          initialData = myData.gameData;

          if (!SetInit)
          {
            SetInit = true;
            InitialiseUIData();
          }
          break;
        }
      case "Result":
        {
          resultData = myData;
          isResultdone = true;
          break;
        }
      case "ExitUser":
        {
          if (this.manager != null)
          {
            Debug.Log("Dispose my Socket");
            gameSocket.Disconnect();
            this.manager.Close();
          }
#if UNITY_WEBGL && !UNITY_EDITOR
              JSManager.SendCustomMessage("OnExit"); // was "onExit" — host matches "OnExit"
#endif
          break;
        }
    }
  }

  private void InitialiseUIData()
  {
    uiManager.InitializeUIData();
    // uiManager.UpdateHotColdNumbers(initialData.hot_numbers,initialData.cold_numbers);
#if UNITY_WEBGL && !UNITY_EDITOR
      JSManager.SendCustomMessage("OnEnter");
#endif
    RaycastBlocker.SetActive(false);
  }

  internal void AccumulateResult(int playerBet, int bankerBet, int tieBet)
  {
    isResultdone = false;
    MessageData message = new MessageData();
    message.type = "DEAL";
    message.payload.playerBet = playerBet;
    message.payload.bankerBet = bankerBet;
    message.payload.tieBet = tieBet;

    // Serialize message data to JSON
    string json = JsonConvert.SerializeObject(message);
    SendDataWithNamespace("request", json);
  }
}

// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);

[Serializable]
public class AuthTokenData
{
  public string cookie;
  public string socketURL;
  public string nameSpace;
}

[Serializable]
public class MessageData
{
  public string type;
  public Data payload = new();
}

[Serializable]
public class Data
{
  public int tieBet { get; set; }
  public int playerBet { get; set; }
  public int bankerBet { get; set; }
}

// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);

[Serializable]
public class Root
{
  public bool success { get; set; }
  public string id { get; set; }
  public GameData gameData { get; set; }
  public Player player { get; set; }
  public Payload payload { get; set; }
}

[Serializable]
public class Player
{
  public double balance { get; set; }
}

[Serializable]
public class BalanceSyncPayload
{
  public double balance;
}

[Serializable]
public class GameData
{
  public List<int> bets { get; set; }
  public Limits limits { get; set; }
  public int deckCount { get; set; }
  public Paytable paytable { get; set; }
}

[Serializable]
public class Limits
{
  public int tieBet { get; set; }
  public int playerBet { get; set; }
  public int bankerBet { get; set; }
}

[Serializable]
public class Paytable
{
  public int playerWin { get; set; }
  public double bankerWin { get; set; }
  public int tie { get; set; }
  public int push { get; set; }
}

[Serializable]
public class Payload
{
  public PlayerHand playerHand { get; set; }
  public DealerHand dealerHand { get; set; }
  public string result { get; set; }
  public double winAmount { get; set; }
}

[Serializable]
public class PlayerHand
{
  public List<Card> cards { get; set; }
  public int value { get; set; }
  public List<int> values { get; set; }
}

[Serializable]
public class DealerHand
{
  public List<Card> cards { get; set; }
  public int value { get; set; }
  public List<int> values { get; set; }
}

[Serializable]
public class Card
{
  public string rank { get; set; }
  public string suit { get; set; }
}
