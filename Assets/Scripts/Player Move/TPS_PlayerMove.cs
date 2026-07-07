using UnityEngine;

/// <summary>
/// TPS用：左右の傾きで旋回、前後の傾きで前後移動するスクリプト。
/// </summary>
public class TPS_PlayerMove : MonoBehaviour
{
    Joycon_accel_Receiver JoyAccelRec;
    public Vector3 JoyAccel;

    [Header("速度設定")]
    public float maxSpeed = 8f;         // 最大速度
    public float acceleration = 16f;    // 加速度
    public float currentSpeed = 0f;    // 現在の速度
    public float brakeSpeed = 40f;     // 減速（ブレーキ）速度
    public float rotationSpeed = 120f;  // 旋回速度

    private Vector3 moveDirection = Vector3.zero;
    private const float MOVEMENT_VELOCITY_EPSILON = 0.0001f;

    public Rigidbody rb;

    void Start()
    {
        if (JoyAccelRec == null) JoyAccelRec = GetComponent<Joycon_accel_Receiver>();
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        Vector3 oldDir = moveDirection;

        // 旋回と前後移動の入力を取得
        GetMoveDirection();

        // 加減速ロジック（前後移動の反転・ブレーキ用）
        if (currentSpeed > 0.1f)
        {
            if (Vector3.Dot(oldDir, moveDirection) < -0.1f)
            {
                currentSpeed -= brakeSpeed * Time.deltaTime;
                currentSpeed = Mathf.Max(currentSpeed, 0f);
            }
            else if (moveDirection.sqrMagnitude > 0)
            {
                currentSpeed += acceleration * Time.deltaTime;
                currentSpeed = Mathf.Min(currentSpeed, maxSpeed);
            }
        }
        else
        {
            if (moveDirection.sqrMagnitude > 0)
            {
                currentSpeed += acceleration * Time.deltaTime;
                currentSpeed = Mathf.Min(currentSpeed, maxSpeed);
            }
        }

        if (moveDirection.sqrMagnitude == 0)
        {
            currentSpeed -= brakeSpeed * Time.deltaTime;
            currentSpeed = Mathf.Max(currentSpeed, 0f);
        }
    }

    private void FixedUpdate()
    {
        PlayerMove(currentSpeed);
    }

    public void PlayerMove(float speed)
    {
        if (moveDirection.sqrMagnitude > 0)
        {
            // 自分の今の向き（旋回後）に対して前進・後退のベクトルを計算
            Vector3 localMoveDir = transform.rotation * moveDirection;
            Vector3 targetVelocity = localMoveDir * speed;

            // 坂道対応
            Ray ray = new Ray(rb.position + Vector3.up * 0.2f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, 0.6f))
            {
                targetVelocity = Vector3.ProjectOnPlane(targetVelocity, hit.normal);
            }

            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }
        else
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    public void GetMoveDirection()
    {
        float moveZ = 0;
        float rotationInput = 0f;

        // 前後の傾き（Y軸）で、前進（+1）または後退（-1）
        if (GetTiltY() > 0.2f) moveZ -= 1f;
        if (GetTiltY() < -0.2f) moveZ += 1f;

        // 左右の傾き（X軸）で、右旋回（+1）または左旋回（-1）
        if (GetTiltX() > 0.2f) rotationInput += 1f;
        if (GetTiltX() < -0.2f) rotationInput -= 1f;

        // その場でキャラクターの向きを回転させる
        transform.Rotate(0f, rotationInput * rotationSpeed * Time.deltaTime, 0f);

        // 移動方向は純粋に前後（Z軸）の入力のみにする（左右の横滑りはさせない）
        Vector3 inputVector = new Vector3(0f, 0f, moveZ);

        if (inputVector.sqrMagnitude > 0)
        {
            moveDirection = inputVector.normalized;
        }
        else
        {
            moveDirection = Vector3.zero;
        }
    }

    public float GetTiltX()
    {
        JoyAccel = JoyAccelRec.GetAccel();
        return JoyAccel.x;
    }

    public float GetTiltY()
    {
        JoyAccel = JoyAccelRec.GetAccel();
        return JoyAccel.y;
    }

    public Vector3 GetInputMoveVelocity()
    {
        return moveDirection * currentSpeed;
    }
}