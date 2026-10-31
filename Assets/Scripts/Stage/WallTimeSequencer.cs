using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WallTimeBreaker を持つ壁を、基準点からの |x|・|z| の大きさ順に、時間をずらして壊す。
/// シーンに1つ置く。どの壁を対象にするかは、WallTimeBreaker を付けた壁かどうかで決まる。
/// 距離は max(|x|, |z|)。壁の中心は Renderer の bounds で取る(ピボットがずれていても影響しない)。
/// </summary>
public class WallTimeSequencer : MonoBehaviour
{
    [Tooltip("基準点。空ならワールド原点")]
    public Transform center;

    [Tooltip("最初の壁が消えるまでの秒数")]
    [Min(0f)] public float startDelay = 30f;

    [Tooltip("次の壁が消えるまでの間隔(秒)")]
    [Min(0.1f)] public float interval = 30f;

    [Tooltip("ONなら |x|,|z| の大きい壁(外側)から、OFFなら小さい壁(内側)から順に消す")]
    public bool outerFirst = true;

    [Tooltip("距離の差がこの値以内の壁は同時に消える(左右・前後の対称な壁をそろえる)")]
    [Min(0f)] public float tieTolerance = 0.5f;

    private void Start()
    {
        AssignBreakTimes();
    }

    private void AssignBreakTimes()
    {
        WallTimeBreaker[] breakers = FindObjectsByType<WallTimeBreaker>();
        if (breakers.Length == 0) return;

        Vector3 origin = center != null ? center.position : Vector3.zero;

        List<KeyValuePair<float, WallTimeBreaker>> ranked = new List<KeyValuePair<float, WallTimeBreaker>>();
        foreach (WallTimeBreaker breaker in breakers)
        {
            ranked.Add(new KeyValuePair<float, WallTimeBreaker>(GetDistance(breaker, origin), breaker));
        }

        ranked.Sort((a, b) => outerFirst ? b.Key.CompareTo(a.Key) : a.Key.CompareTo(b.Key));

        int group = 0;
        float groupDistance = ranked[0].Key;
        foreach (KeyValuePair<float, WallTimeBreaker> pair in ranked)
        {
            if (Mathf.Abs(pair.Key - groupDistance) > tieTolerance)
            {
                group++;
                groupDistance = pair.Key;
            }
            pair.Value.timeToBreak = startDelay + group * interval;
        }
    }

    private float GetDistance(WallTimeBreaker breaker, Vector3 origin)
    {
        Vector3 position = GetWallCenter(breaker);
        return Mathf.Max(Mathf.Abs(position.x - origin.x), Mathf.Abs(position.z - origin.z));
    }

    private Vector3 GetWallCenter(WallTimeBreaker breaker)
    {
        Renderer wallRenderer = breaker.GetComponentInChildren<Renderer>();
        return wallRenderer != null ? wallRenderer.bounds.center : breaker.transform.position;
    }
}
