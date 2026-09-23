using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

public class ChipSelector : MonoBehaviour
{
    [SerializeField] private List<ChipButton> chips = new List<ChipButton>();
    internal ChipButton selectedChip;
    private bool chipSelected = false;


    private void Start()
    {
        foreach (var chip in chips)
        {
            chip.button.onClick.AddListener(() => OnChipSelected(chip));
        }
        selectedChip = chips[5];
        selectedChip.SetSelected(true);
        chipSelected = true;
    }

    private void OnChipSelected(ChipButton chip)
    {
        if (selectedChip != null)
            selectedChip.SetSelected(false);

        selectedChip = chip;
        selectedChip.SetSelected(true);

        chipSelected = true;

        Debug.Log("Selected Chip: " + GetSelectedChipValue());
    }


    internal float GetSelectedChipValue()
    {
        return selectedChip != null ? selectedChip.value : 0;
    }

    internal void ApplyChipValues(List<int> values)
    {
        if (values == null || values.Count == 0)
        {
            Debug.LogWarning("ApplyChipValues: no chip values provided, keeping inspector defaults.");
            return;
        }

        if (values.Count != chips.Count)
            Debug.LogWarning($"ApplyChipValues: expected {chips.Count} chip values, got {values.Count}.");

        int count = Mathf.Min(values.Count, chips.Count);
        for (int i = 0; i < count; i++)
            chips[i].SetValue(values[i]);
    }


    internal ChipButton GetChipByValue(float value)
    {
        const float EPS = 0.0001f;

        foreach (var chip in chips)
        {
            if (Mathf.Abs(chip.value - value) <= EPS)
                return chip;
        }

        ChipButton best = null;
        float bestDiff = float.MaxValue;
        foreach (var chip in chips)
        {
            float diff = Mathf.Abs(chip.value - value);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                best = chip;
            }
        }

        if (best == null)
            Debug.LogError($"GetChipByValue: no chip buttons assigned in ChipSelector!");
        else
            Debug.LogWarning($"GetChipByValue: exact match not found for {value}, using closest {best.value}");

        return best;
    }

}
