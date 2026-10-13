using NUnit.Framework.Constraints;
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
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    // 決定ボタン
    public bool GetButtonDown()
    {
        // Joy-Con管理クラスが存在しなければ何もしない
        if (JoyconManager.Instance == null) return false;
        if (JoyconManager.Instance.j == null) return false;

        // 接続中のJoy-Conを順番に確認
        foreach (Joycon joycon in JoyconManager.Instance.j)
        {
            if (joycon == null) continue;

            // 横持ち時の右側に当たるボタンを取得
            if (joycon.isLeft)
            {
                if (joycon.GetButtonDown(Joycon.Button.DPAD_DOWN))
                    return true;
            }
            else
            {
                if (joycon.GetButtonDown(Joycon.Button.DPAD_UP))
                    return true;
            }
        }

        return false;
    }

    // スティック入力
    public UIInputDirection GetStick()
    {
        foreach (Joycon joycon in JoyconManager.Instance.j)
        {
            if (joycon == null) continue;

            float[] stick = joycon.GetStick();

            float x = stick[0];
            float y = stick[1];

            if (joycon.isLeft)
            {
                if (x > 0.5f)
                {
                    Debug.Log("上に入力");
                    return UIInputDirection.Up;
                }
                else if (x < -0.5f)
                {
                    Debug.Log("下に入力");
                    return UIInputDirection.Down;
                }

                if (y > 0.5f)
                {
                    Debug.Log("左に入力");
                    return UIInputDirection.Left;
                }
                else if (y < -0.5f)
                {
                    Debug.Log("右に入力");
                    return UIInputDirection.Right;
                }
            }
            else
            {
                if (x > 0.5f)
                {
                    Debug.Log("下に入力");
                    return UIInputDirection.Down;
                }
                else if (x < -0.5f)
                {
                    Debug.Log("上に入力");
                    return UIInputDirection.Up;
                }

                if (y > 0.5f)
                {
                    Debug.Log("右に入力");
                    return UIInputDirection.Right;
                }
                else if (y < -0.5f)
                {
                    Debug.Log("左に入力");
                    return UIInputDirection.Left;
                }
            }
        }

        return UIInputDirection.None;
    }
    /// <summary>    
    /// 縦の選択インデックス操作
    /// </summary>
    public int UIIndexVertical()
    {
        int index = 0;

        switch (GetStick())
        {
            case UIInputDirection.None:
                index = -0;
                break;
            case UIInputDirection.Up:
                index = -1;
                break;
            case UIInputDirection.Down:
                index = 1;
                break;
        }
        return index;
    }

    /// <summary>    
    /// 横の選択インデックス操作
    /// </summary>
    public int UIIndexHorizontal()
    {
        int index = 0;

        switch (GetStick())
        {
            case UIInputDirection.None:
                index = 0;
                break;
            case UIInputDirection.Right:
                index = 1;
                break;
            case UIInputDirection.Left:
                index = -1;
                break;
        }
        return index;
    }
}
