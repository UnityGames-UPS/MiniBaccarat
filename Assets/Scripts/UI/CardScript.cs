using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CardScript : MonoBehaviour
{
    [SerializeField]
    private Image Card_Image;
    [SerializeField]
    private LayoutElement Card_LE;
    [SerializeField]
    private Transform Card_transform;
    [SerializeField] private GameManager gameManager;

    private Sprite csprite = null;

    private void Start()
    {
        gameManager = GameObject.FindWithTag("GameController").GetComponent<GameManager>();
    }

    public void OnFlipMethod(Sprite cardSprite, int value)
    {
        csprite = cardSprite;
        Card_transform.localEulerAngles = new Vector3(0, 180, 0);

        Sequence flipSeq = DOTween.Sequence();

        flipSeq.Append(Card_transform.DOLocalRotate(Vector3.zero, 0.45f).SetEase(Ease.OutCubic));

        flipSeq.OnComplete(() =>
        {
            Card_LE.ignoreLayout = false;
            gameManager.AfterCardFlip(value);
        });

        DOVirtual.DelayedCall(0.22f, changeSprite);
    }

    private void changeSprite()
    {
        Card_Image.sprite = csprite;
    }
}
