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
    private const float INDICATOR_LIFT_RATIO = 0.03f;

    [Header("チャージ（「上書き」にチェックした項目以外はCharacterStatsの値を使う）")]
    [Tooltip("最大チャージに必要な長押し時間（秒）")]
    public FloatOverride chargeMaxTime;

    [Tooltip("チャージ中の最大移動速度の倍率（0〜1）。小さいほど遅い。0にすると進行方向を決められなくなる")]
    public FloatOverride chargeMoveSpeedMultiplier;

    [Tooltip("チャージ開始後、速度がチャージ中の上限まで落ちるときの減速の強さ（速度/秒）。切り返しにも使う")]
    public FloatOverride chargeDeceleration;

    [Header("ダッシュ")]
    [Tooltip("チャージ0秒（ちょい押し）のダッシュ速度。通常の最大移動速度に対する倍率")]
    public FloatOverride dashMinSpeedMultiplier;

    [Tooltip("最大チャージのダッシュ速度。通常の最大移動速度に対する倍率")]
    public FloatOverride dashMaxSpeedMultiplier;

    [Tooltip("ダッシュの持続時間（秒）。この時間をかけて速度が0に落ちる")]
    public FloatOverride dashDuration;

    [Header("ヒットストップ")]
    [Tooltip("最大チャージのダッシュで衝突したときに止める時間（秒・実時間）")]
    public FloatOverride hitStopDuration;

    [Header("インジケーター（足元のダッシュ方向）")]
    [Tooltip("未設定ならこのオブジェクトのMeshRendererのマテリアルを複製して使う。色はチャージ量で上書きされる")]
    public Material indicatorMaterial;
    public Color indicatorColorMin = Color.white;
    public Color indicatorColorMax = new(1f, 0.25f, 0.1f, 1f);

    [Tooltip("チャージ0秒のときの長さ（プレイヤー半径の何倍か。球体の中心から測る）")]
    [Min(0f)] public float indicatorMinLength = 1.6f;

    [Tooltip("最大チャージのときの長さ（プレイヤー半径の何倍か）")]
    [Min(0f)] public float indicatorMaxLength = 4.5f;

    [Tooltip("矢印の幅（プレイヤー半径の何倍か。頭の半幅）")]
    [Min(0f)] public float indicatorWidth = 0.5f;

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

    private CharacterStats Stats => move.stats != null ? move.stats : CharacterStats.Fallback;
    private float ChargeMaxTime => Mathf.Max(MIN_DURATION, chargeMaxTime.Resolve(Stats.chargeMaxTime));
    private float ChargeMoveSpeedMultiplier => Mathf.Clamp01(chargeMoveSpeedMultiplier.Resolve(Stats.chargeMoveSpeedMultiplier));
    private float ChargeDeceleration => Mathf.Max(0f, chargeDeceleration.Resolve(Stats.chargeDeceleration));
    private float DashMinSpeedMultiplier => Mathf.Max(0f, dashMinSpeedMultiplier.Resolve(Stats.dashMinSpeedMultiplier));
    private float DashMaxSpeedMultiplier => Mathf.Max(DashMinSpeedMultiplier, dashMaxSpeedMultiplier.Resolve(Stats.dashMaxSpeedMultiplier));
    private float DashDuration => Mathf.Max(MIN_DURATION, dashDuration.Resolve(Stats.dashDuration));
    private float HitStopDuration => Mathf.Max(0f, hitStopDuration.Resolve(Stats.hitStopDuration));

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

        // 吹っ飛ばされたら、チャージもダッシュも中断する（吹っ飛びが終わっても、一度離すまでは再チャージしない）
        if (move.IsKnockedBack)
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

        indicator.Show(groundPoint + (groundNormal * radius * INDICATOR_LIFT_RATIO), groundNormal, aimDirection, length, halfWidth, color);
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
        Material source = indicatorMaterial;
        bool useOwnMaterial = source != null;
        if (source == null)
        {
            MeshRenderer ownRenderer = GetComponent<MeshRenderer>();
            if (ownRenderer != null) source = ownRenderer.sharedMaterial;
        }

        return new DashIndicator($"DashIndicator ({name})", source, !useOwnMaterial);
    }

    /// <summary>
    /// 足元に置く、ダッシュ方向の矢印。プレイヤーの子にすると巨大スケールや回転の影響を受けるため、
    /// 独立したオブジェクトとして毎フレーム位置を合わせる。
    /// </summary>
    private sealed class DashIndicator
    {
        private static readonly string[] ColorPropertyNames = { "_BaseColor", "_Color" };
        private static readonly string[] TexturePropertyNames = { "_BaseMap", "_MainTex" };

        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly Vector3[] vertices = new Vector3[7];

        public DashIndicator(string objectName, Material source, bool stripTextures)
        {
            root = new GameObject(objectName);
            root.hideFlags = HideFlags.DontSave;

            mesh = new Mesh { name = objectName };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            // 真上から見て時計回り（表）。シャフト2枚＋矢じり1枚
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6 };
            mesh.normals = new[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up,
            };

            if (source != null)
            {
                material = new Material(source);
                if (stripTextures)
                {
                    // 元マテリアルのテクスチャが、UVの無い矢印に乗らないようにする
                    foreach (string propertyName in TexturePropertyNames)
                    {
                        if (material.HasProperty(propertyName)) material.SetTexture(propertyName, null);
                    }
                }
            }
            else
            {
                Debug.LogWarning("DashIndicator: マテリアルが見つからないため、Sprites/Defaultで代用します。インジケーターのマテリアルを設定してください。");
                material = new Material(Shader.Find("Sprites/Default"));
            }

            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = root.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            root.SetActive(false);
        }

        public void Show(Vector3 position, Vector3 groundNormal, Vector3 aimDirection, float length, float halfWidth, Color color)
        {
            Vector3 forward = Vector3.ProjectOnPlane(aimDirection, groundNormal);
            if (forward.sqrMagnitude < DIRECTION_EPSILON) forward = aimDirection;

            root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward.normalized, groundNormal));
            UpdateMesh(length, halfWidth);

            foreach (string propertyName in ColorPropertyNames)
            {
                if (material.HasProperty(propertyName)) material.SetColor(propertyName, color);
            }

            if (!root.activeSelf) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null && root.activeSelf) root.SetActive(false);
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root);
            if (mesh != null) Object.Destroy(mesh);
            if (material != null) Object.Destroy(material);
        }

        // 原点（球体の中心の真下）からZ+方向へ length の矢印。矢じりの長さは幅から決める
        private void UpdateMesh(float length, float halfWidth)
        {
            float shaftHalfWidth = halfWidth * 0.5f;
            float headLength = Mathf.Min(length * 0.45f, halfWidth * 2.2f);
            float shaftEnd = length - headLength;

            vertices[0] = new Vector3(-shaftHalfWidth, 0f, 0f);
            vertices[1] = new Vector3(-shaftHalfWidth, 0f, shaftEnd);
            vertices[2] = new Vector3(shaftHalfWidth, 0f, shaftEnd);
            vertices[3] = new Vector3(shaftHalfWidth, 0f, 0f);
            vertices[4] = new Vector3(-halfWidth, 0f, shaftEnd);
            vertices[5] = new Vector3(0f, 0f, length);
            vertices[6] = new Vector3(halfWidth, 0f, shaftEnd);

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }
    }
}