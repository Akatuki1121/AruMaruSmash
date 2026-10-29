using System;
using UnityEngine;

public class RespawnManager: MonoBehaviour
{
    [Tooltip("個別の地点が未設定のプレイヤー用の共通リスポーン地点")]
    public GameObject RespawnPoint;
    [Tooltip("プレイヤーごとのリスポーン地点(Players と同じ並び)")]
    public GameObject[] RespawnPoints;
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
            Transform point = GetRespawnPoint(id);
            if (point == null)
            {
                Debug.LogError($"プレイヤー {id} のリスポーン地点が設定されていません");
                return;
            }

            // 指定されたインデックスのプレハブを、そのプレイヤーの地点の位置・回転で生成
            Instantiate(Players[id], point.position, point.rotation);
            Debug.Log($"プレイヤー {id} をリスポーンしました");
        }
        else
        {
            Debug.LogError($"プレイヤーのインデックス {id} に対応するプレハブが登録されていません");
        }

    }

    // プレイヤーごとの地点を返す。未設定なら共通の RespawnPoint を使う
    private Transform GetRespawnPoint(int id)
    {
        if (RespawnPoints != null && id >= 0 && id < RespawnPoints.Length && RespawnPoints[id] != null)
        {
            return RespawnPoints[id].transform;
        }
        return RespawnPoint != null ? RespawnPoint.transform : null;
    }
}
