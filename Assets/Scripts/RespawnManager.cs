using System;
using UnityEngine;

public class RespawnManager: MonoBehaviour
{
    public GameObject RespawnPoint;
    public GameObject[] Players;

    private void Start()
    {
        for (int i = 0;i < Players.Length; i++)
        {
            Respawn(i);
        }
    }
    void OnTriggerEnter(Collider collider)
    {
        DeathPlayer(collider);
    }

    // プレイヤーが場外に行ったら死亡させる
    private void DeathPlayer(Collider collider)
    {
        if (collider.gameObject.tag == "Player")
        {
            TopDownPlayerMove playerScript = collider.GetComponent<TopDownPlayerMove>();

            // indexを読み取っておく
            int id = playerScript.joyconIndex;
            Debug.Log($"プレイヤー {id} が死んだあ");
            Destroy(collider.gameObject);
            Respawn(id);
        }
    }

    // 死んだらリスポーン
    private void Respawn(int id)
    {
        if (id >= 0 && id < Players.Length && Players[id] != null)
        {
            // 指定されたインデックスのプレハブを、RespawnPointの位置・回転で生成
            Instantiate(Players[id], RespawnPoint.transform.position, RespawnPoint.transform.rotation);
            Debug.Log($"プレイヤー {id} をリスポーンしました");
        }
        else
        {
            Debug.LogError($"プレイヤーのインデックス {id} に対応するプレハブが登録されていません");
        }

    }
}
