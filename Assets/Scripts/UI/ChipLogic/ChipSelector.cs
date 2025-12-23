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
