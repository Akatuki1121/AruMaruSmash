using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameState の変化を見て、必要ならフェードしてシーンを読み込む。
/// 役割分担: シーンは「大きな場面の切り替え」、同一シーン内の画面はUIパネルの切り替え（ScreenBase側）
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public const string SCENE_TITLE = "Title";
    public const string SCENE_LOBBY = "CharacterChoice";
    public const string SCENE_GAME = "Game";
    public const string SCENE_RESULT = "Result";

    // 仕様書(スライド2・4・6): フェードアウト0.2秒 → 1秒後に次の画面へ
    private const float FADE_OUT_SECONDS = 0.2f;
    private const float WAIT_AFTER_FADE_SECONDS = 1f;
    // 仕様書に記載なし（仮）: 読み込み後のフェードイン
    private const float FADE_IN_SECONDS = 0.2f;

    private GameFlowManager m_flow;
    private FadeOverlay m_fade;
    private Coroutine m_running;

    private void Start()
    {
        m_flow = GameFlowManager.Instance;
        m_fade = GetComponent<FadeOverlay>();
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
        if (to == GameState.Quit)
        {
            StartRoutine(QuitRoutine());
            return;
        }

        string target = GetSceneName(to);
        if (string.IsNullOrEmpty(target))
        {
            return;
        }
        if (SceneManager.GetActiveScene().name == target)
        {
            return;
        }
        StartRoutine(LoadRoutine(target));
    }

    private void StartRoutine(IEnumerator routine)
    {
        if (m_running != null)
        {
            StopCoroutine(m_running);
        }
        m_running = StartCoroutine(routine);
    }

    private IEnumerator LoadRoutine(string scene_name)
    {
        m_flow.IsInputLocked = true;
        if (m_fade != null)
        {
            yield return m_fade.FadeOut(FADE_OUT_SECONDS);
        }
        yield return new WaitForSecondsRealtime(WAIT_AFTER_FADE_SECONDS);

        SceneManager.LoadScene(scene_name);
        yield return null;   // 読み込み完了を1フレーム待つ

        if (m_fade != null)
        {
            yield return m_fade.FadeIn(FADE_IN_SECONDS);
        }
        m_flow.IsInputLocked = false;
        m_running = null;
    }

    private IEnumerator QuitRoutine()
    {
        m_flow.IsInputLocked = true;
        if (m_fade != null)
        {
            yield return m_fade.FadeOut(FADE_OUT_SECONDS);   // 仕様書: 0.2秒フェードアウトして終了
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
