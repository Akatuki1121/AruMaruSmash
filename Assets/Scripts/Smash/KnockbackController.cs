using UnityEngine;

/// <summary>
/// プレイヤーのノックバック状態（衝突による吹っ飛び）を管理するクラス。
///
/// 役割：
/// - TopDownPlayerMoveから通常移動・ダッシュ速度を取得し、重力・ノックバック速度と合成してRigidbodyに反映
/// - 衝突で受けたXZ方向のノックバック速度の保持と時間減衰
/// - 入力ロック期間、吹っ飛び状態、吹っ飛び中の重力倍率の管理
/// - Y方向の吹っ飛び初速と、上向き速度の制限
///
/// MoveManagerTest（旧移動）はこのクラスが公開する状態（CurrentKnockbackVelocity, IsLocked,
/// IsAirControlWeakened, AirControlMultiplier）を参照する。
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
    private TopDownPlayerMove topDownMove;
    private bool applyGravity;

    // 現在保持しているXZ方向のノックバック速度
    private Vector3 knockbackVelocity = Vector3.zero;
    private float upwardKnockbackVelocity;
    private Vector3 gravityVelocity = Vector3.zero;
    private Vector3 movementVelocity = Vector3.zero;
    private Vector3 finalVelocity = Vector3.zero;
    private float knockbackLockTimer = 0f;
    private bool hasUpwardVelocityLimit;
    private float requestedMaxUpwardVelocity;

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
        topDownMove = GetComponent<TopDownPlayerMove>();
        applyGravity = rb.useGravity;
        rb.useGravity = false;

        Vector3 initialVelocity = rb.linearVelocity;
        knockbackVelocity = new Vector3(initialVelocity.x, 0f, initialVelocity.z);
        gravityVelocity.y = initialVelocity.y;
        finalVelocity = initialVelocity;

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

    private void FixedUpdate()
    {
        preStepVelocity = finalVelocity;

        bool isGrounded = IsGrounded;
        Vector3 targetMovementVelocity = topDownMove != null
            ? topDownMove.GetNormalMoveVelocity() + topDownMove.GetDashVelocity()
            : Vector3.zero;

        if (IsLocked || IsKnockedBack)
        {
            movementVelocity = Vector3.zero;
        }
        else if (isGrounded)
        {
            movementVelocity = targetMovementVelocity;
        }
        else
        {
            float airControl = topDownMove != null
                ? topDownMove.AirControlMultiplier
                : AirControlMultiplier;
            movementVelocity = Vector3.Lerp(movementVelocity, targetMovementVelocity, Mathf.Clamp01(airControl));
        }

        if (applyGravity)
        {
            float gravityMultiplier = IsKnockedBack && !isGrounded ? KnockbackGravityMultiplier : 1f;
            gravityVelocity += Physics.gravity * (gravityMultiplier * Time.fixedDeltaTime);
        }

        if (isGrounded && upwardKnockbackVelocity + gravityVelocity.y <= 0f)
        {
            upwardKnockbackVelocity = 0f;
            gravityVelocity.y = 0f;
        }

        // ノックバック速度の時間減衰（XZ方向のみ。Y方向の落下は重力に任せる）
        if (knockbackVelocity.sqrMagnitude > 0.01f)
        {
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.fixedDeltaTime * KnockbackDecaySpeed);
        }
        else
        {
            knockbackVelocity = Vector3.zero;
        }

        Vector3 velocity = movementVelocity + knockbackVelocity + gravityVelocity;
        velocity.y += upwardKnockbackVelocity;

        if (hasUpwardVelocityLimit && velocity.y > requestedMaxUpwardVelocity)
        {
            upwardKnockbackVelocity -= velocity.y - requestedMaxUpwardVelocity;
            velocity.y = requestedMaxUpwardVelocity;
        }

        rb.linearVelocity = velocity;
        finalVelocity = velocity;
        hasUpwardVelocityLimit = false;
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
        upwardKnockbackVelocity = upForce;

        knockedBackTimer = KNOCKED_BACK_DURATION;
        knockedAirborne = true;
    }

    /// <summary>
    /// 壁接触時の上向き速度上限を次の物理ステップで適用する。
    /// </summary>
    public float LimitUpwardVelocity(float maxUpwardSpeed)
    {
        float currentUpwardVelocity = finalVelocity.y;
        requestedMaxUpwardVelocity = hasUpwardVelocityLimit
            ? Mathf.Min(requestedMaxUpwardVelocity, maxUpwardSpeed)
            : maxUpwardSpeed;
        hasUpwardVelocityLimit = true;
        return currentUpwardVelocity;
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
