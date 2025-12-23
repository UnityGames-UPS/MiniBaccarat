using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("Bet Button")]
    [SerializeField] private Button PlayerBetButton;
    [SerializeField] private Button BankerBetButton;
    [SerializeField] private Button TieBetButton;
    [SerializeField] private Button DealButton;
    [SerializeField] private Button DoubleBetButton;
    [SerializeField] private Button UndoBetButton;
    [SerializeField] private Button ClearBetsButton;
    [SerializeField] private Button RebeDealButton;
    [SerializeField] private Button RebetButton;

    [Header("Card Sprites")]
    [SerializeField] private Sprite[] clubs_Sprite;
    [SerializeField] private Sprite[] spades_Sprite;
    [SerializeField] private Sprite[] hearts_Sprite;
    [SerializeField] private Sprite[] diamonds_Sprite;
    private readonly List<GameObject> bankeractiveCards = new();
    private readonly List<GameObject> playeractiveCards = new();
    private readonly List<GameObject> activeCards = new();

    private int dealerCounter = 0;
    private int playerCounter = 0;
    private bool cardAnim = false;

    [Header("Card Animation")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private RectTransform deckTransform;
    [SerializeField] private RectTransform playerCardContainer;
    [SerializeField] private RectTransform bankerCardContainer;
    [SerializeField] private RectTransform LeftBox_Transform;
    [SerializeField] private Sprite Empty_Sprite;

    [SerializeField] private Sprite[] cardSprites;

    private List<int> playerHand = new();
    private List<int> bankerHand = new();

    private bool isDealing = false;

    internal bool isFlippin = false;

    [Header("Managers")]
    [SerializeField] private BetManager betManager;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private SocketIOManager socketManager;
    [SerializeField] private AudioController audioController;

    private void Start()
    {
        if (PlayerBetButton != null) PlayerBetButton.onClick.AddListener(() => placebets(0));
        if (BankerBetButton != null) BankerBetButton.onClick.AddListener(() => placebets(1));
        if (TieBetButton != null) TieBetButton.onClick.AddListener(() => placebets(2));
        // if (DealButton != null) DealButton.onClick.AddListener(() => StartCoroutine(DealCards()));
        if (DealButton != null) DealButton.onClick.AddListener(() => OnDeal());
        if (DoubleBetButton != null) DoubleBetButton.onClick.AddListener(() => betManager.DoubleBet());
        if (UndoBetButton != null) UndoBetButton.onClick.AddListener(() => betManager.UndoLastBet());
        if (ClearBetsButton != null) ClearBetsButton.onClick.AddListener(() => betManager.ClearAllBets());
        if (RebeDealButton != null) RebeDealButton.onClick.AddListener(() => StartCoroutine(betManager.RebetAndDeal()));
        if (RebetButton != null) RebetButton.onClick.AddListener(() => betManager.Rebet());
    }

    private void Update()
    {

    }
    #region Main Card Dealing Logic

    internal void OnDeal()
    {

        uiManager.ToggleInitialBetButtons(false);
        uiManager.SetCoinButtonsInteractable(false);
        StartCoroutine(DealCards());
    }

    private IEnumerator DealCards()
    {
        betManager.SaveLastBets();
        socketManager.AccumulateResult(betManager.GetPlayerBet(), betManager.GetBankerBet(), betManager.GetTieBet());

        yield return new WaitUntil(() => socketManager.isResultdone);
        uiManager.UpdateBalanceText(socketManager.resultData.player.balance);

        var payload = socketManager.resultData.payload;

        yield return DealHands(payload.playerHand.cards, payload.dealerHand.cards);
        yield return new WaitUntil(() => !isDealing);

        yield return new WaitForSeconds(1f);
        uiManager.AddToHistory(payload.playerHand.value, payload.dealerHand.value);

        if (payload.winAmount > 0)
        {
            uiManager.UpdateWinAmountText(payload.winAmount);
        }

        yield return new WaitForSeconds(1f);

        StartCoroutine(betManager.PlayWinningAnimation());
        yield return new WaitForSeconds(5f);

        yield return StartCoroutine(CardCollectionAnim());
    }

    #endregion

    #region Helper logic
    private IEnumerator DealHands(List<Card> pcards, List<Card> bcards)
    {
        isDealing = true;
        int playerCardCount = pcards.Count;
        int bankerCardCount = bcards.Count;
        playerCounter = 0;
        dealerCounter = 0;
        for (int i = 0; i < Mathf.Max(playerCardCount, bankerCardCount); i++)
        {
            if (i > 1)
            {
                yield return new WaitForSeconds(1f);
            }
            if (i < playerCardCount)
            {
                isFlippin = true;
                OnPlayerDealCard(pcards[i].rank, pcards[i].suit);
                yield return new WaitUntil(() => !isFlippin);
                yield return new WaitForSeconds(0.1f);
            }

            if (i < bankerCardCount)
            {
                isFlippin = true;
                OnBankerDealCard(bcards[i].rank, bcards[i].suit);
                yield return new WaitUntil(() => !isFlippin);
                yield return new WaitForSeconds(0.1f);
            }

        }
        isDealing = false;
    }
    private Sprite GetCardSprite(string rank, string suit)
    {
        Sprite[] suitArray = suit switch
        {
            "clubs" => clubs_Sprite,
            "spades" => spades_Sprite,
            "hearts" => hearts_Sprite,
            "diamonds" => diamonds_Sprite,
            _ => null
        };

        int value = rank switch
        {
            "A" => 1,
            "2" => 2,
            "3" => 3,
            "4" => 4,
            "5" => 5,
            "6" => 6,
            "7" => 7,
            "8" => 8,
            "9" => 9,
            "10" => 10,
            "J" => 11,
            "Q" => 12,
            "K" => 13,
            _ => 0
        };

        return suitArray[value - 1];
    }

    internal void OnPlayerDealCard(string rank, string suit)
    {
        audioController.PlayCardPlaced();
        GameObject card = Instantiate(cardPrefab, deckTransform);
        playeractiveCards.Add(card);
        card.transform.localPosition = new Vector2(0, -30);
        card.transform.SetParent(playerCardContainer);

        // Sprite tempArr = SelectRandomArray(playerData[playerCounter]);
        Sprite tempArr = GetCardSprite(rank, suit);

        card.transform.localScale = Vector3.one * 0.95f;

        Sequence seq = DOTween.Sequence();

        seq.Append(card.transform.DOLocalMove(Vector2.zero, 0.4f).SetEase(Ease.OutCubic));

        seq.AppendInterval(0.05f);

        seq.OnComplete(() =>
        {
            card.GetComponent<CardScript>().OnFlipMethod(tempArr, 1);
        });
    }


    internal void OnBankerDealCard(string rank, string suit)
    {
        audioController.PlayCardPlaced();
        GameObject card = Instantiate(cardPrefab, deckTransform);
        bankeractiveCards.Add(card);
        card.transform.localPosition = new Vector2(0, -30);
        card.transform.SetParent(bankerCardContainer);

        // Sprite tempArr = SelectRandomArray(bankerData[dealerCounter]);
        Sprite tempArr = GetCardSprite(rank, suit);

        card.transform.localScale = Vector3.one * 0.95f;

        Sequence seq = DOTween.Sequence();

        seq.Append(card.transform.DOLocalMove(Vector2.zero, 0.4f).SetEase(Ease.OutCubic));

        seq.AppendInterval(0.05f);

        seq.OnComplete(() =>
        {
            card.GetComponent<CardScript>().OnFlipMethod(tempArr, 2);
        });
    }

    private IEnumerator CardCollectionAnim()
    {
        uiManager.PlayerCardValueUI_Object.SetActive(false);
        uiManager.BankerCardValueUI_Object.SetActive(false);
        yield return new WaitForSeconds(0.4f);

        yield return StartCoroutine(stackAllActiveCards());
        yield return new WaitUntil(() => !cardAnim);

        yield return StartCoroutine(FlipAllCardsToBackSide());
        yield return new WaitUntil(() => !cardAnim);

        yield return StartCoroutine(cardsToFinishPosition());
        yield return new WaitUntil(() => !cardAnim);

        Debug.Log("Collect Cards Done");
        yield return new WaitForSeconds(0.45f);

        foreach (GameObject card in activeCards)
            Destroy(card);
        activeCards.Clear();

        uiManager.ToggleReBetButtons(true);
        uiManager.SetCoinButtonsInteractable(true);

    }

    private IEnumerator stackAllActiveCards()
    {
        cardAnim = true;
        Vector3 targetPos = playerCardContainer.position;

        int index = 0;

        foreach (GameObject card in bankeractiveCards)
        {
            if (!card) continue;

            RectTransform rt = card.GetComponent<RectTransform>();


            Vector3 localTarget = new Vector3(0, index * 2f, 0);

            rt.DOMove(targetPos, 0.35f).SetEase(Ease.InCubic);
            activeCards.Add(card);
            index++;
        }
        index = 0;
        foreach (GameObject card in playeractiveCards)
        {
            if (!card) continue;

            RectTransform rt = card.GetComponent<RectTransform>();

            rt.DOMove(targetPos, 0.35f).SetEase(Ease.InCubic);
            activeCards.Add(card);
            index++;
        }
        yield return new WaitForSeconds(0.8f);
        playeractiveCards.Clear();
        bankeractiveCards.Clear();
        cardAnim = false;
    }

    private IEnumerator FlipAllCardsToBackSide()
    {
        cardAnim = true;
        foreach (GameObject card in activeCards)
        {

            Sprite csprite = Empty_Sprite;

            card.transform.localEulerAngles = new Vector3(0, 180, 0);

            Sequence flipSeq = DOTween.Sequence();

            flipSeq.Append(card.transform.DORotate(Vector3.zero, 0.45f).SetEase(Ease.OutCubic));

            DOVirtual.DelayedCall(0.1f, () =>
            {
                card.GetComponent<Image>().sprite = csprite;
            });
        }
        cardAnim = false;
        yield return new WaitForSeconds(0.1f);
    }

    private IEnumerator cardsToFinishPosition()
    {
        cardAnim = true;
        Vector3 targetPos = LeftBox_Transform.position;
        int index = 0;
        yield return new WaitForSeconds(0.5f);
        foreach (GameObject card in activeCards)
        {
            if (!card) continue;

            RectTransform rt = card.GetComponent<RectTransform>();

            rt.SetParent(LeftBox_Transform);

            Vector3 localTarget = new Vector3(0, index * 2f, 0);

            rt.DOMove(targetPos, 0.35f).SetEase(Ease.InCubic);


            index++;
        }
        cardAnim = false;
        // activeCards.Clear();
        yield return new WaitForSeconds(0.1f);
    }

    internal void AfterCardFlip(int value)
    {
        isFlippin = false;
        switch (value)
        {
            case 1:
                uiManager.PlayerCardValueUI_Object.SetActive(true);
                uiManager.PlayerCardValueText.text = socketManager.resultData.payload.playerHand.values[playerCounter].ToString();
                playerCounter++;
                break;
            case 2:
                uiManager.BankerCardValueUI_Object.SetActive(true);
                uiManager.BankerCardValueText.text = socketManager.resultData.payload.dealerHand.values[dealerCounter].ToString();
                dealerCounter++;
                break;
        }
    }

    private void placebets(int betType)
    {
        betManager.PlaceBet(betType);
        uiManager.ToggleInitialBetButtons(true);
    }

    #endregion
}