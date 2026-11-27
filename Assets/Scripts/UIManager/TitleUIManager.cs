
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TitleUIManager : BaseUIManager
{
    [Header("スタートボタン")]
    [SerializeField] private Image startButtonImage;
    [SerializeField] private Sprite normalStartSprite;
    [SerializeField] private Sprite pressedStartSprite;

    [Header("オプションボタン")]
    [SerializeField] private Image optionButtonImage;
    [SerializeField] private Sprite normalOptionSprite;
    [SerializeField] private Sprite pressedOptionSprite;

    [Header("ボタン演出")]
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float pressedScale = 0.95f;
    [SerializeField] private float pressDuration = 0.15f;
    [SerializeField] private float scaleSpeed = 12f;

    [Header("UIの切り替え")]
    public GameObject UIManager;
    public GameObject OptionUI;

    private ButtonManager button;

    private const int MAX_V_SELECT = 1;

    private int selectV = 0;

    private Vector3 startOriginalScale;
    private Vector3 optionOriginalScale;


    private UIInputDirection previousDirection = UIInputDirection.None;
    private bool isPressing = false;

    private void Start()
    {
        button = FindAnyObjectByType<ButtonManager>();

        startOriginalScale = startButtonImage.rectTransform.localScale;
        optionOriginalScale = optionButtonImage.rectTransform.localScale;

        UpdateButtonSprites(startButtonImage, normalStartSprite);
        UpdateButtonSprites(optionButtonImage, normalOptionSprite);
    }

    private void Update()
    {
        if (button == null || isPressing) return;

        UIInputDirection direction = GetStick();

        if (direction != UIInputDirection.None &&
            previousDirection == UIInputDirection.None)
        {
            switch (direction)
            {
                case UIInputDirection.Up:
                    selectV--;
                    break;
                case UIInputDirection.Down:
                    selectV++;
                    break;
            }

            selectV = Mathf.Clamp(selectV, 0, MAX_V_SELECT);
        }

        previousDirection = direction;

        UpdateButtonScale();

        if (GetConfirmButtonDown())
        {
            StartCoroutine(PressButtonRoutine());
        }
    }

    private void UpdateButtonScale()
    {
        SetButtonScale(startButtonImage, startOriginalScale,
        selectV == 0);

        SetButtonScale(optionButtonImage, optionOriginalScale,
            selectV == 1);
    }

    private void SetButtonScale(Image image, Vector3 originalScale, bool selected)
    {
        if (image == null) return;

        float multiplier = selected ? selectedScale : 1f;

        image.rectTransform.localScale = Vector3.Lerp(
            image.rectTransform.localScale,
            originalScale * multiplier,
            Time.unscaledDeltaTime * scaleSpeed
        );
    }

    private IEnumerator PressButtonRoutine()
    {
        isPressing = true;

        Image selectedImage = null;
        Sprite pressedSprite = null;

        if (selectV == 0)
        {
            selectedImage = startButtonImage;
            pressedSprite = pressedStartSprite;
        }
        else if (selectV == 1)
        {
            selectedImage = optionButtonImage;
            pressedSprite = pressedOptionSprite;
        }
        else
        {
            isPressing = false;
            yield break;
        }

        if (selectedImage != null)
        {
            if (pressedSprite != null)
                selectedImage.sprite = pressedSprite;

            selectedImage.rectTransform.localScale =
                Vector3.one * pressedScale;
        }

        yield return new WaitForSecondsRealtime(pressDuration);

        if (selectV == 0)
        {
            button.OnStartClicked();
        }
        else if (selectV == 1)
        {
            button.OnOptionClicked();
        }

        UpdateButtonSprites(startButtonImage, normalStartSprite);
        UpdateButtonSprites(optionButtonImage, normalOptionSprite);
        isPressing = false;
    }
}
