
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HowToPlayUIManager : BaseUIManager
{
    [Header("戻るボタン")]
    [SerializeField] private Image backButtonImage;
    [SerializeField] private Sprite normalBackSprite;
    [SerializeField] private Sprite pressedBackSprite;

    [Header("ボタン演出")]
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float pressedScale = 0.95f;
    [SerializeField] private float pressDuration = 0.15f;
    [SerializeField] private float scaleSpeed = 12f;

    private ButtonManager button;
    private Vector3 backButtonOriginalScale;
    private bool isPressing = false;

    private void Start()
    {
        button = FindAnyObjectByType<ButtonManager>();

        backButtonOriginalScale =
            backButtonImage.rectTransform.localScale;

        backButtonImage.sprite = normalBackSprite;
    }

    private void OnEnable()
    {
        isPressing = false;
    }

    private void Update()
    {
        if (button == null || isPressing) return;

        UpdateButtonScale();

        if (GetConfirmButtonDown())
        {
            StartCoroutine(PressButtonRoutine());
        }
    }

    private void UpdateButtonScale()
    {
        if (backButtonImage == null) return;

        Vector3 targetScale =
            backButtonOriginalScale * selectedScale;

        backButtonImage.rectTransform.localScale =
            Vector3.Lerp(
                backButtonImage.rectTransform.localScale,
                targetScale,
                Time.unscaledDeltaTime * scaleSpeed
            );
    }

    private IEnumerator PressButtonRoutine()
    {
        isPressing = true;

        backButtonImage.sprite = pressedBackSprite;

        backButtonImage.rectTransform.localScale =
            backButtonOriginalScale * pressedScale;

        yield return new WaitForSecondsRealtime(pressDuration);

        backButtonImage.sprite = normalBackSprite;

        button.OnBackClicked();

        isPressing = false;
    }
}
