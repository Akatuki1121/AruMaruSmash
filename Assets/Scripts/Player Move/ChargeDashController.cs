using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 「チャージ → ダッシュ」を1セットで担当するクラス。
///
/// 流れ：
/// - ダッシュボタン長押し：チャージ。チャージ中は移動が遅くなる（進行方向は傾きで決められる）
/// - ボタンを離す：チャージ時間に応じた速さで、その瞬間の進行方向へダッシュ
/// - ダッシュは dashDuration 秒かけて減速して終わる
/// - チャージ中は足元に、ダッシュ方向のインジケーターを出す
/// - 最大チャージのダッシュで最初に他プレイヤーと衝突したとき、ヒットストップを1回要求する
///
/// 通常移動はTopDownPlayerMoveが、衝突の解決とヒットストップの発動はPlayerCollisionHandlerが担当する。
/// このクラスは「今どの段階か」と「ダッシュによる追加速度」だけを公開する。
/// </summary>
[DefaultExecutionOrder(10)] // TopDownPlayerMove（既定=0）の後に動かし、同じフレームの進行方向を読む
[RequireComponent(typeof(TopDownPlayerMove))]
public class ChargeDashController : MonoBehaviour
{
    public enum Phase
    {
        Idle,
        Charging,
        Dashing,
    }

    // 方向ベクトルとみなす最小の大きさ
    private const float DIRECTION_EPSILON = 0.0001f;
    // 最大チャージとみなす割合（浮動小数点の誤差対策）
    private const float MAX_CHARGE_RATIO = 0.999f;
    // 最大チャージ時間・ダッシュ持続時間の下限（0除算回避）
    private const float MIN_DURATION = 0.01f;
    // インジケーターを接地面から浮かせる量（プレイヤー半径に対する割合。Z-fighting対策）
    private const float INDICATOR_LIFT_RATIO = 0.05f;

    [Header("チャージ")]
    [Tooltip("最大チャージに必要な長押し時間（秒）")]
    public float chargeMaxTime = 2.5f;

    [Tooltip("チャージ中の最大移動速度の倍率（0〜1）。小さいほど遅い。0にすると進行方向を決められなくなる")]
    public float chargeMoveSpeedMultiplier = 0.5f;

    [Tooltip("チャージ開始後、速度がチャージ中の上限まで落ちるときの減速の強さ（速度/秒）。切り返しにも使う")]
    public float chargeDeceleration = 15f;

    [Header("ダッシュ")]
    [Tooltip("チャージ0秒（ちょい押し）のダッシュ速度。通常の最大移動速度に対する倍率")]
    public float dashMinSpeedMultiplier = 1.5f;

    [Tooltip("最大チャージのダッシュ速度。通常の最大移動速度に対する倍率")]
    public float dashMaxSpeedMultiplier = 3f;

    [Tooltip("ダッシュの持続時間（秒）。この時間をかけて速度が0に落ちる")]
    public float dashDuration = 1f;

    [Header("ヒットストップ")]
    [Tooltip("最大チャージのダッシュで衝突したときに止める時間（秒・実時間）")]
    public float hitStopDuration = 0.05f;

    [Header("インジケーター（足元のダッシュ方向）")]
    [Tooltip("未設定なら、ライティングの影響を受けない単色のマテリアルを使う。色はチャージ量で上書きされる")]
    public Material indicatorMaterial;
    public Color indicatorColorMin = Color.white;
    public Color indicatorColorMax = new(1f, 0.25f, 0.1f, 1f);

    [Tooltip("チャージ0秒のときの長さ（プレイヤー半径の何倍か。球体の端から測る）")]
    [Min(0f)] public float indicatorMinLength = 1.6f;

    [Tooltip("最大チャージのときの長さ（プレイヤー半径の何倍か）")]
    [Min(0f)] public float indicatorMaxLength = 4.5f;

    [Tooltip("矢印の幅（プレイヤー半径の何倍か。頭の半幅）")]
    [Min(0f)] public float indicatorWidth = 0.5f;

    [Tooltip("矢印の縁取りの色。明るい床や赤い背景でも矢印が見えるようにする。アルファ0で縁取りなし")]
    public Color indicatorOutlineColor = new(0f, 0f, 0f, 0.75f);

    [Tooltip("縁取りの太さ（プレイヤー半径の何倍か）")]
    [Min(0f)] public float indicatorOutlineWidth = 0.12f;

    private TopDownPlayerMove move;
    private SphereCollider sphere;
    private DashIndicator indicator;

    private Phase phase = Phase.Idle;
    private float chargeTime;
    private bool requireRelease;          // 中断後は、一度ボタンを離すまで新しいチャージを始めない

    private Vector3 aimDirection = Vector3.forward;   // 水平の進行方向（最後に入力があった向き）
    private Vector3 dashDirection;
    private float dashSpeed;
    private float dashRemaining;
    private float dashTotal = 1f;
    private bool hitStopPending;

    private float ChargeMaxTime => Mathf.Max(MIN_DURATION, chargeMaxTime);
    private float ChargeMoveSpeedMultiplier => Mathf.Clamp01(chargeMoveSpeedMultiplier);
    private float ChargeDeceleration => Mathf.Max(0f, chargeDeceleration);
    private float DashMinSpeedMultiplier => Mathf.Max(0f, dashMinSpeedMultiplier);
    private float DashMaxSpeedMultiplier => Mathf.Max(DashMinSpeedMultiplier, dashMaxSpeedMultiplier);
    private float DashDuration => Mathf.Max(MIN_DURATION, dashDuration);
    private float HitStopDuration => Mathf.Max(0f, hitStopDuration);

    // ===== 他のコンポーネントから読む状態 =====

    public Phase CurrentPhase => phase;
    public bool IsCharging => phase == Phase.Charging;

    // 衝突の勝敗判定で「攻撃中」とみなすのは、ダッシュ中だけ（チャージ中は無防備）
    public bool IsAttack => phase == Phase.Dashing;

    // チャージ量（0〜1）。チャージ中以外は0
    public float ChargeRatio => phase == Phase.Charging ? Mathf.Clamp01(chargeTime / ChargeMaxTime) : 0f;

    // TopDownPlayerMoveの最大移動速度に掛ける倍率（チャージ中だけ小さくなる）
    public float MoveSpeedMultiplier => IsCharging ? ChargeMoveSpeedMultiplier : 1f;

    // チャージ中の減速の強さ
    public float ChargeDecelerationRate => ChargeDeceleration;

    // ダッシュによる追加速度（ダッシュ中以外は0）。向きは放した瞬間のもので固定、速さは時間とともに落ちる
    public Vector3 GetDashVelocity()
    {
        if (phase != Phase.Dashing) return Vector3.zero;

        float speed = dashSpeed * Mathf.Clamp01(dashRemaining / dashTotal);
        return move.ProjectOnGround(dashDirection) * speed;
    }

    // 最大チャージのダッシュ中に最初に呼ばれたときだけ、ヒットストップの長さ（秒）を返す。それ以外は0
    public float ConsumeHitStopDuration()
    {
        if (phase != Phase.Dashing || !hitStopPending) return 0f;

        hitStopPending = false;
        return HitStopDuration;
    }

    // ===== 処理 =====

    private void Awake()
    {
        move = GetComponent<TopDownPlayerMove>();
        sphere = GetComponent<SphereCollider>();

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > DIRECTION_EPSILON) aimDirection = forward.normalized;
    }

    private void Update()
    {
        bool held = ReadDashButtonHeld();
        if (!held) requireRelease = false;

        // 吹っ飛ばされたら（入力ロックが終わるまで）、チャージもダッシュも中断する（終わっても、一度離すまでは再チャージしない）
        if (move.IsInputLocked)
        {
            if (phase != Phase.Idle) Cancel();
            requireRelease = held;
            return;
        }

        if (phase != Phase.Dashing) TrackAimDirection();

        switch (phase)
        {
            case Phase.Idle:
                if (held && !requireRelease) BeginCharge();
                break;

            case Phase.Charging:
                if (held) chargeTime = Mathf.Min(chargeTime + Time.deltaTime, ChargeMaxTime);
                else ReleaseDash();
                break;

            case Phase.Dashing:
                dashRemaining -= Time.deltaTime;
                if (dashRemaining <= 0f) EndDash();
                break;
        }
    }

    private void LateUpdate()
    {
        if (phase != Phase.Charging)
        {
            indicator?.Hide();
            return;
        }

        if (indicator == null) indicator = CreateIndicator();

        float ratio = ChargeRatio;
        float radius = GetPlayerRadius();
        GetGroundPoint(radius, out Vector3 groundPoint, out Vector3 groundNormal);

        float length = radius * Mathf.Lerp(indicatorMinLength, indicatorMaxLength, ratio);
        float halfWidth = radius * indicatorWidth;
        Color color = Color.Lerp(indicatorColorMin, indicatorColorMax, ratio);

        // 矢印は球体の端から始める（中心から始めると、球体の下に隠れて見えない部分ができる）
        indicator.Show(groundPoint + (groundNormal * radius * INDICATOR_LIFT_RATIO), groundNormal, aimDirection, radius, length, halfWidth, color, indicatorOutlineColor, radius * Mathf.Max(0f, indicatorOutlineWidth));
    }

    private void OnDisable()
    {
        Cancel();
        indicator?.Hide();
    }

    private void OnDestroy()
    {
        indicator?.Destroy();
        indicator = null;
    }

    private void BeginCharge()
    {
        phase = Phase.Charging;
        chargeTime = 0f;
    }

    private void ReleaseDash()
    {
        float ratio = Mathf.Clamp01(chargeTime / ChargeMaxTime);

        dashSpeed = move.GroundMaxSpeed * Mathf.Lerp(DashMinSpeedMultiplier, DashMaxSpeedMultiplier, ratio);
        dashDirection = aimDirection;
        dashTotal = DashDuration;
        dashRemaining = dashTotal;
        hitStopPending = ratio >= MAX_CHARGE_RATIO;

        chargeTime = 0f;
        phase = Phase.Dashing;
    }

    private void EndDash()
    {
        phase = Phase.Idle;
        hitStopPending = false;
    }

    private void Cancel()
    {
        phase = Phase.Idle;
        chargeTime = 0f;
        hitStopPending = false;
    }

    // 入力があるあいだ、最後に入力された水平方向を覚えておく（入力が無い状態で放したときのダッシュ方向になる）
    private void TrackAimDirection()
    {
        Vector3 direction = move.WorldMoveDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude > DIRECTION_EPSILON) aimDirection = direction.normalized;
    }

    private bool ReadDashButtonHeld()
    {
        Joycon joycon = move.ActiveJoycon;
        if (joycon != null)
        {
            // 横持ち時に「物理的に一番右側（進行方向）」にあるボタン。Lは縦持ちの▼（DPAD_DOWN）、Rは縦持ちの▲（DPAD_UP）
            return joycon.isLeft
                ? joycon.GetButton(Joycon.Button.DPAD_DOWN)
                : joycon.GetButton(Joycon.Button.DPAD_UP);
        }

        // Joy-Conが無いときのデバッグ用に、PCではEキーでも動く
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.eKey.isPressed;
    }

    // ===== インジケーター =====

    private float GetPlayerRadius()
    {
        if (sphere == null) return 1f;

        Vector3 scale = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return sphere.radius * maxScale;
    }

    // 球体の真下の接地点。見つからなければ「半径ぶん下」を足元とみなす
    private void GetGroundPoint(float radius, out Vector3 point, out Vector3 normal)
    {
        Vector3 center = move.rb != null ? move.rb.position : transform.position;

        // 球体の中心から撃つので、自分自身のコライダーには当たらない
        if (Physics.Raycast(center, Vector3.down, out RaycastHit hit, radius * 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
            normal = hit.normal;
            return;
        }

        point = center + (Vector3.down * radius);
        normal = Vector3.up;
    }

    private DashIndicator CreateIndicator()
    {
        return new DashIndicator($"DashIndicator ({name})", indicatorMaterial);
    }

    /// <summary>
    /// 足元に置く、ダッシュ方向の矢印。プレイヤーの子にすると巨大スケールや回転の影響を受けるため、
    /// 独立したオブジェクトとして毎フレーム位置を合わせる。
    /// 縁取り（暗い矢印）を一回り大きく後ろに描いて、床や背景の色に関係なく見えるようにする。
    /// </summary>
    private sealed class DashIndicator
    {
        private static readonly string[] ColorPropertyNames = { "_BaseColor", "_Color" };
        private static readonly int[] Triangles = { 0, 1, 2, 0, 2, 3, 4, 5, 6 }; // 真上から見て時計回り（表）。シャフト2枚＋矢じり1枚

        // 縁取りを先に、矢印本体を後に描く
        private const int OUTLINE_RENDER_QUEUE = 3000;
        private const int FILL_RENDER_QUEUE = 3001;
        // 縁取りの矢じりは、先端が尖っているぶん太さより長く伸ばす
        private const float OUTLINE_TIP_SCALE = 2.2f;
        private const float OUTLINE_HEAD_WIDTH_SCALE = 1.6f;

        private readonly GameObject root;
        private readonly Mesh fillMesh;
        private readonly Material fillMaterial;
        private readonly GameObject outlineRoot;
        private readonly Mesh outlineMesh;
        private readonly Material outlineMaterial;
        private readonly Vector3[] fillVertices = new Vector3[7];
        private readonly Vector3[] outlineVertices = new Vector3[7];

        public DashIndicator(string objectName, Material source)
        {
            root = new GameObject(objectName);
            root.hideFlags = HideFlags.DontSave;

            fillMesh = CreateMesh(objectName, fillVertices);
            fillMaterial = source != null ? new Material(source) : new Material(Shader.Find("Sprites/Default"));
            fillMaterial.renderQueue = FILL_RENDER_QUEUE;
            AddRenderer(root, fillMesh, fillMaterial);

            Shader unlit = Shader.Find("Sprites/Default");
            if (unlit != null)
            {
                outlineRoot = new GameObject($"{objectName} Outline");
                outlineRoot.hideFlags = HideFlags.DontSave;
                outlineRoot.transform.SetParent(root.transform, false);

                outlineMesh = CreateMesh($"{objectName} Outline", outlineVertices);
                outlineMaterial = new Material(unlit);
                outlineMaterial.renderQueue = OUTLINE_RENDER_QUEUE;
                AddRenderer(outlineRoot, outlineMesh, outlineMaterial);
            }

            root.SetActive(false);
        }

        // startOffset: 矢印の根元の位置（原点＝球体の中心の真下から前方への距離）。length: そこからの長さ
        public void Show(Vector3 position, Vector3 groundNormal, Vector3 aimDirection, float startOffset, float length, float halfWidth, Color color, Color outlineColor, float outlineThickness)
        {
            Vector3 forward = Vector3.ProjectOnPlane(aimDirection, groundNormal);
            if (forward.sqrMagnitude < DIRECTION_EPSILON) forward = aimDirection;

            root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward.normalized, groundNormal));

            UpdateFillMesh(startOffset, length, halfWidth);
            SetColor(fillMaterial, color);

            bool showOutline = outlineRoot != null && outlineColor.a > 0f && outlineThickness > 0f;
            if (outlineRoot != null)
            {
                if (outlineRoot.activeSelf != showOutline) outlineRoot.SetActive(showOutline);
                if (showOutline)
                {
                    UpdateOutlineMesh(startOffset, length, halfWidth, outlineThickness);
                    SetColor(outlineMaterial, outlineColor);
                }
            }

            if (!root.activeSelf) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null && root.activeSelf) root.SetActive(false);
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root); // 縁取りは子なので一緒に消える
            if (fillMesh != null) Object.Destroy(fillMesh);
            if (fillMaterial != null) Object.Destroy(fillMaterial);
            if (outlineMesh != null) Object.Destroy(outlineMesh);
            if (outlineMaterial != null) Object.Destroy(outlineMaterial);
        }

        private static Mesh CreateMesh(string meshName, Vector3[] vertices)
        {
            Mesh mesh = new() { name = meshName };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.triangles = Triangles;
            mesh.normals = new[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up,
            };
            return mesh;
        }

        private static void AddRenderer(GameObject target, Mesh mesh, Material material)
        {
            target.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = target.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        private static void SetColor(Material material, Color color)
        {
            foreach (string propertyName in ColorPropertyNames)
            {
                if (material.HasProperty(propertyName)) material.SetColor(propertyName, color);
            }
        }

        // 根元(z=startOffset)から先端(z=startOffset+length)までの矢印。矢じりの長さは幅から決める
        private void UpdateFillMesh(float startOffset, float length, float halfWidth)
        {
            float shaftHalfWidth = halfWidth * 0.5f;
            float tip = startOffset + length;
            float headLength = Mathf.Min(length * 0.45f, halfWidth * 2.2f);
            float shaftEnd = tip - headLength;

            fillVertices[0] = new Vector3(-shaftHalfWidth, 0f, startOffset);
            fillVertices[1] = new Vector3(-shaftHalfWidth, 0f, shaftEnd);
            fillVertices[2] = new Vector3(shaftHalfWidth, 0f, shaftEnd);
            fillVertices[3] = new Vector3(shaftHalfWidth, 0f, startOffset);
            fillVertices[4] = new Vector3(-halfWidth, 0f, shaftEnd);
            fillVertices[5] = new Vector3(0f, 0f, tip);
            fillVertices[6] = new Vector3(halfWidth, 0f, shaftEnd);

            fillMesh.vertices = fillVertices;
            fillMesh.RecalculateBounds();
        }

        // 本体の矢印を thickness ぶん一回り大きくしたもの
        private void UpdateOutlineMesh(float startOffset, float length, float halfWidth, float thickness)
        {
            float shaftHalfWidth = (halfWidth * 0.5f) + thickness;
            float headHalfWidth = halfWidth + (thickness * OUTLINE_HEAD_WIDTH_SCALE);
            float tip = startOffset + length;
            float headLength = Mathf.Min(length * 0.45f, halfWidth * 2.2f);
            float shaftEnd = tip - headLength;
            float baseZ = startOffset - thickness;
            float headBaseZ = shaftEnd - thickness;

            outlineVertices[0] = new Vector3(-shaftHalfWidth, 0f, baseZ);
            outlineVertices[1] = new Vector3(-shaftHalfWidth, 0f, shaftEnd);
            outlineVertices[2] = new Vector3(shaftHalfWidth, 0f, shaftEnd);
            outlineVertices[3] = new Vector3(shaftHalfWidth, 0f, baseZ);
            outlineVertices[4] = new Vector3(-headHalfWidth, 0f, headBaseZ);
            outlineVertices[5] = new Vector3(0f, 0f, tip + (thickness * OUTLINE_TIP_SCALE));
            outlineVertices[6] = new Vector3(headHalfWidth, 0f, headBaseZ);

            outlineMesh.vertices = outlineVertices;
            outlineMesh.RecalculateBounds();
        }
    }
}