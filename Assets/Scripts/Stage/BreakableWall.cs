
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 壊れる壁の本体。壊したい壁の GameObject に付ける。
/// 壊れる条件は一緒に付けるコンポーネントで決まる。
///   回数で壊す: WallHitBreaker  /  時間で壊す: WallTimeBreaker  /  両方付ければ先に条件を満たした方で壊れる
/// 各 Breaker が進行度(0〜1)を ApplyProgress で伝え、色が crackedColor に近づく。
/// 1に達する(Break が呼ばれる)と当たり判定ごと消える。
/// 壊れた壁は Revive で元の状態(色・回数・タイマー)に戻して復活させられる。
/// </summary>
public class BreakableWall : MonoBehaviour
{
    [Tooltip("進行するほど近づく色(壁の色が段階的に変わる)")]
    public Color crackedColor = new Color(0.35f, 0.3f, 0.3f, 1f);

    // 壊れたとき／復活したときに通知する。管理側(WallRandomManager など)が購読する。
    // 壊れた壁は SetActive(false) で見つけにくくなるため、管理側は壁の一覧を事前に保持しておく。
    public event Action<BreakableWall> Broken;
    public event Action<BreakableWall> Revived;

    // 壊れている間は true。Revive で false に戻る
    public bool IsBroken { get; private set; }

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
        if (IsBroken) return;

        IsBroken = true;
        gameObject.SetActive(false);
        Broken?.Invoke(this);
    }

    // 壊れた壁を元の状態に戻して復活させる。壊れていないときは何もしない。
    // 進行度・色・各 Breaker の状態(回数・クールダウン・開始時刻)を全て初期化する。
    public void Revive()
    {
        if (!IsBroken) return;

        IsBroken = false;
        currentProgress = 0f;
        gameObject.SetActive(true);
        ApplyTint(0f);
        ResetBreakers();
        Revived?.Invoke(this);
    }

    private void ResetBreakers()
    {
        WallHitBreaker hitBreaker = GetComponent<WallHitBreaker>();
        if (hitBreaker != null)
        {
            hitBreaker.ResetHits();
        }

        WallTimeBreaker timeBreaker = GetComponent<WallTimeBreaker>();
        if (timeBreaker != null)
        {
            timeBreaker.ResetTimer();
        }
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
