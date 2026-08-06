using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Joy-Con�̉����x�Z���T�[�̒l���擾���āA�X���ʂ����m����X�N���v�g�B
/// 
/// Player_Move.cs��accel�̒l��n���B
/// 
/// JoyconDemo.cs����ꕔ�𔲐����R�s�y�������́B
/// <summary>

public class JoyconAccelReceiver : MonoBehaviour
{
    private List<Joycon> joycons;

    public Vector3 accel;   // �X�����m�p
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
