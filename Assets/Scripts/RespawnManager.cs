using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnManager: MonoBehaviour
{
    [Tooltip("個別の地点が未設定のプレイヤー用の共通リスポーン地点。落下後の復活では、この地点の真下にあるステージの床の上（ステージ中央）に出現する")]
    public GameObject RespawnPoint;
    [Tooltip("プレイヤーごとのリスポーン地点(P1〜P4、Players と同じ並び)。試合開始時と落下後の復活に使う")]
    public GameObject[] RespawnPoints;
    public GameObject[] Players;

    [Header("復活の演出")]
    [Tooltip("落下してから復活するまでの時間（秒）")]
    public float respawnDelay = 0.5f;
    [Tooltip("ステージの床の下からせり上がる出現演出の長さ（秒）。この間も無敵")]
    public float emergeDuration = 0.5f;
    [Tooltip("出現演出が終わったあとに続く無敵時間（秒）")]
    public float invincibleAfterEmerge = 1.0f;
    [Tooltip("無敵中の点滅の切り替え間隔（秒）")]
    public float blinkInterval = 0.1f;

    // 復活地点の床を探すレイの設定
    private const string GROUND_TAG = "Ground";
    private const float GROUND_RAY_HEIGHT = 50f;       // 復活地点より上から撃つ
    private const float GROUND_RAY_DISTANCE = 200f;    // 復活地点より下へ探す距離
    private readonly RaycastHit[] groundHitBuffer = new RaycastHit[64];
    // 出現の開始位置を、球体の上端が床の面より少し下になるようにする余白
    private const float EMERGE_START_MARGIN = 0.05f;

    // 落下してから復活するまでの間にいるプレイヤー番号（同じプレイヤーの二重復活を防ぐ）
    private readonly HashSet<int> respawning = new();

    private void Start()
    {
        MatchStats.Reset();
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
        TopDownPlayerMove playerScript = collider.GetComponentInParent<TopDownPlayerMove>();
        if (playerScript == null || playerScript.joyconIndex < 0)
        {
            GameObject fallingObject = collider.attachedRigidbody != null
                ? collider.attachedRigidbody.gameObject
                : collider.gameObject;
            MatchStats.RegisterNonPlayerFall(fallingObject);
            return;
        }

        // 床の下からせり上がっている間は、場外の判定を受けない
        if (collider.TryGetComponent(out PlayerRespawnEffect effect) && effect.IsEmerging) return;

        // indexを読み取っておく
        int id = playerScript.joyconIndex;

        // 同じプレイヤーのコライダーが複数回トリガーに入っても、死亡は1回だけ数える
        if (!respawning.Add(id)) return;

        Debug.Log($"プレイヤー {id} が死んだあ");
        MatchStats.RegisterFall(id);
        Destroy(collider.gameObject);
        StartCoroutine(RespawnAfterDelay(id));
    }

    // 落下から respawnDelay 秒後に、プレイヤーごとの地点で復活させる
    private IEnumerator RespawnAfterDelay(int id)
    {
        yield return new WaitForSeconds(respawnDelay);

        respawning.Remove(id);

        // 試合が終わっていたら復活させない
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow != null && flow.State == GameState.Result) yield break;

        RespawnWithEffect(id);
    }

    // 復活の演出つき：プレイヤーごとの地点の床下から出現し、出現の開始から無敵になる
    private void RespawnWithEffect(int id)
    {
        if (id < 0 || id >= Players.Length || Players[id] == null)
        {
            Debug.LogError($"プレイヤーのインデックス {id} に対応するプレハブが登録されていません");
            return;
        }

        // 落下後も試合開始時と同じプレイヤーごとのリスポーン地点を使う
        Transform point = GetRespawnPoint(id);
        if (point == null)
        {
            Debug.LogError($"プレイヤー {id} のリスポーン地点が設定されていません");
            return;
        }

        // 床の面の位置から、球体の中心が「床の上に乗る位置」と「床の下に隠れる位置」を決める
        float radius = GetPlayerRadius(Players[id]);
        Vector3 surface = FindGroundPoint(point.position);
        Vector3 end = surface + (Vector3.up * radius);
        Vector3 start = surface + (Vector3.down * (radius + EMERGE_START_MARGIN));

        GameObject player = Instantiate(Players[id], start, point.rotation);
        PlayerRespawnEffect effect = player.AddComponent<PlayerRespawnEffect>();
        effect.Begin(start, end, emergeDuration, invincibleAfterEmerge, blinkInterval);

        Debug.Log($"プレイヤー {id} をリスポーンしました（出現演出つき）");
    }

    // 地点の真下にあるステージの床（Groundタグ）の面を探す。見つからなければ地点そのものを床とみなす
    private Vector3 FindGroundPoint(Vector3 position)
    {
        Vector3 origin = position + (Vector3.up * GROUND_RAY_HEIGHT);
        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHitBuffer, GROUND_RAY_HEIGHT + GROUND_RAY_DISTANCE, ~0, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        Vector3 groundPoint = position;
        bool found = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHitBuffer[i];
            // RespawnPoint より上にある床ではなく、地点の真下にある最も近い床を選ぶ
            if (!hit.collider.CompareTag(GROUND_TAG) || hit.point.y > position.y || hit.distance >= nearest) continue;

            nearest = hit.distance;
            groundPoint = hit.point;
            found = true;
        }

        if (!found)
        {
            Debug.LogWarning("復活地点の下に Ground タグの床が見つかりません。復活地点の高さをそのまま床として扱います");
        }
        return groundPoint;
    }

    // プレハブのSphereColliderから、ワールドでの球体の半径を求める
    private static float GetPlayerRadius(GameObject playerPrefab)
    {
        if (!playerPrefab.TryGetComponent(out SphereCollider sphere)) return 0.5f;

        Vector3 scale = playerPrefab.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return sphere.radius * maxScale;
    }

    // 試合開始時の配置（演出なし）
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
