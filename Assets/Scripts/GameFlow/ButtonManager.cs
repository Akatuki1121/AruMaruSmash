using UnityEngine;

public class ButtonManager : MonoBehaviour
{
    //sutartボタンが押されたときの処理
    public void OnStartClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.Start);
    }

    //optionボタンが押されたときの処理
    public void OnOptionClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.OpenOption);
    }

    //quitボタンが押されたときの処理
    public void OnQuitClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.Quit);
    }

    // gameスタートボタンが押されたときの処理
    public void OnStartGameClicked()
    {
        GameFlowManager.Instance?.Request(GameEvent.StartGame);
    }

}
