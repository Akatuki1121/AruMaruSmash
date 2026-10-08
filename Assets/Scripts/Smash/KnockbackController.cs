using UnityEngine;

/// <summary>
/// プレイヤーのノックバック状態（衝突による吹っ飛び）を管理するクラス。
///
/// 役割：
/// - 衝突等で受けたXZ方向の外力（knockbackVelocity）の保持と時間減衰
/// - 衝突直後の「入力完全ロック」期間の管理
/// - 吹っ飛び状態（着地まで継続）の管理。TopDownPlayerMoveはこの状態を読んで入力を止める
/// - 吹っ飛び中の重力倍率
/// - Y方向（上向き）の吹っ飛び初速、XZ速度の直接指定、上向き速度の制限（Rigidbodyへの書き込みはここに集約）
///
/// 担当しない処理：
/// - 衝突の解釈（相手・壁の分類、勝敗）→ PlayerCollisionHandler
/// - 通常移動・ダッシュ → TopDownPlayerMove
///
/// MoveManagerTest（旧移動）はこのクラスが公開する状態（CurrentKnockbackVelocity, IsLocked,
/// IsAirControlWeakened, AirControlMultiplier）を読むだけで、自分でタイマーやノックバック量を管理する必要がない。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class KnockbackController : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("接地判定。未設定の場合は同じオブジェクトから自動取得する")]
    public GroundChecker groundChecker;

    [Header("キャラクターデータ（各値のデフォルト。未設定ならコード上の初期値を使う）")]
    public CharacterStats stats;

    // 値の決定順：このコンポーネントの「上書き」がON → その値 / OFF → CharacterStatsの値
    private CharacterStats Stats => stats != null ? stats : CharacterStats.Fallback;

    [Header("衝突後の制御不能時間")]
    [Tooltip("吹っ飛び・押し負け直後、プレイヤー入力での移動を完全に無効化する時間（秒）。上げるほど長く操作できない")]
    public FloatOverride knockbackLockDuration;

    [Header("ノックバックの減衰")]
    [Tooltip("ノックバック速度が時間経過で0に近づく早さ。上げるほど早く収まる。下げるほど長く滑る")]
    public FloatOverride knockbackDecaySpeed;

    [Header("吹っ飛び中の落下")]
    [Tooltip("吹っ飛ばされて着地するまでの間、重力を何倍にするか。1=通常の重力。上げるほど速く落ちる")]
    public FloatOverride knockbackGravityMultiplier;

    [Header("旧移動スクリプト用（MoveManagerTest）")]
    [Tooltip("この値以上のノックバック速度が残っている間だけ「空中制御弱体化」とみなす")]
    public FloatOverride airControlKnockbackThreshold;

    [Header("効果音")]
    public AudioClip HitSound;

    private float KnockbackLockDuration => knockbackLockDuration.Resolve(Stats.knockbackLockDuration);
    private float KnockbackDecaySpeed => knockbackDecaySpeed.Resolve(Stats.knockbackDecaySpeed);
    private float KnockbackGravityMultiplier => knockbackGravityMultiplier.Resolve(Stats.knockbackGravityMultiplier);
    private float AirControlKnockbackThreshold => airControlKnockbackThreshold.Resolve(Stats.airControlKnockbackThreshold);

    private Rigidbody rb;
    private AudioSource audioSource;

    // 現在保持しているXZ方向のノックバック速度
    private Vector3 knockbackVelocity = Vector3.zero;
    private float knockbackLockTimer = 0f;

    // 吹っ飛び状態：最低限の時間が過ぎたうえで着地するまで継続する
    private const float KNOCKED_BACK_DURATION = 0.3f;
    private const float KNOCKED_BACK_MAX_AIR_TIME = 3f;   // 着地を検知できなかった場合の安全弁（秒）
    private float knockedBackTimer = 0f;
    private bool knockedAirborne = false;

    // 物理ステップ直前（＝衝突前）のRigidbody速度。ボタン加速中の勝者が「反発しなかった」ことにするために使う
    private Vector3 preStepVelocity = Vector3.zero;

    // 着地検知用：前フレームの接地状態を記憶しておき、非接地→接地に変わった瞬間を捉える
    private bool wasGroundedLastFrame = true;

    public Vector3 CurrentKnockbackVelocity => knockbackVelocity;
    public Vector3 PreStepVelocity => preStepVelocity;

    // 接地状態をPlayerCollisionHandler等の外部クラスからも参照できるように公開する
    public bool IsGrounded => groundChecker != null && groundChecker.IsGrounded;

    // 吹っ飛び中（着地するまで）かどうか。TopDownPlayerMoveはこれがtrueの間、入力による移動を止める
    public bool IsKnockedBack => knockedBackTimer > 0f || knockedAirborne;

    // 衝突直後、入力を完全に無視すべき期間中かどうか
    public bool IsLocked => knockbackLockTimer > 0f;

    // ロックは終わっているが、空中にいて、まだ知覚できる程度のノックバックが残っている間
    public bool IsAirControlWeakened =>
        !IsLocked &&
        groundChecker != null &&
        !groundChecker.IsGrounded &&
        knockbackVelocity.sqrMagnitude > AirControlKnockbackThreshold * AirControlKnockbackThreshold;

    // 旧移動スクリプト用。TopDownPlayerMoveの空中制御はTopDownPlayerMove側のairControlMultiplierを使う
    public float AirControlMultiplier => Stats.airControlMultiplier;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (groundChecker == null)
        {
            groundChecker = GetComponent<GroundChecker>();
        }

        wasGroundedLastFrame = IsGrounded;

        audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (knockbackLockTimer > 0f)
        {
            knockbackLockTimer -= Time.deltaTime;
        }

        // 着地検知：前フレームは空中で、今フレームで接地した瞬間にロックを強制解除する
        bool isGroundedNow = IsGrounded;
        if (isGroundedNow && !wasGroundedLastFrame)
        {
            knockbackLockTimer = 0f;
        }
        wasGroundedLastFrame = isGroundedNow;

        // 吹っ飛び状態：最低限の時間が過ぎたうえで着地したら終了する
        // （着地を検知できない場合でも、安全弁の時間を超えたら終了する）
        if (knockedAirborne && knockedBackTimer <= 0f &&
            (isGroundedNow || knockedBackTimer < -KNOCKED_BACK_MAX_AIR_TIME))
        {
            knockedAirborne = false;
        }

        if (IsKnockedBack)
        {
            knockedBackTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// 吹っ飛び中かつ空中のときだけ、追加の重力を与えて落下を速くする。
    /// 通常の重力（Rigidbodyの重力）はそのままで、倍率の超過分だけを加速度として足す。
    /// </summary>
    private void ApplyKnockbackGravity()
    {
        float multiplier = KnockbackGravityMultiplier;
        if (multiplier <= 1f) return;
        if (!IsKnockedBack) return;
        if (IsGrounded) return;

        rb.AddForce(Physics.gravity * (multiplier - 1f), ForceMode.Acceleration);
    }

    private void FixedUpdate()
    {
        preStepVelocity = rb.linearVelocity;

        // 吹っ飛び中（着地するまで）は重力を強めて落下を速くする
        ApplyKnockbackGravity();

        // ノックバック速度の時間減衰（XZ方向のみ。Y方向の落下は重力に任せる）
        if (knockbackVelocity.sqrMagnitude > 0.01f)
        {
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.fixedDeltaTime * KnockbackDecaySpeed);
        }
        else
        {
            knockbackVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// XZ方向の外力をノックバック速度に加算し、入力ロック時間をリセットする。
    /// </summary>
    public void ApplyKnockback(Vector3 horizontalForce)
    {
        horizontalForce.y = 0f;
        knockbackVelocity += horizontalForce;
        knockbackLockTimer = KnockbackLockDuration;
    }

    /// <summary>
    /// Y方向（上向き）の吹っ飛び初速を与え、吹っ飛び状態（着地まで継続）にする。
    /// 落下は重力に任せるため、ここでは初速の代入のみ行う。
    /// </summary>
    public void ApplyUpwardBounce(float upForce)
    {
        PlayHitSound();

        Vector3 v = rb.linearVelocity;
        v.y = upForce;
        rb.linearVelocity = v;

        knockedBackTimer = KNOCKED_BACK_DURATION;
        knockedAirborne = true;
    }

    /// <summary>
    /// RigidbodyのXZ速度を直接指定する（Y速度は保持）。加速中の勝敗の結果を反映するために使う。
    /// </summary>
    public void SetHorizontalVelocity(Vector3 horizontalVelocity)
    {
        Vector3 v = rb.linearVelocity;
        rb.linearVelocity = new Vector3(horizontalVelocity.x, v.y, horizontalVelocity.z);
    }

    /// <summary>
    /// XZ速度を衝突前（直前の物理ステップ開始時）の値に戻す。勝者が「反発しなかった」状態にするために使う。
    /// </summary>
    public void RestoreHorizontalVelocity()
    {
        SetHorizontalVelocity(preStepVelocity);
    }

    /// <summary>
    /// 上向きの速度をmaxUpwardSpeed以下に制限する。壁に当たって上へ飛ぶのを防ぐために使う。
    /// 制限前の上向き速度を返す（制限が不要だった場合もその値を返す）。
    /// </summary>
    public float LimitUpwardVelocity(float maxUpwardSpeed)
    {
        Vector3 v = rb.linearVelocity;
        float before = v.y;
        if (before > maxUpwardSpeed)
        {
            v.y = maxUpwardSpeed;
            rb.linearVelocity = v;
        }
        return before;
    }

    /// <summary>
    /// 衝突音
    /// </summary>
    public void PlayHitSound()
    {
        if (audioSource != null && HitSound != null)
        {
            audioSource.PlayOneShot(HitSound);
        }
    }
}
