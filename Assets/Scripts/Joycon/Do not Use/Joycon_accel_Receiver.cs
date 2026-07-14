using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Joy-Conの加速度センサーの値を取得して、傾き量を検知するスクリプト。
/// 
/// Player_Move.csにaccelの値を渡す。
/// 
/// JoyconDemo.csから一部を抜粋しコピペしたもの。
/// <summary>

public class Joycon_accel_Receiver : MonoBehaviour
{
    private List<Joycon> joycons;

    public Vector3 accel;   // 傾き検知用
    public int jc_ind = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        accel = new Vector3(0, 0, 0);

        joycons = JoyconManager.Instance.j;
        if (joycons.Count < jc_ind + 1)
        {
            Destroy(gameObject);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (joycons.Count > 0)
        {
            Joycon j = joycons[jc_ind];
            accel = j.GetAccel();
        }
    }

    public Vector3 GetAccel()
    {
        return accel;
    }
}
