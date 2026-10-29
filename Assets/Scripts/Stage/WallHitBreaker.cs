using UnityEngine;

/// <summary>
/// プレイヤーがぶつかった回数で壁を壊す。BreakableWall と同じ GameObject に付ける。
/// 衝突は PlayerCollisionHandler が RegisterHit を呼んで通知する。
/// </summary>
[RequireComponent(typeof(BreakableWall))]
public class WallHitBreaker : MonoBehaviour
{
    [Tooltip("この回数ぶつかると壁が消える")]
    [Min(1)] public int hitsToBreak = 3;

    [Tooltip("同じ壁で連続してカウントしないための待ち時間(秒)")]
    public float hitCooldown = 0.4f;

    [Tooltip("これより弱い衝突はカウントしない(相対速度)")]
    public float minImpactSpeed = 1f;

    private BreakableWall wall;
    private int hitCount;
    private float lastHitTime = -999f;

    private void Awake()
    {
        wall = GetComponent<BreakableWall>();
    }

    // プレイヤーがぶつかったときに PlayerCollisionHandler から呼ばれる
    public void RegisterHit(float impactSpeed)
    {
        if (impactSpeed < minImpactSpeed) return;
        if (Time.time - lastHitTime < hitCooldown) return;

        lastHitTime = Time.time;
        hitCount++;

        if (hitCount >= hitsToBreak)
        {
            wall.Break();
            return;
        }
        wall.ApplyProgress((float)hitCount / hitsToBreak);
    }
}
