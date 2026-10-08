using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// プレイヤーのノックバック状態（衝突による吹っ飛び）を管理するクラス。
///
/// 役割：
/// - 衝突等で受けたXZ方向の外力（knockbackVelocity）の保持と時間減衰
/// - 衝突直後の「入力完全ロック」期間の管理
/// - ロック終了後、空中にいる間だけ入力の効きを弱める「空中制御弱体化」の判定
/// - Y方向（上向き）の吹っ飛び初速の適用（rb.linearVelocity.yに直接反映、落下は重力に任せる）
///
/// MoveManagerTestはこのクラスが公開する状態（CurrentKnockbackVelocity, IsLocked,
/// IsAirControlWeakened, AirControlMultiplier）を読むだけで、
/// 自分でタイマーやノックバック量を管理する必要がない。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class KnockbackController : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("接地判定。未設定の場合は同じオブジェクトから自動取得する")]
    public GroundChecker groundChecker;

    private Rigidbody rb;

    [Header("衝突後の制御不能時間")]
    [Tooltip("吹っ飛び・押し負け直後、プレイヤー入力での移動を完全に無効化する時間（秒）")]
    public float knockbackLockDuration = 0.25f;

    [Header("空中制御弱体化")]
    [Tooltip("ロック終了後、空中にいる間の入力の効き具合（0=入力無効、1=通常と同じ）")]
    [Range(0f, 1f)]
    public float airControlMultiplier = 0.3f;

    [Tooltip("この値以上のノックバック量が残っている間だけ「空中制御弱体化」とみなす")]
    public float airControlKnockbackThreshold = 0.5f;

    [Header("ノックバックの減衰")]
    [Tooltip("ノックバック速度が時間経過でどれだけ早く0に近づくか")]
    public float knockbackDecaySpeed = 4f;

    [Header("吹っ飛び中の落下")]
    [Tooltip("吹っ飛ばされて着地するまでの間、重力を何倍にするか（1=通常の重力。大きいほど速く落ちる）")]
    [Min(1f)]
    public float knockbackGravityMultiplier = 3f;

    [Header("キャラクターデータ（設定時は上記の初期値を上書きします）")]
    public CharacterStats stats;

    // 現在保持しているXZ方向のノックバック速度
    private Vector3 knockbackVelocity = Vector3.zero;
    private float knockbackLockTimer = 0f;

    // 着地検知用：前フレームの接地状態を記憶しておき、非接地→接地に変わった瞬間を捉える
    private bool wasGroundedLastFrame = true;

    public Vector3 CurrentKnockbackVelocity => knockbackVelocity;

    // 接地状態をPlayerCollisionHandler等の外部クラスからも参照できるように公開する
    public bool IsGrounded => groundChecker != null && groundChecker.IsGrounded;

    // 衝突直後、入力を完全に無視すべき期間中かどうか
    public bool IsLocked => knockbackLockTimer > 0f;

    // ロックは終わっているが、空中にいて、まだ知覚できる程度のノックバックが残っている間
    public bool IsAirControlWeakened =>
        !IsLocked &&
        groundChecker != null &&
        !groundChecker.IsGrounded &&
        knockbackVelocity.sqrMagnitude > airControlKnockbackThreshold * airControlKnockbackThreshold;

    public float AirControlMultiplier => airControlMultiplier;

    public TopDownPlayerMove PlayerMove;

    public AudioClip HitSound;
    AudioSource audioSource;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (groundChecker == null)
        {
            groundChecker = GetComponent<GroundChecker>();
        }

        if (PlayerMove == null)
        {
            PlayerMove = GetComponent<TopDownPlayerMove>();
        }

        // statsが設定されている場合のみ、Inspectorの初期値をSOの値で上書きする
        if (stats != null)
        {
            knockbackLockDuration = stats.knockbackLockDuration;
            airControlMultiplier = stats.airControlMultiplier;
            airControlKnockbackThreshold = stats.airControlKnockbackThreshold;
            knockbackDecaySpeed = stats.knockbackDecaySpeed;
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
    }

    /// <summary>
    /// 吹っ飛び中かつ空中のときだけ、追加の重力を与えて落下を速くする。
    /// 通常の重力（Rigidbodyの重力）はそのままで、倍率の超過分だけを加速度として足す。
    /// </summary>
    private void ApplyKnockbackGravity()
    {
        if (knockbackGravityMultiplier <= 1f) return;
        if (PlayerMove == null || !PlayerMove.IsKnockedBack) return;
        if (IsGrounded) return;

        rb.AddForce(Physics.gravity * (knockbackGravityMultiplier - 1f), ForceMode.Acceleration);
    }

    private void FixedUpdate()
    {
        // 吹っ飛び中（着地するまで）は重力を強めて落下を速くする
        ApplyKnockbackGravity();

        // ノックバック速度の時間減衰（XZ方向のみ。Y方向の落下は重力に任せる）
        if (knockbackVelocity.sqrMagnitude > 0.01f)
        {
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.fixedDeltaTime * knockbackDecaySpeed);
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

        if (!PlayerMove.IsAttack)
        {

            knockbackLockTimer = knockbackLockDuration;
        }
    }

    /// <summary>
    /// Y方向（上向き）の吹っ飛び初速を与える。落下は重力に任せるため、ここでは初速の代入のみ行う。
    /// </summary>
    public void ApplyUpwardBounce(float upForce, bool lockMovement = true)
    {
        PlayHitSound();

        Vector3 v = rb.linearVelocity;
        v.y = upForce;
        rb.linearVelocity = v;

        if (PlayerMove != null && !PlayerMove.IsAttack)
        {
            PlayerMove.StartKnockback(0.3f);
        }
    }

    /// <summary>
    /// 衝突音
    /// </summary>
    public void PlayHitSound()
    {
        audioSource.PlayOneShot(HitSound);
    }
}
