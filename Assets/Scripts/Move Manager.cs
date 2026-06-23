using UnityEngine;

public class MoveManager : InputManager
{
    [Header("速度")]
    public float speed = 5f;

    private Vector3 moveDirection = Vector3.zero;

    void Update()
    {
        GetMoveDirection();
        if (!GetInputRight() && !GetInputLeft() && !GetInputUp() && !GetInputDown())
        {
            // 慣性で移動する場合の減速処理
            moveDirection -= moveDirection * Time.deltaTime * 1f;

            // 移動方向がほぼゼロになったら完全に停止させる
            if (moveDirection.sqrMagnitude < 0.001f)
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

        if (GetInputRight()) moveX += 1f;
        if (GetInputLeft()) moveX -= 1f;

        if (GetInputUp()) moveZ += 1f;
        if (GetInputDown()) moveZ -= 1f;

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

    // 移動処理
    public void PlayerMove(float speed)
    {
        if (moveDirection.sqrMagnitude > 0)
        {
            //rb.linearVelocity = Vector3.zero; // 慣性をリセット

            Vector3 nextPosition = rb.position + (moveDirection * speed * Time.deltaTime);
            rb.MovePosition(nextPosition);
        }
        else
        {
           // rb.linearVelocity = Vector3.zero; // 慣性をリセット
        }
    }
}
