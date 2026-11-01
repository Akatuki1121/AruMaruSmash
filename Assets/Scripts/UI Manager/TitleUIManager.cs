
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

    [Header("UIの切り替え")]
    public GameObject UIManager;
    public GameObject OptionUI;

    [Header("ボタン演出")]
    [SerializeField] private float selectedScale = 1.1f;
    [SerializeField] private float pressedScale = 0.95f;
    [SerializeField] private float pressDuration = 0.15f;
    [SerializeField] private float scaleSpeed = 12f;

    private ButtonManager button;

    private const int MAX_V_SELECT = 1;
    private const int MAX_H_SELECT = 1;

    private int selectV = 0;
    private int selectH = 0;

    private UIInputDirection previousDirection = UIInputDirection.None;
    private bool isPressing = false;

    private void Start()
    {
        button = FindAnyObjectByType<ButtonManager>();
        UpdateButtonSprites();
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
                case UIInputDirection.Left:
                    selectH--;
                    break;
                case UIInputDirection.Right:
                    selectH++;
                    break;
            }

            selectV = Mathf.Clamp(selectV, 0, MAX_V_SELECT);
            selectH = Mathf.Clamp(selectH, 0, MAX_H_SELECT);
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
        SetButtonScale(startButtonImage, selectV == 0 && selectH == 0);
        SetButtonScale(optionButtonImage, selectV == 1 && selectH == 0);
    }

    private void SetButtonScale(Image image, bool selected)
    {
        if (image == null) return;

        float target = selected ? selectedScale : 1f;

        image.rectTransform.localScale = Vector3.Lerp(
            image.rectTransform.localScale,
            Vector3.one * target,
            Time.unscaledDeltaTime * scaleSpeed
        );
    }

    private void UpdateButtonSprites()
    {
        if (startButtonImage != null)
            startButtonImage.sprite = normalStartSprite;

        if (optionButtonImage != null)
            optionButtonImage.sprite = normalOptionSprite;
    }

    private IEnumerator PressButtonRoutine()
    {
        isPressing = true;

        Image selectedImage = null;
        Sprite pressedSprite = null;

        if (selectV == 0 && selectH == 0)
        {
            selectedImage = startButtonImage;
            pressedSprite = pressedStartSprite;
        }
        else if (selectV == 1 && selectH == 0)
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

        if (selectV == 0 && selectH == 0)
        {
            button.OnStartClicked();
        }
        else if (selectV == 1 && selectH == 0)
        {
            button.OnOptionClicked();
        }
        else if (selectV == 1 && selectH == 1)
        {
            button.OnQuitClicked();
        }

        UpdateButtonSprites();
        isPressing = false;
    }
}
