using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 見下ろし視点のプレイヤー移動を制御するクラス。
/// JoyconDemo.csから一部を抜粋しコピペしたもの。
/// </summary>

public class TopDown_PlayerMove : MonoBehaviour
{
    Joycon_accel_Receiver JoyAccelRec;
    public Vector3 JoyAccel;

    [Header("速度設定")]
    public float maxSpeed = 50f;       // 最大速度
    public float currentSpeed = 0f;    // 現在の速度
    public float brakeSpeed = 10f;     // 減速（ブレーキ）速度

    private Vector3 moveDirection = Vector3.zero;
    private const float MOVEMENT_VELOCITY_EPSILON = 0.0001f;

    public Rigidbody rb;

    // 現在の傾き量（0〜1）。これがそのまま速度倍率の元になる
    public float tiltAmount = 0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (JoyAccelRec == null)
        {
            JoyAccelRec = GetComponent<Joycon_accel_Receiver>();
        }
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 oldDir = moveDirection;

        GetMoveDirection();

        if (currentSpeed > 0.1f)
        {
            // 前回の移動方向と現在の移動方向が逆向きの場合、減速する
            if (Vector3.Dot(oldDir, moveDirection) < -0.1f)
            {
                currentSpeed -= brakeSpeed * Time.deltaTime;
                currentSpeed = Mathf.Max(currentSpeed, 0f);
            }
            else if (moveDirection.sqrMagnitude > 0)
            {
                currentSpeed += maxSpeed * tiltAmount * Time.deltaTime;
                currentSpeed = Mathf.Min(currentSpeed, maxSpeed);
            }
        }
        else
        {
            // 入力がある場合、加速する
            if (moveDirection.sqrMagnitude > 0)
            {
                currentSpeed += maxSpeed * tiltAmount * Time.deltaTime;
                currentSpeed = Mathf.Min(currentSpeed, maxSpeed);
            }
        }

        // 慣性の減衰処理（あなたのコードの仕様通り）
        if (moveDirection.sqrMagnitude == 0) // 入力がないときだけ減衰
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
            // 入力方向をワールド座標に変換
            Vector3 localMoveDir = transform.rotation * moveDirection;
            Vector3 targetVelocity = localMoveDir * speed;

            // 足元にレイを飛ばして地面の傾きを検知
            Ray ray = new Ray(rb.position + Vector3.up * 0.2f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, 0.6f))
            {
                // 地面の斜面に沿うように速度ベクトルを曲げる
                targetVelocity = Vector3.ProjectOnPlane(targetVelocity, hit.normal);
            }

            // XZ方向の速度を設定（Y方向の速度はそのままにする）
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }
        else
        {
            // 入力がない場合、XZ方向の速度をゼロにする（Y方向の速度はそのままにする）
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    public void GetMoveDirection()
    {
        float moveX = 0;
        float moveZ = 0;

        // 【提示されたコード通りの軸割り当て】
        // ジョイコンの前後傾き（Y）で、画面の上下（Z軸）を移動
        if (GetTiltY() > 0.2f) moveZ -= 1f;
        if (GetTiltY() < -0.2f) moveZ += 1f;

        // ジョイコンの左右傾き（X）で、画面の左右（X軸）を移動
        if (GetTiltX() > 0.2f) moveX += 1f;
        if (GetTiltX() < -0.2f) moveX -= 1f;

        Vector3 inputVector = new Vector3(moveX, 0f, moveZ);
        
        // 現在の傾き量を計算（0〜1）
        tiltAmount = Mathf.Clamp01(inputVector.magnitude);

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

    // 現在速度と入力方向を掛け合わせた移動ベクトルを返す
    public Vector3 GetInputMoveVelocity()
    {
        return moveDirection * currentSpeed;
    }
}
