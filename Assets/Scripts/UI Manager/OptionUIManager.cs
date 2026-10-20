using UnityEngine;

public class OptionUIManager : BaseUIManager
{
    private ButtonManager button;

    public GameObject UIManager;
    public GameObject OptionUI;
    public GameObject HowToPlayUI;

    private const int MAX_V_SELECT = 3;
    private const int MIN_V_SELECT = 0;

    private int selectV = 0;

    private UIInputDirection previousDirection = UIInputDirection.None;

    private void Start()
    {
        button = FindAnyObjectByType<ButtonManager>();
        OptionUI.SetActive(false);
    }

    private void Update()
    {
        if (button == null) return;

        UIInputDirection direction = GetStick();

        // スティックを倒した瞬間だけ選択位置を変更
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

                // 横入力でもUI操作いるのかな？
                //case UIInputDirection.Left:
                //    selectV--;
                //    break;

                //case UIInputDirection.Right:
                //    selectV++;
                //    break;
            }

            selectV = Mathf.Clamp(selectV, MIN_V_SELECT, MAX_V_SELECT);
        }

        previousDirection = direction;

        if (!GetButtonDown()) return;

        if (selectV == 0)
        {
            HowToPlayUI.SetActive(true);
            OptionUI.SetActive(false);

            button.OnHowToPlayClicked();
        }
        else if (selectV == 1)
        {
            // BGMスライダー操作
        }
        else if (selectV == 2)
        {
            // SEスライダー操作
        }
        else if(selectV == 3)
        {
            UIManager.SetActive(true);
            OptionUI.SetActive(false);

            button.OnBackTitleClicked();
        }

            Debug.Log($"Optionの選択は：{selectV}");
    }
}