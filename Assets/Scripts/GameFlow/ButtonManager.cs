using UnityEngine;

public class ButtonManager : MonoBehaviour
{
    // startボタンが押されたときの処理
    public void OnStartClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.Start);
    }

    // optionボタンが押されたときの処理
    public void OnOptionClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.OpenOption);
    }

    // quitボタンが押されたときの処理
    public void OnQuitClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.Quit);
    }

    // gameスタートボタンが押されたときの処理
    public void OnStartGameClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.StartGame);
    }

    // リーダー限定の遷移（リザルト・ポーズ等）に渡す操作者。
    // slot を渡さないとリーダー判定に通らず遷移できないため、UI操作はリーダーの操作として扱う
    private static int LeaderSlot
    {
        get
        {
            GameFlowManager flow = GameFlowManager.Instance;
            return flow != null ? flow.Players.LeaderIndex : -1;
        }
    }

    // titleに戻るボタンが押されたときの処理
    public void OnBackTitleClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.ToTitle, LeaderSlot);
    }

    // 設定ボタンが押されたときの処理
    public void OnSettingsClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.OpenSettings);
    }

    // 操作説明ボタンが押されたときの処理
    public void OnHowToPlayClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.OpenGuide);
    }

    // 前の画面に戻るボタンが押されたときの処理
    public void OnBackClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.Back);
    }

    // キャラクター選択画面からコントローラー割り当て画面に戻るときの処理
    public void OnBackToAssignmentClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.BackToAssignment);
    }

    // 待機画面からキャラクター選択画面に戻るときの処理
    public void OnCancelToCharacterSelectClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.CancelToCharacterSelect);
    }

    // ポーズボタンが押されたときの処理
    public void OnPauseClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.Pause);
    }

    // ゲーム再開ボタンが押されたときの処理
    public void OnResumeClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.Resume);
    }

    // 待機画面に戻るボタンが押されたときの処理
    public void OnBackToWaitingClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.ToWaiting);
    }

    // リトライボタンが押されたときの処理
    public void OnRetryClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.Retry, LeaderSlot);
    }
}