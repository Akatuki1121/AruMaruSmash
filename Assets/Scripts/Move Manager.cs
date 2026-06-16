using Unity.VisualScripting.InputSystem;
using UnityEngine;
using UnityEngine.InputSystem;

public class MoveManager : InputManager
{
    // 移動処理
    public void PlayerMove(float speed)
    {
        float moveX = 0;
        float moveY = 0;
        float moveZ = 0;

        var pad = Gamepad.current;

        if (GetInputRight()) moveX += 1f;
        if (GetInputLeft()) moveX -= 1f;

        if (GetInputUp()) moveY += 1f;
        if (GetInputDown()) moveY -= 1f;

        Vector3 inputVector = new Vector3(moveX, moveY, moveZ);   // normalizedで斜め移動が早くなってしまうのを防ぐ

        if(inputVector.sqrMagnitude > 0)
        {
            rb.linearVelocity = Vector3.zero; // 慣性をリセット

            Vector3 moveDirection = inputVector.normalized;

            Vector3 nextPosition = rb.position +  * speed * Time.deltaTime;
            rb.MovePosition(nextPosition);
        }
        else
        {
            rb.linearVelocity = Vector3.zero; // 慣性をリセット
        }
    }
}
