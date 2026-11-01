
using UnityEngine;

public class CharacterChoiceUIManager : BaseUIManager
{
    private ButtonManager button;
    private GameFlowManager flow;

    public GameObject[] selectPlayer;
    public GameObject[] readyImage;

    private void Start()
    {
        button = FindAnyObjectByType<ButtonManager>();
        flow = GameFlowManager.Instance;

        // 最初は準備OK画像を非表示
        foreach (GameObject image in readyImage)
        {
            if (image != null)
                image.SetActive(false);
        }

        UpdateReadyImages();
    }

    private void Update()
    {
        if (flow == null) return;
        if (flow.State != GameState.CharacterSelect) return;
        if (flow.IsInputLocked) return;

        // 各Joy-Conの決定ボタンを個別に確認
        foreach (PlayerSlot player in flow.Players.ActivePlayers)
        {
            int slot = player.index;
            int deviceId = player.deviceId;

            // 準備OK　or ゲーム開始の判定
            if (GetConfirmButtonDown(deviceId))
            {
                if (player.hasConfirmedReady)
                {
                    if (flow.Players.AllConfirmed)
                    {
                        OnStartGame();
                        return;
                    }
                }
                else
                {
                    // 準備OK状態にする
                    flow.ConfirmReady(slot);

                    Debug.Log($"{slot + 1}P 準備OK！");
                }
            }

            // キャンセルで準備OK解除
            if (GetCancelButtonDown(deviceId))
            {
                flow.CancelCharacter(slot);

                Debug.Log($"{slot + 1}P 準備OK解除");
            }
        }

        UpdateReadyImages();
    }

    private void UpdateReadyImages()
    {
        if (flow == null) return;

        foreach (PlayerSlot player in flow.Players.slots)
        {
            int slot = player.index;

            if (slot >= readyImage.Length)
                continue;

            if (readyImage[slot] == null)
                continue;

            readyImage[slot].SetActive(player.IsActive && player.hasConfirmedReady);
        }
    }

    // ゲーム開始ボタンから呼ぶ
    public void OnStartGame()
    {
        if (flow == null || button == null)
            return;

        if (!flow.Players.AllConfirmed)
        {
            Debug.Log("まだ全員準備OKではありません");
            return;
        }

        Debug.Log("はじめましょう");
        button.OnStartGameClicked();
    }
}
