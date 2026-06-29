using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Joy-Conの加速度センサーの値を取得して、傾き量を検知するスクリプト。
/// JoyconDemo.csから一部を抜粋しコピペしたもの。
///ジョイコンの前後の傾きで移動、左右の傾きで旋回刺せているバージョン
/// <summary>

public class Player_Move1 : MonoBehaviour
{
    Joycon_accel_Receiver JoyAccelRec;
    public Vector3 JoyAccel;

    

    [Header("速度")]
    float maxSpeed = 100f;  // 最大速度
    float acceleration = 5f;    // 加速度
    public float currentSpeed = 0f; // 現在の速度
    float brakeSpeed = 20f; // 減速速度
    float rotationSpeed = 50f;  // 旋回速度
    private Vector3 moveDirection = Vector3.zero;

   
    private Vector3 currentMoveDirection;
    private Vector3 lastMoveDirection;

    [HideInInspector] public Rigidbody rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(JoyAccelRec == null)
        {
            JoyAccelRec = GetComponent<Joycon_accel_Receiver>();
        }

        if(rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        currentMoveDirection = Vector3.zero;
        lastMoveDirection = Vector3.zero;
    }

    // Update is called once per frame
    void Update()
    {
        // 前回の移動方向を保存
        Vector3 oldDir = moveDirection;

        GetMoveDirection();

        if (currentSpeed > 0.1f)
        {
            if (Vector3.Dot(oldDir, moveDirection) < -0.1f)
            {
                moveDirection = oldDir;

                // 方向が逆になった場合、減速
                currentSpeed -= brakeSpeed * Time.deltaTime;
                currentSpeed = Mathf.Max(currentSpeed, 0f);
            }
            // 加速度に応じて速度を増加させる
            else if (moveDirection.sqrMagnitude > 0)
            {
                currentSpeed += acceleration * Time.deltaTime;
                currentSpeed = Mathf.Min(currentSpeed, maxSpeed);
            }
        }
        else
        {
            // 移動方向がある場合、加速
            if (moveDirection.sqrMagnitude > 0)
            {
                currentSpeed += acceleration * Time.deltaTime;
                currentSpeed = Mathf.Min(currentSpeed, maxSpeed);
            }
        }

        // 慣性で移動する場合の減速処理
        moveDirection -= moveDirection * Time.deltaTime * 1f;

        // 移動方向がほぼゼロになったら完全に停止させる
        if (moveDirection.sqrMagnitude < 0.001f)
        {
            moveDirection = Vector3.zero;
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
            //rb.linearVelocity = Vector3.zero; // 慣性をリセット

            Vector3 localMoveDir = transform.rotation * moveDirection;

            Vector3 nextPosition = rb.position + (speed * Time.deltaTime * localMoveDir);
            rb.MovePosition(nextPosition);
        }
    }

    public void GetMoveDirection()
    {
        float moveX = 0;
        float moveY = 0;
        float moveZ = 0;

        float rotation = 0f;

        if (GetTiltY() > 0.2f) moveZ -= 1f;
        if (GetTiltY() < -0.2f) moveZ += 1f;

        if (GetTiltX() > 0.2f) rotation += 1f;
        if (GetTiltX() < -0.2f) rotation -= 1f;
        Vector3 inputVector = new Vector3(moveX, moveY, moveZ);   // normalizedで斜め移動が早くなってしまうのを防ぐ

        transform.Rotate(0f, rotation * rotationSpeed * Time.deltaTime, 0f);

        if (inputVector.sqrMagnitude > 0)
        {
            moveDirection = inputVector.normalized;
        }
        else
        {
            //moveDirection = Vector3.zero; // 慣性をリセット
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
}
