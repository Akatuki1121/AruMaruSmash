using UnityEngine;

public class OptionUIManager : BaseUIManager
{
    private ButtonManager button;

    private const int MAX_V_SELECT = 1;
    private const int MAX_H_SELECT = 1;
    private const int MIN_V_SELECT = 0;
    private const int MIN_H_SELECT = 0;

    private int selectV = 0;
    private int selectH = 0;

    private UIInputDirection previousDirection = UIInputDirection.None;

    private void Start()
    {
        button = FindAnyObjectByType<ButtonManager>();
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

                case UIInputDirection.Left:
                    selectH--;
                    break;

                case UIInputDirection.Right:
                    selectH++;
                    break;
            }

            selectV = Mathf.Clamp(selectV, MIN_V_SELECT, MAX_V_SELECT);
            selectH = Mathf.Clamp(selectH, MIN_H_SELECT, MAX_H_SELECT);
        }

        previousDirection = direction;

        if (!GetButtonDown()) return;

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

        Debug.Log($"縦の選択は：{selectV}");
        Debug.Log($"横の選択は：{selectH}");
    }
}