using UnityEngine;

/// <summary>
/// Joy-Conとプレイヤーを紐づけるクラス。
/// プレイヤーはカーソルを操作し、エリア内にカーソルをいれて、全員が選んだら決定ボタンを押して確定させる
/// スマブラのキャラ選択画面を参考にしている
/// </summary>

public class PlayerSelector : MonoBehaviour
{
    Joycon_accel_Receiver JoyAccelRec;
    public Vector3 JoyAccel;

    private Joycon joycon; // 接続されているJoy-Con本体への参照

    [Header("移動速度")]
    public float speed = 5f; // 移動速度

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void GetCursorMove(float speed)
    {
        float moveX = 0;
        float moveY = 0;

        if(GetTiltX() > 0.2f) moveX = speed;
        if(GetTiltX() < -0.2f) moveX = -speed;

        if (GetTiltY() > 0.2f) moveY = -speed;
        if (GetTiltY() < -0.2f) moveY = speed;
        Vector3 inputVector = new Vector3(moveX, moveY, 0);   // normalizedで斜め移動が早くなってしまうのを防ぐ
    }

    public float GetTiltX()
    {
        JoyAccel = joycon.GetAccel();

        return JoyAccel.x;
    }

    public float GetTiltY()
    {
        JoyAccel = joycon.GetAccel();

        return JoyAccel.y;
    }
}
