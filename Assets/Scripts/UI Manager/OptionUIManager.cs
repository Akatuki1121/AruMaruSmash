
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class OptionUIManager : BaseUIManager
{
    [Header("遊び方ボタン")]
    [SerializeField] private Image howToPlayButtonImage;
    [SerializeField] private Sprite normalHowToPlaySprite;
    [SerializeField] private Sprite pressedHowToPlaySprite;

    [Header("SEボタン")]
    [SerializeField] private Image SEButtonImage;
    [SerializeField] private Sprite normalSESprite;
    [SerializeField] private Sprite pressedSESprite;

    [Header("BGMボタン")]
    [SerializeField] private Image BGMButtonImage;
    [SerializeField] private Sprite normalBGMSprite;
    [SerializeField] private Sprite pressedBGMSprite;

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

    private const int MAX_V_SELECT = 3;
    private const int MIN_V_SELECT = 0;

    private int selectV = 0;

    private UIInputDirection previousDirection =
        UIInputDirection.None;

    private Vector3 howToPlayOriginalScale;
    private Vector3 backOriginalScale;

    private bool isPressing = false;

    private void Start()
    {
        button = FindAnyObjectByType<ButtonManager>();

        if (howToPlayButtonImage != null)
        {
            howToPlayOriginalScale =
                howToPlayButtonImage.rectTransform.localScale;
        }

        if (backButtonImage != null)
        {
            backOriginalScale =
                backButtonImage.rectTransform.localScale;
        }

        UpdateButtonSprites();
    }

    private void OnEnable()
    {
        isPressing = false;
        previousDirection = UIInputDirection.None;
    }

    private void Update()
    {
        if (button == null || isPressing) return;

        UIInputDirection direction = GetStick();

        // スティックを倒した瞬間だけ選択変更
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

            selectV = Mathf.Clamp(
                selectV,
                MIN_V_SELECT,
                MAX_V_SELECT
            );
        }

        previousDirection = direction;

        // 選択中のボタンを拡大
        UpdateButtonScale();

        if (!GetConfirmButtonDown()) return;

        if (selectV == 0 || selectV == 3)
        {
            StartCoroutine(PressButtonRoutine());
        }
        else if (selectV == 1)
        {
            // BGMスライダー操作
        }
        else if (selectV == 2)
        {
            // SEスライダー操作
        }

        Debug.Log($"Optionの選択は：{selectV}");
    }

    private void UpdateButtonScale()
    {
        SetButtonScale(
            howToPlayButtonImage,
            howToPlayOriginalScale,
            selectV == 0
        );

        SetButtonScale(
            backButtonImage,
            backOriginalScale,
            selectV == 3
        );
    }

    private void SetButtonScale(
        Image image,
        Vector3 originalScale,
        bool selected)
    {
        if (image == null) return;

        float multiplier =
            selected ? selectedScale : 1f;

        image.rectTransform.localScale = Vector3.Lerp(
            image.rectTransform.localScale,
            originalScale * multiplier,
            Time.unscaledDeltaTime * scaleSpeed
        );
    }

    private void UpdateButtonSprites()
    {
        if (howToPlayButtonImage != null &&
            normalHowToPlaySprite != null)
        {
            howToPlayButtonImage.sprite =
                normalHowToPlaySprite;
        }

        if (backButtonImage != null &&
            normalBackSprite != null)
        {
            backButtonImage.sprite =
                normalBackSprite;
        }
    }

    private IEnumerator PressButtonRoutine()
    {
        isPressing = true;

        int pressedSelection = selectV;

        Image selectedImage = null;
        Sprite pressedSprite = null;
        Vector3 originalScale = Vector3.one;

        if (pressedSelection == 0)
        {
            selectedImage = howToPlayButtonImage;
            pressedSprite = pressedHowToPlaySprite;
            originalScale = howToPlayOriginalScale;
        }
        else if (pressedSelection == 3)
        {
            selectedImage = backButtonImage;
            pressedSprite = pressedBackSprite;
            originalScale = backOriginalScale;
        }

        if (selectedImage != null)
        {
            if (pressedSprite != null)
            {
                selectedImage.sprite = pressedSprite;
            }

            selectedImage.rectTransform.localScale =
                originalScale * pressedScale;
        }

        // 押された画像を少しだけ表示
        yield return new WaitForSecondsRealtime(pressDuration);

        // 次に開いたときのために元に戻す
        if (selectedImage != null)
        {
            selectedImage.rectTransform.localScale =
                originalScale;
        }

        UpdateButtonSprites();

        // 画像演出後に画面遷移
        if (pressedSelection == 0)
        {
            button.OnHowToPlayClicked();
        }
        else if (pressedSelection == 3)
        {
            button.OnBackTitleClicked();
        }

        isPressing = false;
    }
}
