using UnityEngine;

/// <summary>
/// 時間経過で壁を壊す。BreakableWall と同じ GameObject に付ける。
/// 四方の壁で timeToBreak をずらすと、順番に壁がなくなる。
/// </summary>
[RequireComponent(typeof(BreakableWall))]
public class WallTimeBreaker : MonoBehaviour
{
    [Tooltip("壁が有効になってから消えるまでの秒数。四方の壁で値をずらすと順番に消える")]
    [Min(0.1f)] public float timeToBreak = 30f;

    private BreakableWall wall;
    private float activatedTime;

    private void Awake()
    {
        wall = GetComponent<BreakableWall>();
    }

    private void OnEnable()
    {
        activatedTime = Time.time;
    }

    private void Update()
    {
        float progress = (Time.time - activatedTime) / timeToBreak;
        if (progress >= 1f)
        {
            wall.Break();
            return;
        }
        wall.ApplyProgress(progress);
    }
}
