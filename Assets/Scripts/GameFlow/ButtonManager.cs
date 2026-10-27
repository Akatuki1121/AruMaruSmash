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

    // titleに戻るボタンが押されたときの処理
    public void OnBackTitleClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.ToTitle);
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
        GameFlowManager.Instance?.Request(GameEvent.Retry);
    }
}