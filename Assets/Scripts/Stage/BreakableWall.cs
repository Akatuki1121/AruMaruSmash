using System.Collections.Generic;
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

    // URP/Lit、Standard、および一般的なTint系シェーダーの色プロパティ。
    // マテリアルごとに HasProperty で確認し、存在するものだけを使用する。
    private static readonly int[] ColorPropertyIds =
    {
        Shader.PropertyToID("_BaseColor"),
        Shader.PropertyToID("_Color"),
        Shader.PropertyToID("_TintColor"),
        Shader.PropertyToID("_BaseColorFactor")
    };

    private sealed class WallColorTarget
    {
        public Renderer Renderer;
        public int MaterialIndex;
        public int ColorPropertyId;
        public Color OriginalColor;
    }

    private readonly List<WallColorTarget> colorTargets = new List<WallColorTarget>();
    private MaterialPropertyBlock block;
    private float currentProgress;

    private void Awake()
    {
        block = new MaterialPropertyBlock();

        // 壁の子階層にある全 Renderer と、各 Renderer の全マテリアルスロットを対象にする。
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null) continue;

            Material[] materials = targetRenderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null) continue;

                int colorPropertyId = FindColorProperty(material);
                if (colorPropertyId == -1) continue;

                colorTargets.Add(new WallColorTarget
                {
                    Renderer = targetRenderer,
                    MaterialIndex = materialIndex,
                    ColorPropertyId = colorPropertyId,
                    OriginalColor = material.GetColor(colorPropertyId)
                });
            }
        }
    }

    private static int FindColorProperty(Material material)
    {
        for (int i = 0; i < ColorPropertyIds.Length; i++)
        {
            int propertyId = ColorPropertyIds[i];
            if (material.HasProperty(propertyId))
            {
                return propertyId;
            }
        }

        return -1;
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
        foreach (WallColorTarget target in colorTargets)
        {
            if (target.Renderer == null) continue;

            // 既存の PropertyBlock を取得してから色だけを上書きし、
            // 他のスクリプトが設定したプロパティを維持する。
            target.Renderer.GetPropertyBlock(block, target.MaterialIndex);
            block.SetColor(
                target.ColorPropertyId,
                Color.Lerp(target.OriginalColor, crackedColor, t)
            );
            target.Renderer.SetPropertyBlock(block, target.MaterialIndex);
        }
    }
}
