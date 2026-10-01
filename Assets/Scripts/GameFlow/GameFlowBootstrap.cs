using UnityEngine;

/// <summary>
/// どのシーンから再生しても、画面遷移まわりの管理オブジェクトを1つだけ自動生成する。
/// シーンに手置きする必要はない（置いてあっても重複分は GameFlowManager が破棄する）
/// </summary>
public static class GameFlowBootstrap
{
    private const string ROOT_NAME = "GameFlow";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (GameFlowManager.Instance != null)
        {
            return;
        }

        GameObject root = new GameObject(ROOT_NAME);
        root.AddComponent<GameFlowManager>();        // Awake で Instance 設定 + DontDestroyOnLoad
        root.AddComponent<FadeOverlay>();
        root.AddComponent<SceneLoader>();
        root.AddComponent<ControllerConnectionMonitor>();
        root.AddComponent<JoyconConnectionMonitor>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        root.AddComponent<DebugFlowPanel>();         // 仮UI。本UIができたら削除
#endif
    }
}
