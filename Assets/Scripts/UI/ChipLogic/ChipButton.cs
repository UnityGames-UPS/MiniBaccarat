using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChipButton : MonoBehaviour
{
    [SerializeField] internal int value;
    [SerializeField] internal Button button;
    [SerializeField] internal Image chipImage;
    [SerializeField] private float chipSizeFactor = 2;
    [SerializeField] internal GameObject ChipPreab;

    internal void SetSelected(bool active)
    {
        if (active)
            transform.localScale = Vector3.one * chipSizeFactor;
        else
            transform.localScale = Vector3.one;
    }
}
