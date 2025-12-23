using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System;
using DG.Tweening;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject initialBetButtons;
    [SerializeField] private GameObject ReBetButtons;
    [SerializeField] private Button[] coinButtons;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI BalanceText;
    [SerializeField] private TextMeshProUGUI BetAmountText;
    [SerializeField] private TextMeshProUGUI WinAmountText;
    [SerializeField] private TextMeshProUGUI playerMaximunText;
    [SerializeField] private TextMeshProUGUI bankerMaximunText;
    [SerializeField] private TextMeshProUGUI tieMaximunText;
    // [SerializeField] private TextMeshProUGUI MaximumText;
    [SerializeField] internal TextMeshProUGUI WinningAreaText;

    [Header("History UI")]
    [SerializeField] private RectTransform HistoryContentParent;
    [SerializeField] private GameObject HistoryItemPrefab;
    [SerializeField] private Sprite WhiteCircleSprite;
    [SerializeField] private Sprite RedCircleSprite;

    [Header("CardValue UI")]
    [SerializeField] internal GameObject PlayerCardValueUI_Object;
    [SerializeField] internal TextMeshProUGUI PlayerCardValueText;
    [SerializeField] internal GameObject BankerCardValueUI_Object;
    [SerializeField] internal TextMeshProUGUI BankerCardValueText;

    [Header("Main Popus UI Object")]
    [SerializeField] private GameObject MainPopup_Object;

    [Header("Settings Popup")]
    [SerializeField] private GameObject SettingsPopup_Object;
    [SerializeField] private Button Settings_Button;

    [SerializeField] private Button SettingsExit_Button;

    [Header("Info Popup")]
    [SerializeField] private GameObject InfoPopup_Object;
    [SerializeField] private Button Info_Button;
    [SerializeField] private Button InfoExit_Button;

    [Header("Quit Popup UI References")]
    [SerializeField] private GameObject QuitPopup_Object;
    [SerializeField] private Button YesQuit_Button;
    [SerializeField] private Button NoQuit_Button;
    [SerializeField] private Button GameExit_Button;

    [Header("Disconnection Popup")]
    [SerializeField] private Button CloseDisconnect_Button;
    [SerializeField] private GameObject DisconnectPopup_Object;

    [Header("Reconnection Popup")]
    [SerializeField] private GameObject ReconnectPopup_Object;

    [Header("LowBalance Popup")]
    [SerializeField] private Button LBExit_Button;
    [SerializeField] private GameObject LBPopup_Object;

    [Header("Managers")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SocketIOManager socketManager;
    [SerializeField] private BetManager betManager;
    // [SerializeField] private AudioController audioController;
    internal bool betPlacedOnce = false;
    private bool isExit = false;
    internal double currentBalance = 0;

    private void Start()
    {
        if (LBExit_Button) LBExit_Button.onClick.RemoveAllListeners();
        if (LBExit_Button) LBExit_Button.onClick.AddListener(delegate { ClosePopup(LBPopup_Object); });

        if (YesQuit_Button) YesQuit_Button.onClick.RemoveAllListeners();
        if (YesQuit_Button) YesQuit_Button.onClick.AddListener(delegate
        {
            CallOnExitFunction();
        });

        if (CloseDisconnect_Button) CloseDisconnect_Button.onClick.RemoveAllListeners();
        if (CloseDisconnect_Button) CloseDisconnect_Button.onClick.AddListener(CallOnExitFunction);

        if (GameExit_Button) GameExit_Button.onClick.RemoveAllListeners();
        if (GameExit_Button) GameExit_Button.onClick.AddListener(delegate
        {
            OpenPopup(QuitPopup_Object);
        });

        if (NoQuit_Button) NoQuit_Button.onClick.RemoveAllListeners();
        if (NoQuit_Button) NoQuit_Button.onClick.AddListener(delegate
        {
            if (!isExit)
            {
                ClosePopup(QuitPopup_Object);
            }
        });

        if (Settings_Button) Settings_Button.onClick.RemoveAllListeners();
        if (Settings_Button) Settings_Button.onClick.AddListener(delegate
        {
            OpenPopup(SettingsPopup_Object);
        });

        if (SettingsExit_Button) SettingsExit_Button.onClick.RemoveAllListeners();
        if (SettingsExit_Button) SettingsExit_Button.onClick.AddListener(delegate
        {
            ClosePopup(SettingsPopup_Object);
        });

        if (Info_Button) Info_Button.onClick.RemoveAllListeners();
        if (Info_Button) Info_Button.onClick.AddListener(delegate
        {
            OpenPopup(InfoPopup_Object);
        });

        if (InfoExit_Button) InfoExit_Button.onClick.RemoveAllListeners();
        if (InfoExit_Button) InfoExit_Button.onClick.AddListener(delegate
        {
            ClosePopup(InfoPopup_Object);
        });
        ToggleReBetButtons(false);
    }

    internal void ToggleInitialBetButtons(bool state)
    {
        initialBetButtons.SetActive(state);
        if (state == true)
        {
            ReBetButtons.SetActive(false);
        }
    }

    internal void ToggleReBetButtons(bool state)
    {
        ReBetButtons.SetActive(state);
        if (state == true)
        {
            betPlacedOnce = true;
            initialBetButtons.SetActive(false);
        }
    }

    internal void SetCoinButtonsInteractable(bool state)
    {
        foreach (Button button in coinButtons)
        {
            button.interactable = state;
        }
    }

    internal void InitializeUIData()
    {
        currentBalance = socketManager.playerdata.balance;
        BalanceText.text = socketManager.playerdata.balance.ToString("N2");
        playerMaximunText.text = socketManager.initialData.limits.playerBet.ToString();
        bankerMaximunText.text = socketManager.initialData.limits.bankerBet.ToString();
        tieMaximunText.text = socketManager.initialData.limits.tieBet.ToString();
    }

    internal void UpdateBalanceText(double newBalance)
    {
        currentBalance = newBalance;
        BalanceText.text = newBalance.ToString("N2");
    }

    internal void UpdateBetAmountText(int newBetAmount)
    {
        BetAmountText.text = newBetAmount.ToString("N2");
    }

    internal void UpdateWinAmountText(double newWinAmount)
    {
        WinAmountText.text = newWinAmount.ToString("N2");
    }

    internal void AddToHistory(int phvalue, int bhvalue)
    {
        int winner = 0;
        if (phvalue > bhvalue)
        {
            winner = 0;
        }
        else if (bhvalue > phvalue)
        {
            winner = 1;
        }
        else
        {
            winner = 2;
        }
        GameObject historyItem = Instantiate(HistoryItemPrefab, HistoryContentParent);
        historyItem.transform.SetAsFirstSibling();

        Image[] images = historyItem.GetComponentsInChildren<Image>();
        Image LeftImage = images[0];
        Image RightImage = images[1];

        TextMeshProUGUI playerCardText = LeftImage.transform.Find("PlayerCardValueText").GetComponentInChildren<TextMeshProUGUI>();

        TextMeshProUGUI bankerCardText = RightImage.transform.Find("BankerCardValueText").GetComponentInChildren<TextMeshProUGUI>();

        LeftImage.color = Color.white;
        RightImage.color = Color.white;

        if (winner == 0)
        {
            LeftImage.sprite = WhiteCircleSprite;
            LeftImage.color = Color.blue;

            RightImage.sprite = WhiteCircleSprite;
        }
        else if (winner == 1)
        {
            LeftImage.sprite = WhiteCircleSprite;
            RightImage.sprite = RedCircleSprite;
        }
        else
        {
            LeftImage.sprite = WhiteCircleSprite;
            RightImage.sprite = WhiteCircleSprite;

            LeftImage.color = Color.green;
            RightImage.color = Color.green;
        }

        playerCardText.text = phvalue.ToString();
        bankerCardText.text = bhvalue.ToString();
    }

    internal void UpdateWinningAreaText(string winAmount)
    {
        WinningAreaText.gameObject.SetActive(true);
        WinningAreaText.text = winAmount;
    }

    internal void CheckAndClosePopups()
    {
        if (ReconnectPopup_Object.activeInHierarchy)
        {
            ClosePopup(ReconnectPopup_Object);
        }
        if (DisconnectPopup_Object.activeInHierarchy)
        {
            ClosePopup(DisconnectPopup_Object);
        }
    }

    private void CallOnExitFunction()
    {
        isExit = true;
        // audioController.PlayButtonAudio();
        StartCoroutine(socketManager.CloseSocket());
    }

    internal void LowBalPopup()
    {
        OpenPopup(LBPopup_Object);
    }

    internal void DisconnectionPopup()
    {
        if (!isExit)
        {
            isExit = true;
            OpenPopup(DisconnectPopup_Object);
        }
    }
    internal void ReconnectionPopup()
    {
        OpenPopup(ReconnectPopup_Object);
    }

    private void OpenPopup(GameObject Popup)
    {
        // if (audioController) audioController.PlayButtonAudio();
        if (Popup) Popup.SetActive(true);
        if (MainPopup_Object) MainPopup_Object.SetActive(true);
    }

    private void ClosePopup(GameObject Popup)
    {
        // if (audioController) audioController.PlayButtonAudio();
        if (Popup) Popup.SetActive(false);
        if (!DisconnectPopup_Object.activeSelf)
        {
            if (MainPopup_Object) MainPopup_Object.SetActive(false);
        }
    }

}