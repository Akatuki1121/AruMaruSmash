using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// コントローラの切断・再接続を検知して PlayerRegistry に伝える。
/// 注意: JoyconManager（HID直読み）の切断は Input System を通らないため、この監視では拾えない
/// </summary>
public class ControllerConnectionMonitor : MonoBehaviour
{
    private const int SLOT_NOT_FOUND = -1;

    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow == null)
        {
            return;
        }

        int slot = FindSlotByDevice(flow.Players, device.deviceId);
        if (slot == SLOT_NOT_FOUND)
        {
            return;
        }

        switch (change)
        {
            case InputDeviceChange.Disconnected:
            case InputDeviceChange.Removed:
                ControllerDisconnectHandler.Handle(flow, slot);
                break;
            case InputDeviceChange.Reconnected:
                flow.Players.Reconnect(slot, device.deviceId);
                break;
        }
    }

    private static int FindSlotByDevice(PlayerRegistry registry, int device_id)
    {
        foreach (PlayerSlot s in registry.slots)
        {
            if (s.isJoined && s.deviceId == device_id)
            {
                return s.index;
            }
        }
        return SLOT_NOT_FOUND;
    }
}
