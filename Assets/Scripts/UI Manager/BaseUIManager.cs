
using UnityEngine;

public enum UIInputDirection
{
    None,
    Up,
    Down,
    Left,
    Right
}

public class BaseUIManager : MonoBehaviour
{
    // ========================================
    // 決定ボタン
    // ========================================

    // どのJoy-Conでも決定可能
    public bool GetConfirmButtonDown()
    {
        if (!HasJoycons()) return false;

        for (int i = 0; i < JoyconManager.Instance.j.Count; i++)
        {
            if (GetConfirmButtonDown(i))
                return true;
        }

        return false;
    }

    // 指定したJoy-Conだけ決定可能
    public bool GetConfirmButtonDown(int deviceId)
    {
        Joycon joycon = GetJoycon(deviceId);
        if (joycon == null) return false;

        if (joycon.isLeft)
        {
            return joycon.GetButtonDown(Joycon.Button.DPAD_DOWN);
        }
        else
        {
            return joycon.GetButtonDown(Joycon.Button.DPAD_UP);
        }
    }

    // ========================================
    // キャンセルボタン
    // ========================================

    // どのJoy-Conでもキャンセル可能
    public bool GetCancelButtonDown()
    {
        if (!HasJoycons()) return false;

        for (int i = 0; i < JoyconManager.Instance.j.Count; i++)
        {
            if (GetCancelButtonDown(i))
                return true;
        }

        return false;
    }

    // 指定したJoy-Conだけキャンセル可能
    public bool GetCancelButtonDown(int deviceId)
    {
        Joycon joycon = GetJoycon(deviceId);
        if (joycon == null) return false;

        if (joycon.isLeft)
        {
            return joycon.GetButtonDown(Joycon.Button.DPAD_LEFT);
        }
        else
        {
            return joycon.GetButtonDown(Joycon.Button.DPAD_RIGHT);
        }
    }

    // ========================================
    // スティック入力
    // ========================================

    // どのJoy-Conでも操作可能
    public UIInputDirection GetStick()
    {
        if (!HasJoycons()) return UIInputDirection.None;

        for (int i = 0; i < JoyconManager.Instance.j.Count; i++)
        {
            UIInputDirection direction = GetStick(i);

            if (direction != UIInputDirection.None)
                return direction;
        }

        return UIInputDirection.None;
    }

    // 指定したJoy-Conだけ操作可能
    public UIInputDirection GetStick(int deviceId)
    {
        Joycon joycon = GetJoycon(deviceId);

        if (joycon == null)
            return UIInputDirection.None;

        float[] stick = joycon.GetStick();

        if (stick == null || stick.Length < 2)
            return UIInputDirection.None;

        float x = stick[0];
        float y = stick[1];

        if (joycon.isLeft)
        {
            if (x > 0.5f)
                return UIInputDirection.Up;

            if (x < -0.5f)
                return UIInputDirection.Down;

            if (y > 0.5f)
                return UIInputDirection.Left;

            if (y < -0.5f)
                return UIInputDirection.Right;
        }
        else
        {
            if (x > 0.5f)
                return UIInputDirection.Down;

            if (x < -0.5f)
                return UIInputDirection.Up;

            if (y > 0.5f)
                return UIInputDirection.Right;

            if (y < -0.5f)
                return UIInputDirection.Left;
        }

        return UIInputDirection.None;
    }

    // ========================================
    // 縦方向の選択インデックス
    // ========================================

    // 誰でも操作可能
    public int UIIndexVertical()
    {
        return DirectionToVertical(GetStick());
    }

    // 指定したJoy-Conだけ操作可能
    public int UIIndexVertical(int deviceId)
    {
        return DirectionToVertical(GetStick(deviceId));
    }

    private int DirectionToVertical(UIInputDirection direction)
    {
        switch (direction)
        {
            case UIInputDirection.Up:
                return -1;

            case UIInputDirection.Down:
                return 1;

            default:
                return 0;
        }
    }

    // ========================================
    // 横方向の選択インデックス
    // ========================================

    // 誰でも操作可能
    public int UIIndexHorizontal()
    {
        return DirectionToHorizontal(GetStick());
    }

    // 指定したJoy-Conだけ操作可能
    public int UIIndexHorizontal(int deviceId)
    {
        return DirectionToHorizontal(GetStick(deviceId));
    }

    private int DirectionToHorizontal(UIInputDirection direction)
    {
        switch (direction)
        {
            case UIInputDirection.Left:
                return -1;

            case UIInputDirection.Right:
                return 1;

            default:
                return 0;
        }
    }

    // ========================================
    // 共通処理
    // ========================================

    // Joy-Con管理クラスが存在するか
    private bool HasJoycons()
    {
        return JoyconManager.Instance != null &&
               JoyconManager.Instance.j != null;
    }

    // 指定した番号のJoy-Conを取得
    private Joycon GetJoycon(int deviceId)
    {
        if (!HasJoycons())
            return null;

        var joycons = JoyconManager.Instance.j;

        if (deviceId < 0 || deviceId >= joycons.Count)
            return null;

        return joycons[deviceId];
    }
}
