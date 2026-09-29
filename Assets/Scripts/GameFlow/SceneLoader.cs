using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameState の変化を見て、必要ならシーンを読み込む。
/// 役割分担: シーンは「大きな場面の切り替え」、同一シーン内の画面はUIパネルの切り替え（ScreenBase側）
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public const string SCENE_TITLE = "Title";
    public const string SCENE_LOBBY = "CharacterChoice";
    public const string SCENE_GAME = "Game";
    public const string SCENE_RESULT = "Result";

    private GameFlowManager m_flow;

    private void Start()
    {
        m_flow = GameFlowManager.Instance;
        if (m_flow == null)
        {
            return;
        }
        m_flow.OnStateChanged += HandleStateChanged;
    }

    private void OnDestroy()
    {
        if (m_flow != null)
        {
            m_flow.OnStateChanged -= HandleStateChanged;
        }
    }

    /// <summary>
    /// 状態に対応するシーン名。Title周り/ロビー/ゲーム/リザルトの4シーン構成。
    /// ロビーは既存の CharacterChoice シーンにまとめる
    /// </summary>
    public static string GetSceneName(GameState state)
    {
        switch (state)
        {
            case GameState.Title:
            case GameState.Option:
            case GameState.OperationGuide:
            case GameState.Settings:
                return SCENE_TITLE;

            case GameState.ControllerAssignment:
            case GameState.CharacterSelect:
            case GameState.Waiting:
            case GameState.GameStartConfirm:
                return SCENE_LOBBY;

            case GameState.Playing:
            case GameState.Pause:
                return SCENE_GAME;

            case GameState.Result:
                return SCENE_RESULT;

            default:
                return null;   // Quit など、読み込まない状態
        }
    }

    private void HandleStateChanged(GameState from, GameState to)
    {
        string target = GetSceneName(to);
        if (string.IsNullOrEmpty(target))
        {
            return;
        }
        if (SceneManager.GetActiveScene().name == target)
        {
            return;
        }
        SceneManager.LoadScene(target);
    }
}
