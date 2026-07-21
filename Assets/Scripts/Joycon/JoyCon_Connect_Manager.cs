using UnityEngine;

/// <summary>
/// Joy-Conの接続を管理するクラス。
/// SL+SRを押した順で登録する。
/// 誰かの右ボタンで決定する。
/// スイッチでたまにやるあれのイメージ。
/// </summary>

public class JoyCon_Connect_Manager : MonoBehaviour
{
    [Header("Joy-Con設定")]
    public int joyconIndex = 0; // 使用するJoy-Conのインデックス（0または1）
    const int indexMax = 4;     // Joy-Conの最大接続数
    private Joycon joycon; // 接続されているJoy-Con本体への参照

    void Start()
    {
        joyconIndex = 0; // 使用するJoy-Conのインデックスを設定（0または1）
    }

    void Update()
    {
        ConnectJoyCon();
        decisionConnect();
    }

    public void ConnectJoyCon()
    {
        bool isHeldSL = false;
        bool isHeldSR = false;


        isHeldSL = joycon.GetButton(Joycon.Button.SL);
        isHeldSR = joycon.GetButton(Joycon.Button.SR);

        if(isHeldSL && isHeldSR)
        {
            Debug.Log("Joy-Con connected!");
            joycon = JoyconManager.Instance.j[joyconIndex];
            if(joyconIndex < indexMax)
            {
                joyconIndex++; // 次のJoy-Conのインデックスに進める
            }
        }
    }

    public void decisionConnect()
    {

    }
}
