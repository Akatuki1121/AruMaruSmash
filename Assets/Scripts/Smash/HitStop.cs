using System.Collections;
using UnityEngine;

/// <summary>
/// ヒットストップ（衝突の瞬間にゲーム全体を一瞬止める演出）。
/// Time.timeScaleを0にして、実時間で指定秒数が経ったら元に戻す。
///
/// GameFlowManagerのポーズとぶつからないよう、プレイ中以外では発動せず、
/// ポーズ中に終了時刻が来ても timeScale には触らない。
/// </summary>
public static class HitStop
{
    private static HitStopRunner runner;

    /// <summary>ヒットストップを要求する。すでに止まっている・ポーズ中・プレイ中でないときは何もしない。</summary>
    public static void Request(float duration)
    {
        if (duration <= 0f) return;
        if (Time.timeScale <= 0f) return;
        if (!IsPlaying()) return;

        if (runner == null)
        {
            GameObject runnerObject = new GameObject("HitStopRunner");
            runnerObject.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(runnerObject);
            runner = runnerObject.AddComponent<HitStopRunner>();
        }

        runner.Begin(duration);
    }

    private static bool IsPlaying()
    {
        // GameFlowManagerが無いシーン（テストシーン等）では常に発動してよい
        return GameFlowManager.Instance == null || GameFlowManager.Instance.State == GameState.Playing;
    }

    private static bool IsPaused()
    {
        return GameFlowManager.Instance != null && GameFlowManager.Instance.State == GameState.Pause;
    }

    private sealed class HitStopRunner : MonoBehaviour
    {
        private bool active;
        private float savedTimeScale = 1f;

        public void Begin(float duration)
        {
            if (active) return;
            StartCoroutine(Run(duration));
        }

        private IEnumerator Run(float duration)
        {
            active = true;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            yield return new WaitForSecondsRealtime(duration);

            Restore();
        }

        private void Restore()
        {
            if (!active) return;
            active = false;

            // 止めている間にポーズされた場合は、ポーズ側が管理しているので触らない
            if (Time.timeScale == 0f && !IsPaused()) Time.timeScale = savedTimeScale;
        }

        private void OnDisable()
        {
            Restore();
        }
    }
}