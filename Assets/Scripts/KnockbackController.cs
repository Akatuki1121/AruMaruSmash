using UnityEngine;

/// <summary>
/// プレイヤーのノックバック状態（衝突による吹っ飛び）を管理するクラス。
///
/// 役割：
/// - 衝突等で受けたXZ方向の外力（knockbackVelocity）の保持と時間減衰
/// - 衝突直後の「入力完全ロック」期間の管理
/// - ロック終了後、空中にいる間だけ入力の効きを弱める「空中制御弱体化」の判定
/// - Y方向（上向き）の吹っ飛び初速の適用（rb.linearVelocity.yに直接反映、落下は重力に任せる）
///
/// MoveManager_Testはこのクラスが公開する状態（CurrentKnockbackVelocity, IsLocked,
/// IsAirControlWeakened, AirControlMultiplier）を読むだけで、
/// 自分でタイマーやノックバック量を管理する必要がない。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class KnockbackController : MonoBehaviour
{
    // ノックバック判定の許容値
    private const float KNOCKBACK_VELOCITY_THRESHOLD = 0.01f;

    // Y軸のリセット値
    private const float Y_VELOCITY_RESET = 0f;

    // タイマー判定
    private const float TIMER_ACTIVE_THRESHOLD = 0f;

    [Header("参照")]
    [Tooltip("接地判定。未設定の場合は同じオブジェクトから自動取得する")]
    public GroundChecker groundChecker;

    private Rigidbody rb;

    [Header("衝突後の制御不能時間")]
    [Tooltip("吹っ飛び・押し負け直後、プレイヤー入力での移動を完全に無効化する時間（秒）")]
    public float knockbackLockDuration = 0.5f;

    [Header("空中制御弱体化")]
    [Tooltip("ロック終了後、空中にいる間の入力の効き具合（0=入力無効、1=通常と同じ）")]
    [Range(0f, 1f)]
    public float airControlMultiplier = 0.1f;

    [Tooltip("この値以上のノックバック量が残っている間だけ「空中制御弱体化」とみなす")]
    public float airControlKnockbackThreshold = 0.9f;

    [Header("ノックバックの減衰")]
    [Tooltip("ノックバック速度が時間経過でどれだけ早く0に近づくか")]
    public float knockbackDecaySpeed = 4f;

    // 現在保持しているXZ方向のノックバック速度
    private Vector3 knockbackVelocity = Vector3.zero;
    private float knockbackLockTimer = 0f;

    public Vector3 CurrentKnockbackVelocity => knockbackVelocity;

    // 衝突直後、入力を完全に無視すべき期間中かどうか
    public bool IsLocked => knockbackLockTimer > TIMER_ACTIVE_THRESHOLD;

    // ロックは終わっているが、空中にいて、まだ知覚できる程度のノックバックが残っている間
    public bool IsAirControlWeakened =>
        !IsLocked &&
        groundChecker != null &&
        !groundChecker.IsGrounded &&
        knockbackVelocity.sqrMagnitude > airControlKnockbackThreshold * airControlKnockbackThreshold;

    public float AirControlMultiplier => airControlMultiplier;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (groundChecker == null)
        {
            groundChecker = GetComponent<GroundChecker>();
        }
    }

    private void Update()
    {
        if (knockbackLockTimer > TIMER_ACTIVE_THRESHOLD)
        {
            knockbackLockTimer -= Time.deltaTime;
        }
    }

    private void FixedUpdate()
    {
        // ノックバック速度の時間減衰（XZ方向のみ。Y方向の落下は重力に任せる）
        if (knockbackVelocity.sqrMagnitude > KNOCKBACK_VELOCITY_THRESHOLD)
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
        horizontalForce.y = Y_VELOCITY_RESET;
        knockbackVelocity += horizontalForce;
        knockbackLockTimer = knockbackLockDuration;
    }

    /// <summary>
    /// Y方向（上向き）の吹っ飛び初速を与える。落下は重力に任せるため、ここでは初速の代入のみ行う。
    /// </summary>
    public void ApplyUpwardBounce(float upForce)
    {
        Vector3 v = rb.linearVelocity;
        v.y = upForce;
        rb.linearVelocity = v;
    }
}
