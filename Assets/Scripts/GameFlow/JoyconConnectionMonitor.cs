using UnityEngine;

/// <summary>
/// Joy-Con の切断・復帰を監視して PlayerRegistry に伝える。
/// JoyconLib は HID を直接読むため Input System の onDeviceChange では拾えない。
/// Joycon.state を毎フレーム見て判定する
/// </summary>
public class JoyconConnectionMonitor : MonoBehaviour
{
    // スロットごとの Joy-Con（未割り当ては null）
    private readonly Joycon[] m_joycons = new Joycon[PlayerRegistry.MAX_PLAYERS];

    /// <summary>
    /// プレイヤー番号に Joy-Con を紐づける。
    /// コントローラ割り当て画面で確定したタイミングで呼ぶ（JoyconManager.Instance.j[i] を渡す）
    /// </summary>
    public void Bind(int slot, Joycon joycon)
    {
        m_joycons[slot] = joycon;
    }

    public void Unbind(int slot)
    {
        m_joycons[slot] = null;
    }

    private void Update()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow == null)
        {
            return;
        }

        for (int i = 0; i < m_joycons.Length; i++)
        {
            if (m_joycons[i] == null)
            {
                continue;
            }
            CheckSlot(flow, i, m_joycons[i]);
        }
    }

    private static void CheckSlot(GameFlowManager flow, int slot, Joycon joycon)
    {
        PlayerSlot player = flow.Players.slots[slot];
        if (!player.isJoined)
        {
            return;
        }

        bool is_lost = joycon.state == Joycon.state_.DROPPED
            || joycon.state == Joycon.state_.NOT_ATTACHED
            || joycon.state == Joycon.state_.NO_JOYCONS;

        if (is_lost && player.connection == ConnectionState.Connected)
        {
            ControllerDisconnectHandler.Handle(flow, slot);
        }
        else if (!is_lost && player.connection == ConnectionState.Disconnected)
        {
            flow.Players.Reconnect(slot, player.deviceId);
        }
    }
}
