using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using System.Linq;

public class BetManager : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private SocketIOManager socketManager;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private AudioController audioController;

    [Header("References")]
    [SerializeField] private RectTransform chipRoot;
    [SerializeField] private ChipSelector chipSelector;
    [SerializeField] private RectTransform playerBetArea;
    [SerializeField] private RectTransform bankerBetArea;
    [SerializeField] private RectTransform tieBetArea;
    [SerializeField] private RectTransform winningChipArea;

    [Header("Limits")]
    [SerializeField] private int maxPlayerBet = 1000;
    [SerializeField] private int maxBankerBet = 1000;
    [SerializeField] private int maxTieBet = 100;

    [Header("Chip Values")]
    [SerializeField] private int[] chipValues = { 1, 5, 25, 100, 500, 1000 };

    private bool betsLocked = false;
    private List<BetEntry> betHistory = new();

    private List<int> playerBets = new();
    private List<int> bankerBets = new();
    private List<int> tieBets = new();
    private int totalBetAmount = 0;
    private List<GameObject> playerChips = new();
    private List<GameObject> bankerChips = new();
    private List<GameObject> tieChips = new();

    private List<int> winningChipValues = new();
    private List<GameObject> winningChips = new();
    private bool isSpawningWinnings = false;
    private bool collectingWinnings = false;

    // Snapshot of last round bets
    private List<int> lastPlayerBets = new();
    private List<int> lastBankerBets = new();
    private List<int> lastTieBets = new();


    [System.Serializable]
    public class BetEntry
    {
        public int betType;   // 0 Player, 1 Banker, 2 Tie
        public int chipValue;
    }

    #region Betting Logic

    internal void PlaceBet(int betType)           /// 0 = Player, 1 = Banker, 2 = Tie
    {
        if (betsLocked) return;

        int chipValue = (int)chipSelector.GetSelectedChipValue();
        if (chipValue <= 0) return;

        switch (betType)
        {
            case 0:
                AddBet(playerBets, playerChips, playerBetArea, chipValue, maxPlayerBet);
                break;
            case 1:
                AddBet(bankerBets, bankerChips, bankerBetArea, chipValue, maxBankerBet);
                break;
            case 2:
                AddBet(tieBets, tieChips, tieBetArea, chipValue, maxTieBet);
                break;
        }

        betHistory.Add(new BetEntry
        {
            betType = betType,
            chipValue = chipValue
        });
    }

    internal void DoubleBet()
    {
        if (betsLocked) return;

        audioController.PlayUIButton();
        // Copy current bets (important to avoid modifying while iterating)
        var playerCopy = new List<int>(playerBets);
        var bankerCopy = new List<int>(bankerBets);
        var tieCopy = new List<int>(tieBets);

        foreach (int value in playerCopy)
        {
            TryReAddBet(0, value);
        }

        foreach (int value in bankerCopy)
        {
            TryReAddBet(1, value);
        }

        foreach (int value in tieCopy)
        {
            TryReAddBet(2, value);
        }
    }


    internal void UndoLastBet()
    {
        if (betsLocked) return;
        if (betHistory.Count == 0) return;

        audioController.PlayUIButton();
        BetEntry last = betHistory[betHistory.Count - 1];
        betHistory.RemoveAt(betHistory.Count - 1);

        switch (last.betType)
        {
            case 0:
                RemoveLast(playerBets, playerChips);
                break;
            case 1:
                RemoveLast(bankerBets, bankerChips);
                break;
            case 2:
                RemoveLast(tieBets, tieChips);
                break;
        }
        if (betHistory.Count == 0)
        {
            uiManager.ToggleInitialBetButtons(false);
            if (uiManager.betPlacedOnce == true)
            {
                uiManager.ToggleReBetButtons(true);
            }
        }
    }

    internal void ClearAllBets()
    {
        audioController.PlayUIButton();
        ClearArea(playerBets, playerChips);
        ClearArea(bankerBets, bankerChips);
        ClearArea(tieBets, tieChips);
        betHistory.Clear();
        betsLocked = false;
        uiManager.ToggleInitialBetButtons(false);
        if (uiManager.betPlacedOnce == true)
        {
            uiManager.ToggleReBetButtons(true);
        }
    }

    internal void Rebet()
    {
        if (betsLocked) return;
        ClearAllBets();

        audioController.PlayUIButton();
        foreach (int value in lastPlayerBets)
            AddBet(playerBets, playerChips, playerBetArea, value, maxPlayerBet);

        foreach (int value in lastBankerBets)
            AddBet(bankerBets, bankerChips, bankerBetArea, value, maxBankerBet);

        foreach (int value in lastTieBets)
            AddBet(tieBets, tieChips, tieBetArea, value, maxTieBet);

        uiManager.ToggleInitialBetButtons(true);
    }

    internal IEnumerator RebetAndDeal()
    {
        audioController.PlayUIButton();
        Rebet();
        yield return new WaitForSeconds(0.4f);
        gameManager.OnDeal();
    }

    internal void LockBets()
    {
        betsLocked = true;
    }
    internal int GetPlayerBet()
    {
        return playerBets.Sum();
    }
    internal int GetBankerBet()
    {
        return bankerBets.Sum();
    }
    internal int GetTieBet()
    {
        return tieBets.Sum();
    }

    #endregion

    #region Helper Methods

    private void AddBet(List<int> betList, List<GameObject> chipList, RectTransform area, int value, int maxLimit)
    {
        if (value > uiManager.currentBalance)
        {
            uiManager.LowBalPopup();
            return;
        }
        if (betList.Sum() + value > maxLimit)
            return;

        audioController.PlayChip();
        DestroyWinningChips();
        betList.Add(value);
        totalBetAmount += value;
        uiManager.UpdateBetAmountText(totalBetAmount);

        var chipPrefab = chipSelector.GetChipByValue(value).ChipPreab;
        GameObject chip = Instantiate(chipPrefab, chipRoot);
        chip.transform.localScale = Vector3.one;

        AnimateChipToBetArea(chip, area, chipList.Count);

        chipList.Add(chip);

        OptimizeStack(betList, chipList, area);
    }


    private void OptimizeStack(List<int> betList, List<GameObject> chipList, RectTransform area)
    {
        for (int i = 0; i < chipValues.Length - 1; i++)
        {
            int small = chipValues[i];
            int large = chipValues[i + 1];
            int needed = large / small;

            if (betList.Count(v => v == small) >= needed)
            {
                for (int k = 0; k < needed; k++)
                {
                    int idx = betList.LastIndexOf(small);
                    Destroy(chipList[idx]);
                    chipList.RemoveAt(idx);
                    betList.RemoveAt(idx);
                }

                betList.Add(large);
                var prefab = chipSelector.GetChipByValue(large).ChipPreab;
                GameObject chip = Instantiate(prefab, area);
                chip.transform.localScale = Vector3.zero;
                chip.transform.localPosition = new Vector3(0, chipList.Count * 3f, 0);

                chip.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack);

                chipList.Add(chip);

                OptimizeStack(betList, chipList, area);
                return;
            }
        }
        RearrangeStack(betList, chipList);
    }

    private void AnimateChipToBetArea(GameObject chip, RectTransform area, int stackIndex)
    {
        RectTransform chipRT = chip.GetComponent<RectTransform>();

        chipRT.position = chipRoot.position;

        Vector3 targetLocalPos = new Vector3(Random.Range(-4f, 4f), stackIndex * 3f, 0);

        chipRT.SetParent(area, true);

        Sequence seq = DOTween.Sequence();

        seq.Append(chipRT.DOScale(1.2f, 0.15f));

        seq.Append(chipRT.DOLocalMove(targetLocalPos, 0.3f).SetEase(Ease.OutCubic));

        seq.Append(chipRT.DOScale(0.95f, 0.08f));

        seq.Append(chipRT.DOScale(1f, 0.06f));
    }

    private void TryReAddBet(int betType, int chipValue)
    {
        switch (betType)
        {
            case 0:
                AddBet(playerBets, playerChips, playerBetArea, chipValue, maxPlayerBet);
                break;

            case 1:
                AddBet(bankerBets, bankerChips, bankerBetArea, chipValue, maxBankerBet);
                break;

            case 2:
                AddBet(tieBets, tieChips, tieBetArea, chipValue, maxTieBet);
                break;
        }

        betHistory.Add(new BetEntry
        {
            betType = betType,
            chipValue = chipValue
        });
    }

    private void RemoveLast(List<int> betList, List<GameObject> chipList)
    {
        if (betList.Count == 0 || chipList.Count == 0)
        {
            // Safety reset to avoid desync crashes
            betList.Clear();
            chipList.Clear();
            totalBetAmount = 0;
            uiManager.UpdateBetAmountText(totalBetAmount);
            return;
        }

        int lastIndex = chipList.Count - 1;

        Destroy(chipList[lastIndex]);
        chipList.RemoveAt(lastIndex);

        totalBetAmount -= betList[betList.Count - 1];
        betList.RemoveAt(betList.Count - 1);

        uiManager.UpdateBetAmountText(totalBetAmount);
    }


    private void ClearArea(List<int> betList, List<GameObject> chipList)
    {
        foreach (var chip in chipList)
            Destroy(chip);

        betList.Clear();
        chipList.Clear();
        totalBetAmount = 0;
        uiManager.UpdateBetAmountText(totalBetAmount);
    }

    private void RearrangeStack(List<int> betList, List<GameObject> chipList)
    {
        int count = Mathf.Min(betList.Count, chipList.Count);
        if (count == 0) return;

        var paired = new List<(int value, GameObject chip)>(count);

        for (int i = 0; i < count; i++)
            paired.Add((betList[i], chipList[i]));

        paired = paired.OrderByDescending(p => p.value).ToList();

        betList.Clear();
        chipList.Clear();

        for (int i = 0; i < paired.Count; i++)
        {
            betList.Add(paired[i].value);
            chipList.Add(paired[i].chip);

            RectTransform rt = paired[i].chip.GetComponent<RectTransform>();
            rt.DOLocalMove(new Vector3(Random.Range(-2f, 2f), i * 3f, 0), 0.25f)
              .SetEase(Ease.OutCubic);

            rt.SetSiblingIndex(i);
        }
    }

    internal void SaveLastBets()
    {
        lastPlayerBets = new List<int>(playerBets);
        lastBankerBets = new List<int>(bankerBets);
        lastTieBets = new List<int>(tieBets);
    }

    #endregion

    #region Winning Chips Animation

    internal IEnumerator PlayWinningAnimation()
    {
        int player = socketManager.resultData.payload.playerHand.value;
        int banker = socketManager.resultData.payload.dealerHand.value;

        int totalBet = GetPlayerBet() + GetBankerBet() + GetTieBet();
        int winAmount = (int)socketManager.resultData.payload.winAmount;
        // int profit = (winAmount - totalBet);

        if (player > banker)
        {
            int profit = winAmount - GetPlayerBet();
            yield return PlayerWin(profit);
        }

        else if (banker > player)
        {
            yield return BankerWin();
        }
        else
        {
            int profit = winAmount - GetTieBet();
            yield return TieWin(profit);
        }
    }

    private IEnumerator PlayerWin(int profit)
    {
        audioController.PlayPlayerWins();
        yield return new WaitForSeconds(1f);

        yield return SpawnProfitChips(profit, playerBetArea, playerChips, playerBets);
        yield return new WaitUntil(() => !isSpawningWinnings);

        Debug.Log("Spawning done");
        yield return new WaitForSeconds(1.5f);

        yield return CollectChips(playerChips);
        yield return new WaitUntil(() => !collectingWinnings);

        Debug.Log("Collecting done");
        yield return new WaitForSeconds(1.5f);

        DestroyChipList(bankerChips);
        DestroyChipList(tieChips);
    }

    private IEnumerator BankerWin()
    {
        audioController.PlayBankerWins();
        yield return new WaitForSeconds(2f);
        DestroyChipList(playerChips);
        DestroyChipList(bankerChips);
        DestroyChipList(tieChips);
        yield break;
    }

    private IEnumerator TieWin(int profit)
    {
        audioController.PlayGameTie();
        yield return new WaitForSeconds(1f);

        yield return SpawnProfitChips(profit, tieBetArea, tieChips, tieBets);
        yield return new WaitUntil(() => !isSpawningWinnings);

        Debug.Log("Spawning done");
        yield return new WaitForSeconds(1f);

        yield return CollectChips(tieChips);
        yield return new WaitUntil(() => !collectingWinnings);

        Debug.Log("Collecting done");
        yield return new WaitForSeconds(1f);

        yield return CollectChips(bankerChips);
        yield return new WaitUntil(() => !collectingWinnings);

        yield return new WaitForSeconds(1f);

        yield return CollectChips(playerChips);
        yield return new WaitUntil(() => !collectingWinnings);

    }

    private IEnumerator SpawnProfitChips(int amount, RectTransform targetArea, List<GameObject> chipList, List<int> betList)
    {
        isSpawningWinnings = true;
        if (amount <= 0) yield break;

        var chips = BreakIntoChips(amount);

        foreach (int value in chips)
        {
            AddWins(betList, chipList, targetArea, value);
        }
        isSpawningWinnings = false;
        yield return new WaitForSeconds(0.5f);
    }

    private void AddWins(List<int> betList, List<GameObject> chipList, RectTransform area, int value)
    {
        audioController.PlayChip();
        betList.Add(value);

        var chipPrefab = chipSelector.GetChipByValue(value).ChipPreab;
        GameObject chip = Instantiate(chipPrefab, chipRoot);
        chip.transform.localScale = Vector3.one;

        AnimateChipToBetArea(chip, area, chipList.Count);

        chipList.Add(chip);

        OptimizeStack(betList, chipList, area);
    }

    private IEnumerator CollectChips(List<GameObject> chips)
    {
        collectingWinnings = true;
        Debug.Log("Chip Count: " + chips.Count);
        for (int i = 0; i < chips.Count; i++)
        {
            GameObject chip = chips[i];
            if (!chip) continue;

            RectTransform rt = chip.GetComponent<RectTransform>();

            rt.SetParent(winningChipArea, true);

            Vector3 targetLocalPos = new Vector3(0, i * 3f, 0);

            Sequence seq = DOTween.Sequence();
            seq.Append(rt.DOScale(1.1f, 0.12f));
            seq.Append(rt.DOLocalMove(targetLocalPos, 0.35f).SetEase(Ease.InCubic));
            seq.Append(rt.DOScale(1f, 0.1f));

            winningChips.Add(chip);

            // yield return new WaitUntil(() => seq.IsComplete());
        }
        chips.Clear();
        collectingWinnings = false;
        uiManager.UpdateWinningAreaText(socketManager.resultData.payload.winAmount.ToString());
        yield return new WaitForSeconds(0.5f);
    }

    private void DestroyChipList(List<GameObject> chips)
    {
        if (chips.Count == 0) return;
        foreach (var chip in chips)
            Destroy(chip);

        chips.Clear();
    }


    private List<int> BreakIntoChips(int amount)
    {
        List<int> result = new();

        for (int i = chipValues.Length - 1; i >= 0; i--)
        {
            while (amount >= chipValues[i])
            {
                amount -= chipValues[i];
                result.Add(chipValues[i]);
            }
        }
        return result;
    }

    internal void DestroyWinningChips()
    {
        DestroyChipList(winningChips);
        uiManager.WinningAreaText.gameObject.SetActive(false);
    }

    #endregion

}
