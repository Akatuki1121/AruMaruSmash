using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 画面遷移の対象シーン(Title / CharacterChoice / Game / Result)から再生した時だけ、
/// 画面遷移まわりの管理オブジェクトを1つ自動生成する。テスト用シーンでは何も作らない。
/// 注意: シーン読み込み後に生成するため、シーン内の Awake / OnEnable では
/// GameFlowManager.Instance がまだ null。参照は Start 以降で行うこと
/// </summary>
public static class GameFlowBootstrap
{
    private const string ROOT_NAME = "GameFlow";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (GameFlowManager.Instance != null)
        {
            return;
        }
        if (!SceneLoader.IsFlowScene(SceneManager.GetActiveScene().name))
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
