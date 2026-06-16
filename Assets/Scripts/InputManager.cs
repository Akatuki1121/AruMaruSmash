using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    // stickの遊び
    [HideInInspector]public float deadzone = 0.3f;

    [HideInInspector]public Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

    }

    //----- 継続入力 -----//

    // 上入力
    public bool GetInputUp()
    {
        bool up = false;

        // キーボード
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
        {
            up = true;
        }

        // ゲームパッド
        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftStick.y.ReadValue() > deadzone) up = true;
        }

        return up;
    }

    // 下入力
    public bool GetInputDown()
    {
        bool down = false;

        // キーボード
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
        {
            down = true;
        }

        // ゲームパッド
        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftStick.y.ReadValue() < -deadzone) down = true;
        }

        return down;
    }

    // 右入力
    public bool GetInputRight()
    {
        bool right = false;

        // キーボード
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
        {
            right = true;
        }

        // ゲームパッド
        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftStick.x.ReadValue() > deadzone) right = true;
        }

        return right;
    }

    // 左入力
    public bool GetInputLeft()
    {
        bool left = false;

        // キーボード
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
        {
            left = true;
        }

        // ゲームパッド
        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftStick.x.ReadValue() < -deadzone) left = true;
        }

        return left;
    }

    //----- 単発入力 -----//

    // 上入力
    public bool GetInputUpNow()
    {
        bool up = false;

        // キーボード
        if (Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            up = true;
        }

        // ゲームパッド
        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftStick.y.ReadValue() > deadzone) up = true;
        }

        return up;
    }

    // 下入力
    public bool GetInputDownNow()
    {
        bool down = false;

        // キーボード
        if (Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            down = true;
        }

        // ゲームパッド
        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftStick.y.ReadValue() < -deadzone) down = true;
        }

        return down;
    }

    // 右入力
    public bool GetInputRightNow()
    {
        bool right = false;

        // キーボード
        if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            right = true;
        }

        // ゲームパッド
        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftStick.x.ReadValue() > deadzone) right = true;
        }

        return right;
    }

    // 左入力
    public bool GetInputLeftNow()
    {
        bool left = false;

        // キーボード
        if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            left = true;
        }

        // ゲームパッド
        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftStick.x.ReadValue() < -deadzone) left = true;
        }

        return left;
    }

    // 決定
    public bool GetDetermination()
    {
        bool determination = false;

        // キーボード
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            determination = true;
        }

        if(Gamepad.current != null)
        {
            // 後程実装
        }

        return determination;
    }
}
