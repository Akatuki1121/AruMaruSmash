using UnityEngine;

/// <summary>
/// 壊れる壁の本体。壊したい壁の GameObject に付ける。
/// 壊れる条件は一緒に付けるコンポーネントで決まる。
///   回数で壊す: WallHitBreaker  /  時間で壊す: WallTimeBreaker  /  両方付ければ先に条件を満たした方で壊れる
/// 各 Breaker が進行度(0〜1)を ApplyProgress で伝え、色が crackedColor に近づく。
/// 1に達する(Break が呼ばれる)と当たり判定ごと消える。
/// </summary>
public class BreakableWall : MonoBehaviour
{
    [Tooltip("進行するほど近づく色(壁の色が段階的に変わる)")]
    public Color crackedColor = new Color(0.35f, 0.3f, 0.3f, 1f);

    private Renderer wallRenderer;
    private MaterialPropertyBlock block;
    private int colorId = -1;
    private Color baseColor;

    private float currentProgress;

    private void Awake()
    {
        wallRenderer = GetComponentInChildren<Renderer>();
        if (wallRenderer == null) return;

        Material material = wallRenderer.sharedMaterial;
        if (material != null)
        {
            if (material.HasProperty("_BaseColor")) colorId = Shader.PropertyToID("_BaseColor");
            else if (material.HasProperty("_Color")) colorId = Shader.PropertyToID("_Color");
            if (colorId != -1) baseColor = material.GetColor(colorId);
        }
        block = new MaterialPropertyBlock();
    }

    // 各 Breaker から呼ばれる。複数の Breaker がある場合は最も進んでいる値を採用する
    public void ApplyProgress(float progress)
    {
        if (progress <= currentProgress) return;

        currentProgress = Mathf.Clamp01(progress);
        ApplyTint(currentProgress);
    }

    public void Break()
    {
        gameObject.SetActive(false);
    }

    private void ApplyTint(float t)
    {
        if (wallRenderer == null || colorId == -1) return;
        wallRenderer.GetPropertyBlock(block);
        block.SetColor(colorId, Color.Lerp(baseColor, crackedColor, t));
        wallRenderer.SetPropertyBlock(block);
    }
}
