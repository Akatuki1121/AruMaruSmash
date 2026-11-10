using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class JoyconManager : MonoBehaviour
{
    public bool EnableIMU = true;
    public bool EnableLocalize = true;

    private const ushort VendorId = 0x057e;
    private const ushort ProductLeft = 0x2006;
    private const ushort ProductRight = 0x2007;
    private const float RescanIntervalSeconds = 1.0f;

    public List<Joycon> j;
    private static JoyconManager instance;
    private float nextScanTime;
    private bool loggedNoJoycons;

    // Open device id (serial number, or path) per Joycon, to avoid opening the same device twice
    private readonly Dictionary<Joycon, string> deviceIds = new Dictionary<Joycon, string>();

    public static JoyconManager Instance => instance;

    // Joycon に対応する個体識別子（シリアル番号、無ければパス）を返す。未登録なら null
    public string GetDeviceId(Joycon joycon)
    {
        if (joycon == null)
        {
            return null;
        }
        string id;
        return deviceIds.TryGetValue(joycon, out id) ? id : null;
    }

    // 個体識別子で接続中の Joycon を探す。切断中のものは返さない
    public Joycon FindByDeviceId(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return null;
        }
        for (int i = 0; i < j.Count; i++)
        {
            if (j[i].state == Joycon.state_.DROPPED || j[i].state == Joycon.state_.NOT_ATTACHED)
            {
                continue;
            }
            string id;
            if (deviceIds.TryGetValue(j[i], out id) && id == deviceId)
            {
                return j[i];
            }
        }
        return null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // シーンを切り替えてもJoyconManagerを維持
        DontDestroyOnLoad(gameObject);

        j = new List<Joycon>();
        HIDapi.hid_init();
    }

    private void Start()
    {
        ScanAndAttach();
    }

    private void Update()
    {
        PruneDroppedJoycons();

        if (Time.unscaledTime >= nextScanTime)
        {
            ScanAndAttach();
        }

        for (int i = 0; i < j.Count; i++)
        {
            j[i].Update();
        }
    }

    // Remove dropped Joycons from the list; while they stay, the count never drops and no reconnect is accepted.
    // The polling thread has already ended when DROPPED, so Detach is not called here.
    private void PruneDroppedJoycons()
    {
        for (int i = j.Count - 1; i >= 0; i--)
        {
            if (j[i].state == Joycon.state_.DROPPED || j[i].state == Joycon.state_.NOT_ATTACHED)
            {
                deviceIds.Remove(j[i]);
                j.RemoveAt(i);
                Debug.Log("Joy-Con dropped and removed from the list; waiting for reconnect.");
            }
        }
    }

    private void ScanAndAttach()
    {
        nextScanTime = Time.unscaledTime + RescanIntervalSeconds;

        IntPtr devices = HIDapi.hid_enumerate(VendorId, 0);
        hid_device_info device;
        int openedCount = 0;

        try
        {
            IntPtr current = devices;
            while (current != IntPtr.Zero)
            {
                device = (hid_device_info)Marshal.PtrToStructure(current, typeof(hid_device_info));
                current = device.next;

                bool isLeft = device.product_id == ProductLeft;
                if (!isLeft && device.product_id != ProductRight)
                {
                    continue;
                }

                string deviceId = string.IsNullOrEmpty(device.serial_number) ? device.path : device.serial_number;
                if (deviceIds.ContainsValue(deviceId))
                {
                    continue;
                }

                IntPtr handle = HIDapi.hid_open_path(device.path);
                if (handle == IntPtr.Zero)
                {
                    Debug.LogWarning($"Joy-Con HID device was found but could not be opened (product {device.product_id:X4}).");
                    continue;
                }

                HIDapi.hid_set_nonblocking(handle, 1);
                Joycon joycon = new Joycon(handle, EnableIMU, EnableLocalize && EnableIMU, 0.05f, isLeft);
                j.Add(joycon);
                deviceIds[joycon] = deviceId;

                byte leds = (byte)(1 << (j.Count - 1));
                joycon.Attach(leds_: leds);
                joycon.Begin();
                openedCount++;
                Debug.Log($"{(isLeft ? "Left" : "Right")} Joy-Con connected through HID.");
            }
        }
        finally
        {
            if (devices != IntPtr.Zero)
            {
                HIDapi.hid_free_enumeration(devices);
            }
        }

        if (openedCount == 0 && !loggedNoJoycons)
        {
            Debug.Log("No Joy-Con HID devices found yet; checking again every second.");
            loggedNoJoycons = true;
        }
        else if (openedCount > 0)
        {
            loggedNoJoycons = false;
        }
    }

    private void OnApplicationQuit()
    {
        for (int i = 0; i < j.Count; i++)
        {
            j[i].Detach();
        }
        HIDapi.hid_exit();
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
