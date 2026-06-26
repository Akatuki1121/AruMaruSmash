using UnityEngine;

/// <summary>
/// 入力に基づく基本移動のみを担当するクラス。
///
/// 衝突解決（弾性衝突の物理計算）はPlayerCollisionHandlerが、
/// ノックバック状態・空中制御弱体化の判定はKnockbackControllerが、
/// 接地判定はGroundCheckerがそれぞれ担当する。
/// MoveManager_Testはそれらが公開する状態を読み取り、移動意図に反映するだけ。
/// </summary>
public class MoveManager_Test : InputManager_Test
{
    // 移動方向の入力値
    private const float INPUT_POSITIVE_DIRECTION = 1f;
    private const float INPUT_NEGATIVE_DIRECTION = -1f;

    // 移動方向ベクトルのY成分（水平移動のため常に0)
    private const float MOVE_DIRECTION_Y_COMPONENT = 0f;

    // 慣性による減衰
    private const float INERTIA_DECELERATION_RATE = 1f;
    private const float MOVE_DIRECTION_STOP_THRESHOLD = 0.001f;

    // 移動速度計算の許容値
    private const float MOVEMENT_VELOCITY_EPSILON = 0.0001f;

    [Header("速度")]
    public float speed = 3f;

    [Header("方向転換（切り返し）の抵抗")]
    [Tooltip("同方向〜やや角度のある入力に対する追従の速さ（度/秒）。大きいほど素早く向きを変える")]
    public float turnSpeedSame = 720f;

    [Tooltip("完全に逆方向の入力に対する追従の速さ（度/秒）。小さいほど切り返しが遅く、レースゲームのステアリングのような抵抗感が出る")]
    public float turnSpeedReverse = 90f;

    [Tooltip("転換角度に応じたturnSpeedの補間の偏り。1で線形、大きいほど逆方向に近づいた時だけ急激に遅くなる")]
    public float turnSpeedCurve = 5f;

    [Tooltip("逆方向に近い転換中、speedに掛ける最小倍率（0=完全停止に近い、1=減速なし）")]
    [Range(0f, 1f)]
    public float reverseTurnSpeedMultiplierMin = 0.3f;

    [Header("参照")]
    [Tooltip("ノックバック状態の参照。未設定の場合は同じオブジェクトから自動取得する")]
    public KnockbackController knockbackController;

    private Vector3 moveDirection = Vector3.zero;

    protected override void Awake()
    {
        base.Awake(); // InputManager_Test.Awake()でrbを初期化する

        if (knockbackController == null)
        {
            knockbackController = GetComponent<KnockbackController>();
        }
    }

    void Update()
    {
        GetMoveDirection();
        if (!GetInputRight() && !GetInputLeft() && !GetInputUp() && !GetInputDown())
        {
            // 慣性で移動する場合の減速処理
            moveDirection -= INERTIA_DECELERATION_RATE * Time.deltaTime * moveDirection;

            // 移動方向がほぼゼロになったら完全に停止させる
            if (moveDirection.sqrMagnitude < MOVE_DIRECTION_STOP_THRESHOLD)
            {
                moveDirection = Vector3.zero;
            }
        }
    }

    private void FixedUpdate()
    {
        PlayerMove(speed);
    }

    public void GetMoveDirection()
    {
        float moveX = 0;
        float moveY = 0;
        float moveZ = 0;

        if (GetInputRight()) moveX += INPUT_POSITIVE_DIRECTION;
        if (GetInputLeft()) moveX += INPUT_NEGATIVE_DIRECTION;

        if (GetInputUp()) moveZ += INPUT_POSITIVE_DIRECTION;
        if (GetInputDown()) moveZ += INPUT_NEGATIVE_DIRECTION;

        Vector3 inputVector = new Vector3(moveX, moveY, moveZ);   // normalizedで斜め移動が早くなってしまうのを防ぐ

        if (inputVector.sqrMagnitude > 0)
        {
            moveDirection = inputVector.normalized;
        }
        else
        {
            //moveDirection = Vector3.zero; // 慣性をリセット
        }
    }

    // 移動処理。3段階（ロック中／空中制御弱体化中／通常）で入力の効きを切り替える
    public void PlayerMove(float speed)
    {
        Vector3 knockbackVelocity = Vector3.zero;
        Vector3 horizontalIntent;

        if (knockbackController != null && knockbackController.IsLocked)
        {
            // ① ロック中：入力完全無視
            horizontalIntent = Vector3.zero;
            knockbackVelocity = knockbackController.CurrentKnockbackVelocity;
        }
        else if (knockbackController != null && knockbackController.IsAirControlWeakened)
        {
            // ② 空中制御弱体化中：入力は効くが弱める
            horizontalIntent = knockbackController.AirControlMultiplier * speed * moveDirection;
            knockbackVelocity = knockbackController.CurrentKnockbackVelocity;
        }
        else
        {
            // ③ 通常：フル入力
            horizontalIntent = moveDirection * speed;
            if (knockbackController != null)
            {
                knockbackVelocity = knockbackController.CurrentKnockbackVelocity;
            }
        }

        Vector3 horizontalVelocity = horizontalIntent + knockbackVelocity;

        if (horizontalVelocity.sqrMagnitude > MOVEMENT_VELOCITY_EPSILON)
        {
            // XZだけをMovePositionで進め、Y（重力で落下中の高さ）はrb.positionの現在値をそのまま使う
            Vector3 currentPos = rb.position;
            Vector3 delta = horizontalVelocity * Time.deltaTime;
            Vector3 nextPosition = new(currentPos.x + delta.x, currentPos.y, currentPos.z + delta.z);
            rb.MovePosition(nextPosition);
        }
    }

    // 現在のXZ方向の入力由来の移動ベクトル（慣性込み、ノックバックは含まない）を返す。
    // PlayerCollisionHandlerが弾性衝突の計算をする際に使用する。
    public Vector3 GetInputMoveVelocity()
    {
        return moveDirection * speed;
    }
}