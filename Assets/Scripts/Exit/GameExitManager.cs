
using UnityEngine;
using UnityEngine.InputSystem;

public class GameExitManager : MonoBehaviour
{
    private static GameExitManager instance;

    private void Awake()
    {
        // 重複生成を防止
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // シーンが切り替わっても削除されない
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Escキーが押された瞬間
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ExitGame();
        }
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        // エディターでは再生停止
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // ビルド版ではゲーム終了
        Application.Quit();
#endif
    }
}
