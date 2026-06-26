using UnityEngine;

/// <summary>
/// プレイヤー同士の衝突を検知し、同質量の弾性衝突として速度を解決するクラス。
///
/// 役割：
/// - OnCollisionEnterでの衝突検知（相手にもPlayerCollisionHandlerがある場合のみ反応）
/// - 衝突法線・接線方向に速度を分解し、法線成分を交換する物理的な弾性衝突の計算
/// - speedの大小関係に応じて、強い方は法線速度の変化を抑える非対称調整（asymmetryFactor）
/// - 衝突の勢いに応じた上方向（Y軸）の吹っ飛び初速の計算
/// - 同じ相手との短時間での再衝突を無視する「ペアごとの無敵時間」管理
///   （壁・ギミック等、PlayerCollisionHandlerを持たない相手との衝突はこの対象外で、常に反応する）
/// </summary>
[RequireComponent(typeof(MoveManager_Test))]
[RequireComponent(typeof(KnockbackController))]
public class PlayerCollisionHandler : MonoBehaviour
{
    [Header("参照")]
    private MoveManager_Test moveManager;
    private KnockbackController knockbackController;

    [Header("衝突反応（同質量の弾性衝突ベース）")]
    [Tooltip("0=完全に物理的な弾性衝突（法線成分を均等にSwap）/ 1=強い方は法線速度をほぼ変えず弱い方だけが強く飛ぶ（ゲーム的な非対称調整）")]
    [Range(0f, 1f)]
    public float asymmetryFactor = 0.85f;

    [Tooltip("反発の勢い全体に対する倍率")]
    public float bounceForceMultiplier = 1f;

    [Header("吹っ飛び（上方向）")]
    [Tooltip("衝突の相対速度1あたりに加える上方向初速")]
    public float upwardForcePerSpeed = 0.6f;

    [Tooltip("上方向初速の最小値（衝突さえすれば必ずこれくらいは浮く）")]
    public float minUpwardForce = 1.5f;

    [Tooltip("上方向初速の最大値（暴れすぎ防止のクランプ）")]
    public float maxUpwardForce = 8f;

    [Header("同じ相手との再衝突防止")]
    [Tooltip("同じ相手と衝突した後、この時間内は同じ相手との衝突を無視する（秒）。" +
             "壁やギミックなど、PlayerCollisionHandlerを持たない相手との衝突には影響しない。")]
    public float sameTargetCooldown = 0.4f;

    // 直前にぶつかった相手とその時刻（ペアごとの無敵時間判定に使用）
    private PlayerCollisionHandler lastHitOther = null;
    private float lastHitTime = -999f;

    // 着地検知用：前フレームの接地状態を記憶しておき、非接地→接地に変わった瞬間を捉える
    private bool wasGroundedLastFrame = true;

    private void Awake()
    {
        moveManager = GetComponent<MoveManager_Test>();
        knockbackController = GetComponent<KnockbackController>();
        wasGroundedLastFrame = knockbackController.IsGrounded;
    }

    private void Update()
    {
        // 着地検知：前フレームは空中で、今フレームで接地した瞬間に同じ相手との無敵情報をリセットする
        bool isGroundedNow = knockbackController.IsGrounded;
        if (isGroundedNow && !wasGroundedLastFrame)
        {
            lastHitOther = null;
        }
        wasGroundedLastFrame = isGroundedNow;
    }

    private void OnCollisionEnter(Collision collision)
    {
        PlayerCollisionHandler other = collision.collider.GetComponent<PlayerCollisionHandler>();
        if (other == null) return; // 壁・ギミック等、プレイヤーでない相手は無条件で無視（このクラスの対象外）

        // 同じ相手と直前にぶつかっていて、まだ無敵時間内なら無視する
        if (lastHitOther == other && Time.time - lastHitTime < sameTargetCooldown) return;

        // 片方の処理だけで両者分を解決する（自分のEntityIdが小さい方が代表して処理）
        if (GetEntityId() > other.GetEntityId()) return;

        Vector3 contactNormal = collision.GetContact(0).normal; // otherからthisへ向かう方向

        ResolvePlayerCollision(this, other, contactNormal);

        // 両者に「直前にぶつかった相手」を記録する
        lastHitOther = other;
        lastHitTime = Time.time;
        other.lastHitOther = this;
        other.lastHitTime = Time.time;
    }

    private static void ResolvePlayerCollision(PlayerCollisionHandler a, PlayerCollisionHandler b, Vector3 normalBtoA)
    {
        // 衝突法線（水平面のみで計算。立体的な乗り上げ等は無視する）
        Vector3 normal = -normalBtoA.normalized; // aからbへ向かう方向
        normal.y = 0f;
        if (normal.sqrMagnitude < 0.0001f) normal = Vector3.forward;
        normal.Normalize();

        // 各プレイヤーの現在のXZ速度（入力由来の移動 + 既存のノックバック）を取得
        Vector3 velA = a.moveManager.GetInputMoveVelocity() + a.knockbackController.CurrentKnockbackVelocity;
        Vector3 velB = b.moveManager.GetInputMoveVelocity() + b.knockbackController.CurrentKnockbackVelocity;

        // 各速度を「法線方向の成分」と「接線方向の成分」に分解する
        float velA_n = Vector3.Dot(velA, normal);
        float velB_n = Vector3.Dot(velB, normal);
        Vector3 velA_t = velA - normal * velA_n;
        Vector3 velB_t = velB - normal * velB_n;

        // 同質量の1次元弾性衝突：法線方向の速度成分を完全に交換するのが物理的に正しい
        float newVelA_n = velB_n;
        float newVelB_n = velA_n;

        // ゲーム的な非対称調整：実際に今動いている速さが大きい方は法線速度の変化を抑え、
        // 弱い方（遅い方・止まっている方）だけが強く飛ばされるようにブレンドする
        // ※設定上のspeed（moveManager.speed）ではなく、実際の現在の速度の大きさを使う。
        //   設定値を使うと「止まっている相手」と「全力で動いている相手」が同じspeed設定の場合に
        //   誤って「互角」と判定されてしまうため。
        float speedA = velA.magnitude;
        float speedB = velB.magnitude;

        // 両者ともほぼ停止している場合は「互角」として扱う（0除算回避と誤判定防止を兼ねる）
        float weightAIsStronger;
        if (speedA + speedB < 0.0001f)
        {
            weightAIsStronger = 0.5f;
        }
        else
        {
            weightAIsStronger = speedA / (speedA + speedB); // 0.5なら互角、1に近いほどAが圧倒的に強い
        }

        // strongerSideほどasymmetryFactorの影響を強く受け、自分の法線速度を変えない（=元の値に近づける）
        float blendA = a.asymmetryFactor * Mathf.Clamp01((weightAIsStronger - 0.5f) * 2f); // Aが強い時に効く
        float blendB = a.asymmetryFactor * Mathf.Clamp01((0.5f - weightAIsStronger) * 2f); // Bが強い時に効く

        newVelA_n = Mathf.Lerp(newVelA_n, velA_n, blendA);
        newVelB_n = Mathf.Lerp(newVelB_n, velB_n, blendB);

        Vector3 resultVelA = velA_t + normal * newVelA_n;
        Vector3 resultVelB = velB_t + normal * newVelB_n;

        a.knockbackController.ApplyKnockback((resultVelA - velA) * a.bounceForceMultiplier);
        b.knockbackController.ApplyKnockback((resultVelB - velB) * a.bounceForceMultiplier);

        // 吹っ飛び（上方向）：衝突の勢い＝法線方向の相対速度の大きさに応じて決める
        float closingSpeed = Mathf.Abs(velA_n - velB_n);
        float upForce = Mathf.Clamp(closingSpeed * a.upwardForcePerSpeed, a.minUpwardForce, a.maxUpwardForce);

        // 強く飛ばされた側ほど高く浮くように、変化量の比率で上方向初速にも差をつける
        float changeA = Mathf.Abs(newVelA_n - velA_n);
        float changeB = Mathf.Abs(newVelB_n - velB_n);
        float changeTotal = Mathf.Max(changeA + changeB, 0.0001f);

        float upForceA = Mathf.Clamp(upForce * (changeA / changeTotal) * 2f, a.minUpwardForce * 0.3f, a.maxUpwardForce);
        float upForceB = Mathf.Clamp(upForce * (changeB / changeTotal) * 2f, a.minUpwardForce * 0.3f, a.maxUpwardForce);

        a.knockbackController.ApplyUpwardBounce(upForceA);
        b.knockbackController.ApplyUpwardBounce(upForceB);
    }
}
