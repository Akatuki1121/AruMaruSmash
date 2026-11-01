using UnityEngine;

public class CharacterChoiceUIManager : BaseUIManager
{
    private ButtonManager button;

    public GameObject selectPlayer;

    private const int MAX_V_SELECT = 1;
    private const int MAX_H_SELECT = 1;
    private const int MIN_V_SELECT = 0;
    private const int MIN_H_SELECT = 0;

    private int selectV = 0;
    private int selectH = 0;

    // プレイヤーの準備用
    private int readyPlayerCount = 1;  // 準備OKを押した人数
    private int connectedJoyconCount = 1; // 接続中のJoy-Con数

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

        if (!GetConfirmButtonDown()) return;

        GameFlowManager flow = GameFlowManager.Instance;

        if (flow == null) return;

        // テスト用：接続中の全プレイヤーを準備OKにする
        foreach (PlayerSlot player in flow.Players.ActivePlayers)
        {
            flow.ConfirmReady(player.index);
        }

        // 全員準備OKならゲーム開始
        if (flow.Players.AllConfirmed)
        {
            Debug.Log("全員準備OK！ゲームスタート！");
            button.OnStartGameClicked();
        }

        Debug.Log($"縦の選択は：{selectV}");
        Debug.Log($"横の選択は：{selectH}");
    }
}