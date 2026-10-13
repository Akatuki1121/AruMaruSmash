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

    public static JoyconManager Instance => instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        j = new List<Joycon>();
        HIDapi.hid_init();
    }

    private void Start()
    {
        ScanAndAttach();
    }

    private void Update()
    {
        if (j.Count == 0 && Time.unscaledTime >= nextScanTime)
        {
            ScanAndAttach();
        }

        for (int i = 0; i < j.Count; i++)
        {
            j[i].Update();
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

                IntPtr handle = HIDapi.hid_open_path(device.path);
                if (handle == IntPtr.Zero)
                {
                    Debug.LogWarning($"Joy-Con HID device was found but could not be opened (product {device.product_id:X4}).");
                    continue;
                }

                HIDapi.hid_set_nonblocking(handle, 1);
                Joycon joycon = new Joycon(handle, EnableIMU, EnableLocalize && EnableIMU, 0.05f, isLeft);
                j.Add(joycon);

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
