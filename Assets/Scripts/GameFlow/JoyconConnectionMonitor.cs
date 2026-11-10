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

    // スロットごとの個体識別子（シリアル番号）。再接続時はこの個体が戻ったかで判定する
    private readonly string[] m_deviceIds = new string[PlayerRegistry.MAX_PLAYERS];

    /// <summary>
    /// プレイヤー番号に Joy-Con を紐づける。
    /// コントローラ割り当て画面で確定したタイミングで呼ぶ（JoyconManager.Instance.j[i] を渡す）
    /// </summary>
    public void Bind(int slot, Joycon joycon)
    {
        m_joycons[slot] = joycon;
        m_deviceIds[slot] = JoyconManager.Instance != null ? JoyconManager.Instance.GetDeviceId(joycon) : null;
    }

    public void Unbind(int slot)
    {
        m_joycons[slot] = null;
        m_deviceIds[slot] = null;
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
            CheckSlot(flow, i);
        }
    }

    private void CheckSlot(GameFlowManager flow, int slot)
    {
        PlayerSlot player = flow.Players.slots[slot];
        if (!player.isJoined)
        {
            return;
        }

        JoyconManager manager = JoyconManager.Instance;

        // Bind されていなければ、登録時の deviceId（リストのインデックス）から個体を特定して覚える
        if (string.IsNullOrEmpty(m_deviceIds[slot]) && manager != null && manager.j != null
            && player.deviceId >= 0 && player.deviceId < manager.j.Count && manager.j[player.deviceId] != null)
        {
            m_joycons[slot] = manager.j[player.deviceId];
            m_deviceIds[slot] = manager.GetDeviceId(m_joycons[slot]);
        }

        bool is_lost;
        if (!string.IsNullOrEmpty(m_deviceIds[slot]) && manager != null)
        {
            // 古いオブジェクトの state は切断後に戻らないため、同じ個体が今つながっているかで判定する
            Joycon current = manager.FindByDeviceId(m_deviceIds[slot]);
            is_lost = current == null;
            if (current != null)
            {
                m_joycons[slot] = current;
            }
        }
        else
        {
            Joycon joycon = m_joycons[slot];
            if (joycon == null)
            {
                return;
            }
            is_lost = joycon.state == Joycon.state_.DROPPED
                || joycon.state == Joycon.state_.NOT_ATTACHED
                || joycon.state == Joycon.state_.NO_JOYCONS;
        }

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
