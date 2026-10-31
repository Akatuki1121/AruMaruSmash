using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// リザルトの表彰台。順位順にキャラクターを台の上に並べ、成績テキストを台の下に移す。
/// キャラクターは Game で使うプレイヤー用プレハブ(Arumajiro Prefab 0..3)を複製し、
/// 操作・物理まわりのコンポーネントを外して見た目だけにして使う。
/// 台やカメラ位置はコードで作る(シーンに置くのはこのコンポーネントだけ)
/// </summary>
public class PodiumDisplay : MonoBehaviour
{
    [Header("キャラクター(要素番号 = プレイヤー番号。0 = 1P)")]
    [SerializeField] private GameObject[] characterPrefabs;

    [Header("配置")]
    [Tooltip("台と台の間隔(ワールド単位)")]
    [SerializeField] private float podiumSpacing = 3.6f;

    [Tooltip("キャラクター(丸まった球の部分)の直径。元モデルの大きさに関係なく、この大きさに揃える")]
    [SerializeField] private float characterWorldHeight = 3.0f;

    [Tooltip("キャラクターのY回転。カメラの方を向かない場合はここを変える(0 / 90 / 180 / 270)")]
    [SerializeField] private float characterYaw = 180f;

    [Header("カメラ")]
    [Tooltip("オンにすると、再生時にカメラを表彰台が収まる位置へ動かす")]
    [SerializeField] private bool setupCamera = true;

    // カメラ位置は固定値(4人 + 見出しが収まるように調整済み)
    private static readonly Vector3 CAMERA_POSITION = new Vector3(0f, 3.6f, -15.5f);
    private static readonly Vector3 CAMERA_LOOK_AT = new Vector3(0f, 3.1f, 0f);
    private const float CAMERA_FIELD_OF_VIEW = 40f;

    private const float PODIUM_DEPTH = 1.6f;
    private const float TEXT_WIDTH = 340f;

    private readonly List<GameObject> spawned = new List<GameObject>();

    /// <summary>順位表(スコア降順)を受け取って表彰台を組み立てる</summary>
    public void Show(List<MatchStats.Entry> ranking, TMP_Text[] playerTexts)
    {
        Clear();

        Camera cam = Camera.main;
        if (setupCamera && cam != null)
        {
            cam.transform.position = CAMERA_POSITION;
            cam.transform.LookAt(CAMERA_LOOK_AT);
            cam.fieldOfView = CAMERA_FIELD_OF_VIEW;
        }

        List<MatchStats.Entry> order = BuildDisplayOrder(ranking);
        int n = order.Count;

        for (int i = 0; i < n; i++)
        {
            MatchStats.Entry e = order[i];
            float x = (i - (n - 1) * 0.5f) * podiumSpacing;
            float height = PodiumHeight(e.rank);

            CreatePodiumBlock(x, height, e.rank);
            CreateCharacter(e.playerIndex, x, height);
            PlaceText(playerTexts, e.playerIndex, cam, x);
        }
    }

    /// <summary>
    /// 確認用: Inspector のこのコンポーネントを右クリック > Preview 4 Ranks(再生中のみ)。
    /// 1位～4位がすべて別順位の並びを表示して、台の高さの違いを確認できる
    /// </summary>
    [ContextMenu("Preview 4 Ranks")]
    private void PreviewFourRanks()
    {
        if (!Application.isPlaying) return;
        List<MatchStats.Entry> fake = new List<MatchStats.Entry>();
        for (int i = 0; i < 4; i++)
        {
            fake.Add(new MatchStats.Entry { playerIndex = i, rank = i + 1 });
        }
        Show(fake, null);
    }

    private void Clear()
    {
        foreach (GameObject go in spawned)
        {
            if (go != null) Destroy(go);
        }
        spawned.Clear();
    }

    /// <summary>
    /// 左から 2位・1位・3位・4位 の並び(表彰台の定番)。2人以下は順位順のまま
    /// </summary>
    private static List<MatchStats.Entry> BuildDisplayOrder(List<MatchStats.Entry> ranking)
    {
        List<MatchStats.Entry> order = new List<MatchStats.Entry>(ranking);
        if (order.Count >= 3)
        {
            MatchStats.Entry first = order[0];
            order[0] = order[1];
            order[1] = first;
        }
        return order;
    }

    private static float PodiumHeight(int rank)
    {
        // 順位が1つ下がるごとに0.7低くなる(同順位は同じ高さ)。1位2.4 / 2位1.7 / 3位1.0 / 4位0.4
        return Mathf.Max(0.4f, 2.4f - 0.7f * (rank - 1));
    }

    private static Color PodiumColor(int rank)
    {
        switch (rank)
        {
            case 1: return new Color(1.00f, 0.82f, 0.20f);   // 金
            case 2: return new Color(0.80f, 0.82f, 0.86f);   // 銀
            case 3: return new Color(0.80f, 0.50f, 0.25f);   // 銅
            default: return new Color(0.45f, 0.47f, 0.52f);
        }
    }

    private void CreatePodiumBlock(float x, float height, int rank)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = $"Podium_Rank{rank}";
        block.transform.SetParent(transform, false);
        block.transform.localScale = new Vector3(podiumSpacing * 0.85f, height, PODIUM_DEPTH);
        block.transform.localPosition = new Vector3(x, height * 0.5f, 0f);

        // このシーン専用の台。Ground タグがプロジェクトにあれば付ける(無ければ付けない)
        try { block.tag = "Ground"; } catch (UnityException) { }

        Renderer r = block.GetComponent<Renderer>();
        if (r != null) r.material.color = PodiumColor(rank);

        spawned.Add(block);
    }

    private void CreateCharacter(int playerIndex, float x, float podiumTop)
    {
        if (characterPrefabs == null || playerIndex < 0 || playerIndex >= characterPrefabs.Length) return;
        GameObject prefab = characterPrefabs[playerIndex];
        if (prefab == null) return;

        // 非アクティブな入れ物の中で複製すると Awake / OnEnable が走らない。
        // その間に操作・物理のコンポーネントを外し、見た目だけにしてから表に出す
        GameObject holder = new GameObject("CharacterHolder");
        holder.SetActive(false);
        GameObject character = Instantiate(prefab, holder.transform);
        character.name = $"Character_{playerIndex + 1}P";

        // ゲーム中の「ボール」を表す SphereCollider の位置と大きさを、削除する前に控える。
        // ゲーム中に地面へ接する位置と同じなので、台に接する位置の基準にも使える
        SphereCollider sphere = character.GetComponentInChildren<SphereCollider>(true);
        Transform sphereTransform = sphere != null ? sphere.transform : null;
        Vector3 sphereCenter = sphere != null ? sphere.center : Vector3.zero;
        float sphereRadius = sphere != null ? sphere.radius : 0f;

        StripToVisual(character);

        character.transform.SetParent(transform, false);
        character.transform.localRotation = Quaternion.Euler(0f, characterYaw, 0f);
        character.transform.localPosition = Vector3.zero;
        Destroy(holder);

        // 初期ポーズを反映してから測る(移動量はアニメーションに任せない)
        Animator anim = character.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
            anim.Update(0f);
            anim.speed = 0f;   // 初期ポーズのまま止める(回転などのアニメーションを再生しない)
        }

        Vector3 target = transform.TransformPoint(new Vector3(x, podiumTop, 0f));

        // 基本: 球の直径を揃え、球の一番下を台の天面に合わせる
        Vector3 worldCenter;
        float worldRadius;
        if (sphereTransform != null && sphereRadius > 0f
            && MeasureSphere(sphereTransform, sphereCenter, sphereRadius, out worldCenter, out worldRadius)
            && worldRadius > 0.0001f)
        {
            character.transform.localScale *= characterWorldHeight / (2f * worldRadius);

            MeasureSphere(sphereTransform, sphereCenter, sphereRadius, out worldCenter, out worldRadius);
            character.transform.position += new Vector3(
                target.x - worldCenter.x,
                target.y - (worldCenter.y - worldRadius),
                target.z - worldCenter.z);
        }
        else
        {
            // 球が無いプレハブ用の予備: 見た目の頂点から測る
            Bounds b;
            if (TryGetVisualBounds(character, out b) && b.size.y > 0.0001f)
            {
                character.transform.localScale *= characterWorldHeight / b.size.y;
                TryGetVisualBounds(character, out b);
                character.transform.position += new Vector3(
                    target.x - b.center.x,
                    target.y - b.min.y,
                    target.z - b.center.z);
            }
        }

        character.SetActive(true);
        spawned.Add(character);
    }

    private static bool MeasureSphere(Transform t, Vector3 localCenter, float localRadius,
        out Vector3 worldCenter, out float worldRadius)
    {
        Vector3 s = t.lossyScale;
        worldCenter = t.TransformPoint(localCenter);
        worldRadius = localRadius * Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
        return true;
    }
    private static void StripToVisual(GameObject root)
    {
        // 「他のコンポーネントが RequireComponent で要求しているもの」は後回しにして、
        // 依存される側が残ったり、削除エラーが出たりしないようにする
        List<MonoBehaviour> behaviours = new List<MonoBehaviour>(root.GetComponentsInChildren<MonoBehaviour>(true));
        HashSet<System.Type> required = new HashSet<System.Type>();
        foreach (MonoBehaviour mb in behaviours)
        {
            if (mb == null) continue;
            foreach (RequireComponent attr in
                (RequireComponent[])System.Attribute.GetCustomAttributes(mb.GetType(), typeof(RequireComponent), true))
            {
                if (attr.m_Type0 != null) required.Add(attr.m_Type0);
                if (attr.m_Type1 != null) required.Add(attr.m_Type1);
                if (attr.m_Type2 != null) required.Add(attr.m_Type2);
            }
        }
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (MonoBehaviour mb in behaviours)
            {
                if (mb == null) continue;
                bool isRequired = required.Contains(mb.GetType());
                if (isRequired == (pass == 1)) DestroyImmediate(mb);
            }
        }

        foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
        {
            DestroyImmediate(c);
        }
        foreach (AudioSource a in root.GetComponentsInChildren<AudioSource>(true))
        {
            DestroyImmediate(a);
        }
        foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
        {
            DestroyImmediate(rb);
        }

        // 念のため: 何かの理由で Rigidbody が残っても、落下しないように固定しておく
        foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    /// <summary>
    /// 見た目の実際の大きさ(ワールド座標)を、メッシュの頂点から測る。
    /// Renderer.bounds はスキンメッシュだと余白が付いて足元が浮くので使わない。
    /// MeshRenderer / SkinnedMeshRenderer だけが対象(パーティクルやトレイルは無視)
    /// </summary>
    private static bool TryGetVisualBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        List<Vector3> vertices = new List<Vector3>();

        foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (smr.sharedMesh == null) continue;
            Mesh baked = new Mesh();
            smr.BakeMesh(baked, false);
            baked.GetVertices(vertices);
            Matrix4x4 m = smr.transform.localToWorldMatrix;
            Encapsulate(vertices, m, ref bounds, ref found);
            Destroy(baked);
        }

        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(false))
        {
            if (mf.sharedMesh == null) continue;
            if (mf.GetComponent<MeshRenderer>() == null) continue;

            if (!mf.sharedMesh.isReadable)
            {
                // 頂点を読めない設定のメッシュ(ビルドで起こりうる)は、メッシュの外枠の8隅で代用する
                Bounds mb = mf.sharedMesh.bounds;
                vertices.Clear();
                for (int i = 0; i < 8; i++)
                {
                    vertices.Add(mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f)));
                }
            }
            else
            {
                mf.sharedMesh.GetVertices(vertices);
            }
            Encapsulate(vertices, mf.transform.localToWorldMatrix, ref bounds, ref found);
        }
        return found;
    }

    private static void Encapsulate(List<Vector3> vertices, Matrix4x4 m, ref Bounds bounds, ref bool found)
    {
        foreach (Vector3 v in vertices)
        {
            Vector3 w = m.MultiplyPoint3x4(v);
            if (!found)
            {
                bounds = new Bounds(w, Vector3.zero);
                found = true;
            }
            else
            {
                bounds.Encapsulate(w);
            }
        }
    }
    /// <summary>成績テキストを、その人の台の下に移す(画面座標に変換して置く)</summary>
    private void PlaceText(TMP_Text[] playerTexts, int playerIndex, Camera cam, float x)
    {
        if (cam == null || playerTexts == null) return;
        if (playerIndex < 0 || playerIndex >= playerTexts.Length) return;
        TMP_Text text = playerTexts[playerIndex];
        if (text == null) return;

        Vector3 world = transform.TransformPoint(new Vector3(x, -0.6f, -PODIUM_DEPTH * 0.5f));
        RectTransform rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(TEXT_WIDTH, rt.sizeDelta.y);
        rt.position = cam.WorldToScreenPoint(world);
    }
}
