using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using System.Linq;
using TMPro;

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
    [SerializeField] internal int maxPlayerBet = 1000;
    [SerializeField] internal int maxBankerBet = 1000;
    [SerializeField] internal int maxTieBet = 100;

    [Header("Chip Values")]
    [SerializeField] internal int[] chipValues = {};

    private bool betsLocked = false;
    private bool rebetDone = false;

    // Current round bets
    private List<int> playerBets = new();
    private List<int> bankerBets = new();
    private List<int> tieBets = new();

    // Chip GameObjects
    private List<GameObject> playerChips = new();
    private List<GameObject> bankerChips = new();
    private List<GameObject> tieChips = new();

    // Bet history tracking
    private List<BetHistoryEntry> betHistory = new();

    // Snapshot of last round bets
    private List<int> lastPlayerBets = new();
    private List<int> lastBankerBets = new();
    private List<int> lastTieBets = new();

    // Winning chips
    private List<GameObject> winningChips = new();
    private bool isSpawningWinnings = false;
    private bool collectingWinnings = false;

    [System.Serializable]
    public class BetHistoryEntry
    {
        public int betType; // 0 = Player, 1 = Banker, 2 = Tie
        public int chipValue;

        public BetHistoryEntry(int type, int value)
        {
            betType = type;
            chipValue = value;
        }
    }


    #region Betting Logic

    internal void PlaceBet(int betType) // 0 = Player, 1 = Banker, 2 = Tie
    {
        if (betsLocked) return;

        int chipValue = (int)chipSelector.GetSelectedChipValue();
        if (chipValue <= 0) return;

        // Check balance properly
        int currentTotalBet = GetTotalCurrentBet();
        if (currentTotalBet + chipValue > uiManager.currentBalance)
        {
            uiManager.LowBalPopup();
            return;
        }

        if (betType == 0 && chipValue > maxPlayerBet)
        {
            return;
        }
        if (betType == 1 && chipValue > maxBankerBet)
        {
            return;
        }
        if (betType == 2 && chipValue > maxTieBet)
        {
            return;
        }

        uiManager.ToggleInitialBetButtons(true);

        // Add to history BEFORE optimization
        betHistory.Add(new BetHistoryEntry(betType, chipValue));

        switch (betType)
        {
            case 0:
                AddBet(playerBets, playerChips, playerBetArea, chipValue, maxPlayerBet, 0);
                break;
            case 1:
                AddBet(bankerBets, bankerChips, bankerBetArea, chipValue, maxBankerBet, 1);
                break;
            case 2:
                AddBet(tieBets, tieChips, tieBetArea, chipValue, maxTieBet, 2);
                break;
        }

        UpdateTotalBetDisplay();
    }

    internal void DoubleBet()
    {
        if (betsLocked) return;

        int currentTotal = GetTotalCurrentBet();

        // Check if we can afford to double
        if (currentTotal * 2 > uiManager.currentBalance)
        {
            uiManager.LowBalPopup();
            return;
        }

        audioController.PlayUIButton();

        // Make a copy of current history to double
        var historyCopy = new List<BetHistoryEntry>(betHistory);

        foreach (var entry in historyCopy)
        {
            // if(entry.betType == 0 && entry.chipValue + GetPlayerBet() > maxPlayerBet)
            // {
            //     return;
            // }
            // if(entry.betType == 1 && entry.chipValue + GetBankerBet() > maxBankerBet)
            // {
            //     return;
            // }
            // if(entry.betType == 2 && entry.chipValue + GetTieBet() > maxTieBet)
            // {
            //     return;
            // }
            betHistory.Add(new BetHistoryEntry(entry.betType, entry.chipValue));

            switch (entry.betType)
            {
                case 0:
                    if (entry.chipValue + GetPlayerBet() > maxPlayerBet)
                    {
                        break;
                    }
                    AddBet(playerBets, playerChips, playerBetArea, entry.chipValue, maxPlayerBet, 0);
                    break;
                case 1:
                    if (entry.chipValue + GetBankerBet() > maxBankerBet)
                    {
                        break;
                    }
                    AddBet(bankerBets, bankerChips, bankerBetArea, entry.chipValue, maxBankerBet, 1);
                    break;
                case 2:
                    if (entry.chipValue + GetTieBet() > maxTieBet)
                    {
                        break;
                    }
                    AddBet(tieBets, tieChips, tieBetArea, entry.chipValue, maxTieBet, 2);
                    break;
            }
        }

        UpdateTotalBetDisplay();
    }

    internal void UndoLastBet()
    {
        if (betsLocked) return;
        if (betHistory.Count == 0) return;

        audioController.PlayUIButton();

        // Get the last bet from history
        BetHistoryEntry lastBet = betHistory[betHistory.Count - 1];
        betHistory.RemoveAt(betHistory.Count - 1);

        // Remove from the appropriate area
        switch (lastBet.betType)
        {
            case 0:
                RemoveSpecificBet(playerBets, playerChips, lastBet.chipValue);
                break;
            case 1:
                RemoveSpecificBet(bankerBets, bankerChips, lastBet.chipValue);
                break;
            case 2:
                RemoveSpecificBet(tieBets, tieChips, lastBet.chipValue);
                break;
        }

        UpdateTotalBetDisplay();

        // Check if all bets are cleared
        if (playerChips.Count == 0 && bankerChips.Count == 0 && tieChips.Count == 0)
        {
            uiManager.ToggleInitialBetButtons(false);
            if (uiManager.betPlacedOnce)
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
        UpdateTotalBetDisplay();

        uiManager.ToggleInitialBetButtons(false);
        if (uiManager.betPlacedOnce)
        {
            uiManager.ToggleReBetButtons(true);
        }
    }

    internal void Rebet(bool isrebet = false)
    {
        audioController.PlayUIButton();
        if (betsLocked) return;

        ClearAllBets();

        int lastTotal = lastPlayerBets.Sum() + lastBankerBets.Sum() + lastTieBets.Sum();
        if (lastTotal > uiManager.currentBalance)
        {
            uiManager.LowBalPopup();
            return;
        }


        // Rebuild history from last bets
        foreach (int value in lastPlayerBets)
        {
            betHistory.Add(new BetHistoryEntry(0, value));
            AddBet(playerBets, playerChips, playerBetArea, value, maxPlayerBet, 0);
        }
        foreach (int value in lastBankerBets)
        {
            betHistory.Add(new BetHistoryEntry(1, value));
            AddBet(bankerBets, bankerChips, bankerBetArea, value, maxBankerBet, 1);
        }
        foreach (int value in lastTieBets)
        {
            betHistory.Add(new BetHistoryEntry(2, value));
            AddBet(tieBets, tieChips, tieBetArea, value, maxTieBet, 2);
        }

        rebetDone = true;

        UpdateTotalBetDisplay();
        if (isrebet)
        {
            uiManager.ToggleInitialBetButtons(true);
        }
    }

    internal IEnumerator RebetAndDeal()
    {
        audioController.PlayUIButton();
        rebetDone = false;
        Rebet();
        if (rebetDone)
        {
            gameManager.OnDeal();
        }
        yield return new WaitForSeconds(0.4f);
    }

    internal void LockBets()
    {
        betsLocked = true;
    }

    internal int GetPlayerBet() => playerBets.Sum();
    internal int GetBankerBet() => bankerBets.Sum();
    internal int GetTieBet() => tieBets.Sum();

    internal int GetTotalCurrentBet() => GetPlayerBet() + GetBankerBet() + GetTieBet();

    internal void ApplyChipDenominations(List<int> bets)
    {
        if (bets == null || bets.Count == 0)
        {
            Debug.LogWarning("ApplyChipDenominations: backend bets list is null/empty, keeping inspector chipValues.");
            return;
        }

        chipSelector.ApplyChipValues(bets);
        chipValues = bets.OrderBy(v => v).ToArray();
    }

    #endregion

    #region Helper Methods

    private void AddBet(List<int> betList, List<GameObject> chipList, RectTransform area, int value, int maxLimit, int betType)
    {
        // Check max limit for this specific bet area
        if (betList.Sum() + value > maxLimit) return;

        audioController.PlayChip();
        DestroyWinningChips();

        betList.Add(value);

        var chipPrefab = chipSelector.GetChipByValue(value).ChipPreab;
        GameObject chip = Instantiate(chipPrefab, chipRoot);
        chip.transform.localScale = Vector3.one;
        var chipText = chip.GetComponentInChildren<TextMeshProUGUI>();
        if (chipText != null) chipText.text = value.ToString();

        AnimateChipToBetArea(chip, area, chipList.Count);
        chipList.Add(chip);

        OptimizeStack(betList, chipList, area);
    }

    private void OptimizeStack(List<int> betList, List<GameObject> chipList, RectTransform area)
    {
        // Recompute the canonical largest-chips-first breakdown of the running total
        // (same greedy decomposition used elsewhere, e.g. for winning chips) and only
        // rebuild the visual stack when it actually differs from the current one -
        // this correctly merges any combination that sums to a larger denomination
        // (e.g. 5x100 -> one 500, or 2x100+1x50 -> one 250), not just same-value stacks.
        int total = betList.Sum();
        var optimal = BreakIntoChips(total);

        if (betList.OrderBy(v => v).SequenceEqual(optimal.OrderBy(v => v))) return;

        betList.Clear();
        betList.AddRange(optimal);
        RebuildChipsVisually(betList, chipList, area);
    }

    private void AnimateChipToBetArea(GameObject chip, RectTransform area, int stackIndex)
    {
        RectTransform chipRT = chip.GetComponent<RectTransform>();
        chipRT.position = chipRoot.position;

        Vector3 targetLocalPos = new Vector3(0, stackIndex * 3f, 0);
        chipRT.SetParent(area, true);

        Sequence seq = DOTween.Sequence();
        seq.Append(chipRT.DOScale(1.2f, 0.15f));
        seq.Append(chipRT.DOLocalMove(targetLocalPos, 0.3f).SetEase(Ease.OutCubic));
        seq.Append(chipRT.DOScale(0.95f, 0.08f));
        seq.Append(chipRT.DOScale(1f, 0.06f));
    }

    private void RemoveSpecificBet(List<int> betList, List<GameObject> chipList, int chipValue)
    {
        // Find the chip value in the bet list
        int index = betList.LastIndexOf(chipValue);

        if (index == -1)
        {
            // The chip was optimized into a larger chip, we need to break it down
            BreakDownChipForRemoval(betList, chipList, chipValue);
            return;
        }

        // Remove the chip
        if (index >= 0 && index < chipList.Count)
        {
            if (chipList[index] != null)
            {
                Destroy(chipList[index]);
            }
            chipList.RemoveAt(index);
            betList.RemoveAt(index);
        }
    }

    private void BreakDownChipForRemoval(List<int> betList, List<GameObject> chipList, int targetValue)
    {
        // Find a larger chip that can be broken down
        for (int i = chipValues.Length - 1; i >= 0; i--)
        {
            int largeValue = chipValues[i];
            if (largeValue <= targetValue) continue;

            int largeIndex = betList.LastIndexOf(largeValue);
            if (largeIndex == -1) continue;

            // Break down this large chip
            if (largeIndex >= 0 && largeIndex < chipList.Count)
            {
                // Remove the large chip
                Destroy(chipList[largeIndex]);
                chipList.RemoveAt(largeIndex);
                betList.RemoveAt(largeIndex);

                // Find what chips make up this large chip
                int remaining = largeValue - targetValue;
                var breakdownChips = BreakIntoChips(remaining);

                // Add the breakdown chips back (except the one we're removing)
                foreach (int value in breakdownChips)
                {
                    betList.Add(value);
                }

                // Update the visual representation
                // We need to get the area - find it from the betList reference
                RectTransform area = GetAreaFromBetList(betList);
                if (area != null)
                {
                    RebuildChipsVisually(betList, chipList, area);
                }

                return;
            }
        }

        // Fallback: if we can't find a larger chip, just remove the last chip
        if (chipList.Count > 0 && betList.Count > 0)
        {
            int lastIndex = chipList.Count - 1;
            if (chipList[lastIndex] != null)
            {
                Destroy(chipList[lastIndex]);
            }
            chipList.RemoveAt(lastIndex);
            betList.RemoveAt(betList.Count - 1);
        }
    }

    private RectTransform GetAreaFromBetList(List<int> betList)
    {
        if (betList == playerBets) return playerBetArea;
        if (betList == bankerBets) return bankerBetArea;
        if (betList == tieBets) return tieBetArea;
        return null;
    }

    private void RebuildChipsVisually(List<int> betList, List<GameObject> chipList, RectTransform area)
    {
        // Clear existing chips
        foreach (var chip in chipList)
        {
            if (chip != null) Destroy(chip);
        }
        chipList.Clear();

        // Recreate chips based on betList
        foreach (int value in betList)
        {
            var chipPrefab = chipSelector.GetChipByValue(value).ChipPreab;
            GameObject chip = Instantiate(chipPrefab, area);
            chip.transform.localScale = Vector3.one;
            chip.transform.localPosition = new Vector3(0, chipList.Count * 3f, 0);
            var chipText = chip.GetComponentInChildren<TextMeshProUGUI>();
            if (chipText != null) chipText.text = value.ToString();
            chipList.Add(chip);
        }

        RearrangeStack(betList, chipList);
    }

    private void ClearArea(List<int> betList, List<GameObject> chipList)
    {
        foreach (var chip in chipList)
        {
            if (chip != null) Destroy(chip);
        }
        betList.Clear();
        chipList.Clear();
    }

    private void RearrangeStack(List<int> betList, List<GameObject> chipList)
    {
        int count = Mathf.Min(betList.Count, chipList.Count);
        if (count == 0) return;

        var paired = new List<(int value, GameObject chip)>(count);
        for (int i = 0; i < count; i++)
            paired.Add((betList[i], chipList[i]));

        // Sort by value descending (largest chips at bottom)
        paired = paired.OrderByDescending(p => p.value).ToList();

        betList.Clear();
        chipList.Clear();

        for (int i = 0; i < paired.Count; i++)
        {
            betList.Add(paired[i].value);
            chipList.Add(paired[i].chip);

            RectTransform rt = paired[i].chip.GetComponent<RectTransform>();
            rt.DOLocalMove(new Vector3(0, i * 3f, 0), 0.25f).SetEase(Ease.OutCubic);
            rt.SetSiblingIndex(i);
        }
    }

    private void UpdateTotalBetDisplay()
    {
        int total = GetTotalCurrentBet();
        uiManager.UpdateBetAmountText(total);
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
        // int winAmount = (int)socketManager.resultData.payload.winAmount;
        int winAmount = Mathf.RoundToInt((float)socketManager.resultData.payload.winAmount);
        uiManager.UpdateBalanceText(socketManager.resultData.player.balance);

        if (player > banker)
        {
            Debug.Log("Player wins");
            audioController.PlayPlayerWins();
            int profit = winAmount - GetPlayerBet();
            // if (profit == 0)
            // {
            //     profit = 1;
            // }
            yield return PlayerWin(profit);
        }
        else if (banker > player)
        {
            Debug.Log("Banker wins");
            audioController.PlayBankerWins();
            int profit = winAmount - GetBankerBet();
            // if (profit == 0)
            // {
            //     profit = 1;
            // }
            yield return BankerWin(profit);
        }
        else
        {
            Debug.Log("Tie");
            audioController.PlayGameTie();
            int profit = winAmount - (GetTieBet() + GetBankerBet() + GetPlayerBet());
            yield return TieWin(profit);
        }
        // bankerBets.Clear();
        // playerBets.Clear();
        // tieBets.Clear();
        uiManager.UpdateBetAmountText(0);
    }

    private IEnumerator PlayerWin(int profit)
    {
        if (profit <= 0)
        {
            DestroyChipList(bankerChips, bankerBets);
            DestroyChipList(tieChips, tieBets);
            bankerBets.Clear();
            tieBets.Clear();
            yield break;
        }
        // audioController.PlayPlayerWins();
        yield return new WaitForSeconds(1f);

        yield return SpawnProfitChips(profit, playerBetArea, playerChips, playerBets);
        yield return new WaitUntil(() => !isSpawningWinnings);

        Debug.Log("Spawning done");
        yield return new WaitForSeconds(1.5f);

        yield return CollectChips(playerChips, playerBets);
        yield return new WaitUntil(() => !collectingWinnings);

        uiManager.UpdateWinningAreaText(socketManager.resultData.payload.winAmount.ToString());
        Debug.Log("Collecting done");

        yield return new WaitForSeconds(1.5f);

        DestroyChipList(bankerChips, bankerBets);
        DestroyChipList(tieChips, tieBets);
    }

    private IEnumerator BankerWin(int profit)
    {

        if (profit <= 0)
        {
            DestroyChipList(playerChips, playerBets);
            DestroyChipList(tieChips, tieBets);
            playerBets.Clear();
            tieBets.Clear();
            yield break;
        }

        yield return SpawnProfitChips(profit, bankerBetArea, bankerChips, bankerBets);
        yield return new WaitUntil(() => !isSpawningWinnings);

        Debug.Log("Spawning done");
        yield return new WaitForSeconds(1.5f);

        yield return CollectChips(bankerChips, bankerBets);
        yield return new WaitUntil(() => !collectingWinnings);

        uiManager.UpdateWinningAreaText(socketManager.resultData.payload.winAmount.ToString());
        Debug.Log("Collecting done");

        yield return new WaitForSeconds(1.5f);

        // audioController.PlayBankerWins();
        yield return new WaitForSeconds(1f);


        DestroyChipList(playerChips, playerBets);
        // DestroyChipList(bankerChips);
        DestroyChipList(tieChips, tieBets);
    }

    private IEnumerator TieWin(int profit)
    {

        if (profit <= 0)
        {
            DestroyChipList(playerChips, playerBets);
            DestroyChipList(bankerChips, bankerBets);
            playerBets.Clear();
            bankerBets.Clear();
            yield break;
        }

        // audioController.PlayGameTie();
        yield return new WaitForSeconds(1f);

        yield return SpawnProfitChips(profit, tieBetArea, tieChips, tieBets);
        yield return new WaitUntil(() => !isSpawningWinnings);

        Debug.Log("Spawning done");
        yield return new WaitForSeconds(1f);

        yield return CollectChips(tieChips, tieBets);
        yield return new WaitUntil(() => !collectingWinnings);

        Debug.Log("Collecting done");
        yield return new WaitForSeconds(1f);

        yield return CollectChips(bankerChips, bankerBets);
        yield return new WaitUntil(() => !collectingWinnings);

        yield return new WaitForSeconds(1f);

        yield return CollectChips(playerChips, playerBets);
        yield return new WaitUntil(() => !collectingWinnings);

        uiManager.UpdateWinningAreaText(socketManager.resultData.payload.winAmount.ToString());
    }

    private IEnumerator SpawnProfitChips(int amount, RectTransform targetArea, List<GameObject> chipList, List<int> betList)
    {
        if (amount <= 0) yield break;

        isSpawningWinnings = true;

        var chips = BreakIntoChips(amount);
        foreach (int value in chips)
        {
            AddWinningChip(betList, chipList, targetArea, value);
            yield return new WaitForSeconds(0.1f);
        }

        isSpawningWinnings = false;
    }

    private void AddWinningChip(List<int> betList, List<GameObject> chipList, RectTransform area, int value)
    {
        audioController.PlayChip();
        betList.Add(value);

        var chipPrefab = chipSelector.GetChipByValue(value).ChipPreab;
        GameObject chip = Instantiate(chipPrefab, chipRoot);
        chip.transform.localScale = Vector3.one;
        var chipText = chip.GetComponentInChildren<TextMeshProUGUI>();
        if (chipText != null) chipText.text = value.ToString();

        AnimateChipToBetArea(chip, area, chipList.Count);
        chipList.Add(chip);

        OptimizeStack(betList, chipList, area);
    }

    private IEnumerator CollectChips(List<GameObject> chips, List<int> betList)
    {
        collectingWinnings = true;
        Debug.Log("Chip Count: " + chips.Count);

        for (int i = 0; i < chips.Count; i++)
        {
            GameObject chip = chips[i];
            if (chip == null) continue;

            RectTransform rt = chip.GetComponent<RectTransform>();
            rt.SetParent(winningChipArea, true);

            Vector3 targetLocalPos = new Vector3(0, winningChips.Count * 3f, 0);

            Sequence seq = DOTween.Sequence();
            seq.Append(rt.DOScale(1.1f, 0.12f));
            seq.Append(rt.DOLocalMove(targetLocalPos, 0.35f).SetEase(Ease.InCubic));
            seq.Append(rt.DOScale(1f, 0.1f));

            winningChips.Add(chip);

            yield return new WaitForSeconds(0.1f);
        }

        chips.Clear();
        betList.Clear();
        collectingWinnings = false;
    }

    private void DestroyChipList(List<GameObject> chips, List<int> betList)
    {
        if (chips.Count == 0) return;

        foreach (var chip in chips)
        {
            if (chip != null) Destroy(chip);
        }
        chips.Clear();
        betList.Clear();
    }

    private List<int> BreakIntoChips(int amount)
    {
        if (amount <= 0) return new List<int>();

        var denoms = chipValues.Distinct().OrderByDescending(v => v).ToArray();

        // Fewest-chip EXACT decomposition via DP so we never silently drop value.
        // A pure greedy largest-first pick can get stuck with this denomination set
        // (e.g. 30 with {10,25,...} takes one 25 and is left with an
        // unrepresentable remainder of 5, losing 5 of the actual bet).
        int[] count = new int[amount + 1];
        int[] usedDenom = new int[amount + 1];
        for (int a = 1; a <= amount; a++)
        {
            count[a] = int.MaxValue;
            usedDenom[a] = -1;
            foreach (int d in denoms)
            {
                if (d > a || count[a - d] == int.MaxValue) continue;
                if (count[a - d] + 1 < count[a])
                {
                    count[a] = count[a - d] + 1;
                    usedDenom[a] = d;
                }
            }
        }

        List<int> result = new();
        if (count[amount] != int.MaxValue)
        {
            int remaining = amount;
            while (remaining > 0)
            {
                int d = usedDenom[remaining];
                result.Add(d);
                remaining -= d;
            }
            return result;
        }

        // No exact combination of chip denominations sums to this amount
        // (can happen for arbitrary payout amounts) - fall back to a
        // best-effort greedy breakdown, used only for cosmetic chip stacking.
        int rem = amount;
        for (int i = 0; i < denoms.Length; i++)
        {
            while (rem >= denoms[i])
            {
                rem -= denoms[i];
                result.Add(denoms[i]);
            }
        }
        return result;
    }

    internal void DestroyWinningChips()
    {
        List<int> winningbets = new();
        DestroyChipList(winningChips, winningbets);
        uiManager.WinningAreaText.gameObject.SetActive(false);
    }

    internal void ClearBetHistoryAfterRound()
    {
        betHistory.Clear();
        betsLocked = false;
    }

    #endregion
}
