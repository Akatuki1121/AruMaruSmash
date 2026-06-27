using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Joy-Conの加速度センサーの値を取得して、傾き量を検知するスクリプト。
/// JoyconDemo.csから一部を抜粋しコピペしたもの。
/// <summary>

public class Player_Move : MonoBehaviour
{
    Joycon_accel_Receiver JoyAccelRec;
    public Vector3 JoyAccel;

    [HideInInspector] public Rigidbody rb;

    [Header("速度")]
    float speed = 3f;
    private Vector3 moveDirection = Vector3.zero;

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
    }

    // Update is called once per frame
    void Update()
    {
        GetMoveDirection();

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
        PlayerMove(speed);
    }

    public void PlayerMove(float speed)
    {
        if (moveDirection.sqrMagnitude > 0)
        {
            //rb.linearVelocity = Vector3.zero; // 慣性をリセット

            Vector3 nextPosition = rb.position + (speed * Time.deltaTime * moveDirection);
            rb.MovePosition(nextPosition);
        }
    }

    public void GetMoveDirection()
    {
        float moveX = 0;
        float moveY = 0;
        float moveZ = 0;

        if (GetTiltX() > 0.2f) moveX += 1f;
        if (GetTiltX() < -0.2f) moveX -= 1f;

        if (GetTiltY() > 0.2f) moveZ -= 1f;
        if (GetTiltY() < -0.2f) moveZ += 1f;
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
