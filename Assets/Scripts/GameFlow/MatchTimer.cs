using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 試合時間の管理。Game シーンに1つ置く。
/// GameState が Playing の間だけ時間が進み(ポーズ中は止まる)、matchSeconds に達したら
/// GameEvent.MatchEnd を Request して Result へ遷移させる。
/// GameFlowManager が無い(Game シーンを単体で再生した)場合も時間は進み、終了時はイベントだけ飛ばす。
/// 壁の崩壊タイミングは WallTimeSequencer(startDelay / interval)側で決める
/// </summary>
public class MatchTimer : MonoBehaviour
{
    [Tooltip("試合時間(秒)。3分 = 180")]
    [Min(1f)] public float matchSeconds = 180f;

    [Tooltip("残り時間を m:ss で表示するテキスト。未設定なら表示しない")]
    public TMP_Text timerText;

    /// <summary>試合開始からの経過秒数(Playing 中のみ加算)</summary>
    public float ElapsedSeconds { get; private set; }

    public float RemainingSeconds
    {
        get { return Mathf.Max(0f, matchSeconds - ElapsedSeconds); }
    }

    public bool IsFinished { get; private set; }

    /// <summary>試合時間が尽きたときに1回だけ呼ばれる</summary>
    public event Action OnMatchFinished;

    private int m_shown_seconds = -1;

    private void Start()
    {
        RefreshText();
    }

    private void Update()
    {
        if (IsFinished)
        {
            return;
        }
        if (!IsPlaying())
        {
            return;
        }

        ElapsedSeconds += Time.deltaTime;
        RefreshText();

        if (ElapsedSeconds >= matchSeconds)
        {
            Finish();
        }
    }

    private static bool IsPlaying()
    {
        // Game シーンを単体再生した場合、GameFlowManager は Title 状態で自動生成される。
        // その場合も試合を進めたいので、止めるのは Pause の間だけにする
        GameFlowManager flow = GameFlowManager.Instance;
        return flow == null || flow.State != GameState.Pause;
    }

    private void Finish()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        // Playing 中だけ遷移を要求する(単体再生時は遷移せず、イベントだけ飛ばす)。
        // フェード中などで受け付けられなかった場合は、次フレームでもう一度試す
        if (flow != null && flow.State == GameState.Playing && !flow.Request(GameEvent.MatchEnd))
        {
            return;
        }

        IsFinished = true;
        if (OnMatchFinished != null)
        {
            OnMatchFinished();
        }
    }

    private void RefreshText()
    {
        if (timerText == null)
        {
            return;
        }

        int seconds = Mathf.CeilToInt(RemainingSeconds);
        if (seconds == m_shown_seconds)
        {
            return;
        }
        m_shown_seconds = seconds;
        timerText.text = string.Format("{0}:{1:00}", seconds / 60, seconds % 60);
    }
}
