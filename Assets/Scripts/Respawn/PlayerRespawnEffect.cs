using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 復活直後のプレイヤーに付ける一時的なコンポーネント（RespawnManagerが復活のたびに付ける。終わると自分で消える）。
///
/// 流れ：
/// 1. 出現（Emerging）：ステージの床の下から、穴を掘って出てくるように床の上へせり上がる。
///    この間は移動とチャージができない（方向の入力だけは受け付ける）。
/// 2. 無敵（Invincible）：出現演出が終わったあとも invincibleDuration 秒、無敵が続く。
///
/// 無敵は出現開始から付き、出現演出中と、その後 invincibleDuration 秒間続く。
/// 無敵中は他プレイヤーとの当たり（反発・押し出し）をすべて無効にし、姿を点滅させる。
/// 壁・床との当たりは通常どおり残るので、無敵中でも床の外へ出れば落下する。
/// </summary>
[DisallowMultipleComponent]
public class PlayerRespawnEffect : MonoBehaviour
{
    private enum Stage
    {
        Emerging,
        Invincible,
        Done,
    }

    private Stage stage = Stage.Done;
    private float timer;

    private Vector3 startPosition;
    private Vector3 endPosition;
    private float emergeDuration;
    private float invincibleDuration;
    private float blinkInterval;

    private Rigidbody rb;
    private KnockbackController knockback;
    private TopDownPlayerMove move;
    private Collider[] myColliders;

    // 当たりを無効にした相手と、点滅対象のRenderer
    private readonly HashSet<PlayerCollisionHandler> ignoredPlayers = new();
    private Renderer[] blinkRenderers;
    private bool[] originalForceRenderingOff;
    private float blinkTimer;
    private bool blinkVisible = true;

    /// <summary>出現演出中（床の下からせり上がっている間）</summary>
    public bool IsEmerging => stage == Stage.Emerging;

    /// <summary>無敵中（出現中と、出現後の無敵時間）</summary>
    public bool IsInvincible => stage != Stage.Done;

    /// <summary>
    /// 出現演出と無敵を始める。startPosition から endPosition へ emergeDuration 秒でせり上がり、
    /// 出現開始から無敵にし、出現後も invincibleDuration 秒間無敵にする。
    /// </summary>
    public void Begin(Vector3 startPosition, Vector3 endPosition, float emergeDuration, float invincibleDuration, float blinkInterval)
    {
        this.startPosition = startPosition;
        this.endPosition = endPosition;
        this.emergeDuration = Mathf.Max(0.01f, emergeDuration);
        this.invincibleDuration = Mathf.Max(0f, invincibleDuration);
        this.blinkInterval = Mathf.Max(0.01f, blinkInterval);

        rb = GetComponent<Rigidbody>();
        knockback = GetComponent<KnockbackController>();
        move = GetComponent<TopDownPlayerMove>();
        myColliders = GetComponentsInChildren<Collider>();
        blinkRenderers = GetComponentsInChildren<Renderer>(true);
        originalForceRenderingOff = new bool[blinkRenderers.Length];
        for (int i = 0; i < blinkRenderers.Length; i++)
        {
            originalForceRenderingOff[i] = blinkRenderers[i].forceRenderingOff;
        }

        // 出現中は物理に任せず位置を直接動かす。KnockbackControllerは速度を毎フレーム書き込むため止めておく
        if (move != null) move.IsEmerging = true;
        if (knockback != null) knockback.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        timer = 0f;
        blinkTimer = 0f;
        blinkVisible = true;
        stage = Stage.Emerging;

        ApplyPosition(startPosition);
        IgnoreOtherPlayers();
    }

    private void Update()
    {
        if (stage == Stage.Done) return;

        timer += Time.deltaTime;

        if (stage == Stage.Emerging)
        {
            float t = Mathf.Clamp01(timer / emergeDuration);
            ApplyPosition(Vector3.Lerp(startPosition, endPosition, Mathf.SmoothStep(0f, 1f, t)));

            if (t >= 1f) EndEmerge();
        }
        if (stage == Stage.Invincible && timer >= invincibleDuration)
        {
            EndInvincible();
            return;
        }

        // 無敵中に復活した他プレイヤーとも当たらないようにする
        IgnoreOtherPlayers();
        UpdateBlink();
    }

    private void OnDestroy()
    {
        // 途中でプレイヤーが消えた（落下など）ときは、ほかのプレイヤーとの当たりだけ元に戻す
        if (stage != Stage.Done) RestoreCollisions();
    }

    private void ApplyPosition(Vector3 position)
    {
        transform.position = position;
        if (rb != null) rb.position = position;
    }

    // 出現が終わったら物理に戻す（ここから移動・チャージができる。無敵は続く）
    private void EndEmerge()
    {
        ApplyPosition(endPosition);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
        }
        if (knockback != null) knockback.enabled = true;
        if (move != null) move.IsEmerging = false;

        timer = 0f;
        stage = Stage.Invincible;

        // 出現中は床の下で見えないので、点滅は床の上に出た時点から数え直す（出た瞬間は見える状態から始める）
        blinkTimer = 0f;
        SetBlinkVisible(true);
    }

    private void EndInvincible()
    {
        // IsInvincible を先に false にしてから当たりを戻す（相手が同時に無敵のとき、後に終わる側が戻す）
        stage = Stage.Done;
        RestoreCollisions();
        SetBlinkVisible(true);
        Destroy(this);
    }

    // ===== 当たりの無効化 =====

    // 他のプレイヤーのコライダーとの衝突を無効にする（新しく増えたプレイヤーだけ処理する）
    private void IgnoreOtherPlayers()
    {
        PlayerCollisionHandler[] players = FindObjectsByType<PlayerCollisionHandler>();
        foreach (PlayerCollisionHandler other in players)
        {
            if (other == null || other.gameObject == gameObject || ignoredPlayers.Contains(other)) continue;

            SetIgnore(other, true);
            ignoredPlayers.Add(other);
        }
    }

    private void RestoreCollisions()
    {
        foreach (PlayerCollisionHandler other in ignoredPlayers)
        {
            if (other == null) continue;

            // 相手もまだ無敵なら、相手が終わるときに戻す
            if (other.TryGetComponent(out PlayerRespawnEffect otherEffect) && otherEffect.IsInvincible) continue;

            SetIgnore(other, false);
        }
        ignoredPlayers.Clear();
    }

    private void SetIgnore(PlayerCollisionHandler other, bool ignore)
    {
        Collider[] otherColliders = other.GetComponentsInChildren<Collider>();
        foreach (Collider mine in myColliders)
        {
            if (mine == null) continue;
            foreach (Collider theirs in otherColliders)
            {
                if (theirs != null) Physics.IgnoreCollision(mine, theirs, ignore);
            }
        }
    }

    // ===== 点滅 =====

    private void UpdateBlink()
    {
        blinkTimer += Time.deltaTime;
        while (blinkTimer >= blinkInterval)
        {
            blinkTimer -= blinkInterval;
            SetBlinkVisible(!blinkVisible);
        }
    }

    // Renderer自体は有効のまま描画だけを切り替える
    private void SetBlinkVisible(bool visible)
    {
        blinkVisible = visible;

        for (int i = 0; i < blinkRenderers.Length; i++)
        {
            if (blinkRenderers[i] != null)
            {
                blinkRenderers[i].forceRenderingOff = visible && originalForceRenderingOff[i];
                if (!visible) blinkRenderers[i].forceRenderingOff = true;
            }
        }
    }
}
