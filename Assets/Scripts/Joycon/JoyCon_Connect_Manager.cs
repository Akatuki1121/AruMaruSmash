using UnityEngine;

/// <summary>
/// Joy-Conの接続を管理するクラス。
/// SL+SRを押した順で登録する。
/// スイッチでたまにやるあれのイメージ。
/// </summary>

public class JoyCon_Connect_Manager : MonoBehaviour
{
    [Header("Joy-Con設定")]
    public int joyconIndex = 0; // 使用するJoy-Conのインデックス（0または1）
    private Joycon joycon; // 接続されているJoy-Con本体への参照

    void Start()
    {
        joyconIndex = 0; // 使用するJoy-Conのインデックスを設定（0または1）
    }

    void Update()
    {
        ConnectJoyCon();
    }

    public void ConnectJoyCon()
    {
        bool isDownSL = false;
        bool isDownSR = false;

        isDownSL = joycon.GetButtonDown(Joycon.Button.SL);
        isDownSR = joycon.GetButtonDown(Joycon.Button.SR);

        if(isDownSL && isDownSR)
        {
            Debug.Log("Joy-Con connected!");
            joycon = JoyconManager.Instance.j[joyconIndex];
            joyconIndex++; // 次のJoy-Conのインデックスに進める
        }
    }
}
