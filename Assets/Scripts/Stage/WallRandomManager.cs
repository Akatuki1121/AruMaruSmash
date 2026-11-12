
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 壁をランダムに壊し、壊れた壁をランダムな時間後に復活させる。シーンに1つ置く。
/// 対象はシーン内の全 BreakableWall(開始時に非アクティブな壁も含めて取得し、一覧を保持する)。
/// 復活は壊れた原因を問わない(ランダム破壊・衝突・WallTimeBreaker のどれで壊れた壁も戻る)。
/// WallTimeBreaker / WallTimeSequencer とは独立に動く。ランダム破壊だけが「最低残す壁の数」で歯止めされる。
/// </summary>
public class WallRandomManager : MonoBehaviour
{
    [Header("ON / OFF")]
    [Tooltip("ONなら、生きている壁からランダムに1つずつ壊す")]
    public bool enableRandomBreak = true;

    [Tooltip("ONなら、壊れた壁を壊れてからランダムな時間後に復活させる(壊れた原因は問わない)")]
    public bool enableRandomRevive = true;

    [Header("ランダム破壊")]
    [Tooltip("ゲーム開始から最初のランダム破壊までの猶予(秒)")]
    [Min(0f)] public float startDelay = 10f;

    [Tooltip("次のランダム破壊までの間隔の最小値(秒)")]
    [Min(0.1f)] public float minBreakInterval = 8f;

    [Tooltip("次のランダム破壊までの間隔の最大値(秒)")]
    [Min(0.1f)] public float maxBreakInterval = 15f;

    [Tooltip("生きている壁がこの数以下のときは、ランダム破壊をしない(壁が減りすぎないための歯止め)")]
    [Min(0)] public int minAliveWalls = 8;

    [Tooltip("ONなら、直前にランダム破壊した壁を続けて選ばない")]
    public bool avoidRepeatBreak = true;

    [Header("ランダム復活")]
    [Tooltip("壊れてから復活するまでの時間の最小値(秒)")]
    [Min(0.1f)] public float minReviveDelay = 12f;

    [Tooltip("壊れてから復活するまでの時間の最大値(秒)")]
    [Min(0.1f)] public float maxReviveDelay = 25f;

    private readonly List<BreakableWall> walls = new List<BreakableWall>();
    private readonly Dictionary<BreakableWall, float> reviveTimes = new Dictionary<BreakableWall, float>();
    private readonly List<BreakableWall> candidates = new List<BreakableWall>();

    private float nextBreakTime;
    private BreakableWall lastRandomBroken;

    private void OnValidate()
    {
        maxBreakInterval = Mathf.Max(maxBreakInterval, minBreakInterval);
        maxReviveDelay = Mathf.Max(maxReviveDelay, minReviveDelay);
    }

    private void Start()
    {
        // 壊れた壁は SetActive(false) になり後から Find で見つけにくいので、非アクティブ込みで最初に全件保持する
        walls.AddRange(FindObjectsByType<BreakableWall>(FindObjectsInactive.Include));

        foreach (BreakableWall wall in walls)
        {
            wall.Broken += OnWallBroken;
            wall.Revived += OnWallRevived;

            if (wall.IsBroken)
            {
                ScheduleRevive(wall);
            }
        }

        nextBreakTime = Time.time + startDelay;
    }

    private void OnDestroy()
    {
        foreach (BreakableWall wall in walls)
        {
            if (wall == null) continue;

            wall.Broken -= OnWallBroken;
            wall.Revived -= OnWallRevived;
        }
    }

    private void Update()
    {
        if (enableRandomRevive)
        {
            TryReviveDueWall();
        }

        if (enableRandomBreak)
        {
            TryBreakRandomWall();
        }
    }

    private void OnWallBroken(BreakableWall wall)
    {
        ScheduleRevive(wall);
    }

    private void OnWallRevived(BreakableWall wall)
    {
        reviveTimes.Remove(wall);
    }

    // 壊れた時点で復活時刻を決める。トグルがOFFの間に壊れた壁も、ONにすれば時間が来たものから戻る
    private void ScheduleRevive(BreakableWall wall)
    {
        reviveTimes[wall] = Time.time + Random.Range(minReviveDelay, maxReviveDelay);
    }

    private void TryReviveDueWall()
    {
        candidates.Clear();
        foreach (BreakableWall wall in walls)
        {
            if (wall == null || !wall.IsBroken) continue;
            if (!reviveTimes.TryGetValue(wall, out float reviveTime)) continue;
            if (Time.time < reviveTime) continue;

            candidates.Add(wall);
        }

        if (candidates.Count == 0) return;

        // 同時に時間が来た壁があっても、1回に復活させるのは1つだけ
        candidates[Random.Range(0, candidates.Count)].Revive();
    }

    private void TryBreakRandomWall()
    {
        if (Time.time < nextBreakTime) return;

        nextBreakTime = Time.time + Random.Range(minBreakInterval, maxBreakInterval);

        if (CountAliveWalls() <= minAliveWalls) return;

        BreakableWall target = PickRandomAliveWall();
        if (target == null) return;

        lastRandomBroken = target;
        target.Break();
    }

    // 生きている壁 = 壊れておらず、階層上も有効な壁
    private static bool IsAlive(BreakableWall wall)
    {
        return wall != null && !wall.IsBroken && wall.gameObject.activeInHierarchy;
    }

    private int CountAliveWalls()
    {
        int count = 0;
        foreach (BreakableWall wall in walls)
        {
            if (IsAlive(wall))
            {
                count++;
            }
        }

        return count;
    }

    private BreakableWall PickRandomAliveWall()
    {
        candidates.Clear();
        foreach (BreakableWall wall in walls)
        {
            if (IsAlive(wall))
            {
                candidates.Add(wall);
            }
        }

        // 候補が他にもあるときだけ、直前に壊した壁を外す
        if (avoidRepeatBreak && candidates.Count > 1)
        {
            candidates.Remove(lastRandomBroken);
        }

        if (candidates.Count == 0) return null;

        return candidates[Random.Range(0, candidates.Count)];
    }
}
