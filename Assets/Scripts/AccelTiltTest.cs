using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 「傾けるほど加速する」入力の検証用スクリプト。
/// 本来はJoy-Conの加速度センサー/ジャイロの傾き量を使う想定だが、
/// 実機検証ができない間はQ/Eキーでtilt量（0〜1）を代用して動きを確認する。
///
/// Q : 長押しすると時間をかけてtiltAmountが0→1へ滑らかに上昇する
/// E : 押した瞬間にtiltAmountが一気に1.0へジャンプする
/// どちらも離すと時間をかけて減衰する
///
/// さらに、移動方向の入力が直前のmoveDirectionと逆向き（内積が負）になった瞬間は
/// 車の急ブレーキのようにtiltAmountを急減速させる。
///
/// MoveManager_Testの speed には (1 + tiltAmount * accelMultiplier) を掛けて反映する。
/// MoveManager_Test本体の改造を避けるため、外部からspeedを書き換える形にしている。
/// </summary>
[RequireComponent(typeof(MoveManager_Test))]
public class AccelTiltTest : MonoBehaviour
{
    // 浮動小数点数の許容値
    private const float FLOAT_EPSILON = 0.0001f;

    // Tilt量の判定閾値
    private const float TILT_NEARLY_ZERO_THRESHOLD = 0.01f;

    // Tilt量の範囲
    private const float TILT_MIN_VALUE = 0f;
    private const float TILT_MAX_VALUE = 1f;

    // 速度倍率の計算
    private const float SPEED_MULTIPLIER_BASE = 1f;

    // 入力方向の計算
    private const float INPUT_DIRECTION_POSITIVE = 1f;
    private const float INPUT_DIRECTION_NEGATIVE = -1f;

    [Header("参照")]
    private MoveManager_Test moveManager;

    [Header("ベース速度（MoveManager_Test.speedの基準値として保持）")]
    public float baseSpeed = 1f;

    [Header("Qキー：長押し加速")]
    [Tooltip("Qを押し続けた時、tiltAmountが0→1に達するまでの秒数")]
    public float qRiseDuration = 1.5f;

    [Header("Eキー：急加速")]
    [Tooltip("Eを押した瞬間にtiltAmountをこの値までジャンプさせる（基本1.0）")]
    public float eJumpAmount = 1.0f;

    [Header("共通：キーを離した時の減衰")]
    [Tooltip("Q/Eを離した後、tiltAmountが0に戻るまでの秒数")]
    public float decayDuration = 1.0f;

    [Header("急ブレーキ（逆方向入力検知）")]
    [Tooltip("急ブレーキ発生とみなす内積のしきい値（-1〜0、0に近いほど敏感）")]
    public float reverseDotThreshold = -0.1f;

    [Tooltip("急ブレーキ時、tiltAmountが0に落ちるまでの秒数（通常減衰より短くする）")]
    public float brakeDuration = 0.15f;

    [Header("速度への反映")]
    [Tooltip("tiltAmount=1.0の時、speedに何倍までブーストするか（例: 1.0なら最大2倍速）")]
    public float accelMultiplier = 1.0f;

    [Header("デバッグ表示")]
    public bool showDebugLog = false;

    // 現在の傾き量（0〜1）。これがそのまま速度倍率の元になる
    public float tiltAmount = 0f;

    // 直前フレームのmoveDirection（MoveManager_Test側はprivateなので、入力から同じロジックで自前計算する）
    private Vector3 previousMoveDirection = Vector3.zero;

    private bool isBraking = false;
    private float brakeTimer = 0f;

    private void Awake()
    {
        moveManager = GetComponent<MoveManager_Test>();
        if (baseSpeed <= 0f)
        {
            baseSpeed = moveManager.speed;
        }
    }

    private void Update()
    {
        HandleQEInput();
        HandleReverseBrakeCheck();
        ApplyTiltToSpeed();
    }

    private void HandleQEInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // 急ブレーキ中は通常のQ/E加速処理よりブレーキ減衰を優先する
        if (isBraking)
        {
            brakeTimer -= Time.deltaTime;
            tiltAmount = Mathf.Lerp(tiltAmount, TILT_MIN_VALUE, Time.deltaTime / Mathf.Max(brakeDuration, FLOAT_EPSILON));

            if (brakeTimer <= 0f || tiltAmount < TILT_NEARLY_ZERO_THRESHOLD)
            {
                tiltAmount = TILT_MIN_VALUE;
                isBraking = false;
            }
            return;
        }

        bool qHeld = kb.qKey.isPressed;
        bool ePressedThisFrame = kb.eKey.wasPressedThisFrame;
        bool eHeld = kb.eKey.isPressed;

        if (ePressedThisFrame)
        {
            // Eは押した瞬間に一気にジャンプ
            tiltAmount = Mathf.Max(tiltAmount, eJumpAmount);
        }

        if (qHeld || eHeld)
        {
            if (qHeld)
            {
                // Qは長押しで滑らかに上昇
                float riseSpeed = SPEED_MULTIPLIER_BASE / Mathf.Max(qRiseDuration, FLOAT_EPSILON);
                tiltAmount = Mathf.MoveTowards(tiltAmount, TILT_MAX_VALUE, riseSpeed * Time.deltaTime);
            }
            // eHeldのみの場合はジャンプ直後の値を維持するだけで、Qのような追加上昇はしない
        }
        else
        {
            // どちらも離されていたら時間経過で減衰
            float decaySpeed = SPEED_MULTIPLIER_BASE / Mathf.Max(decayDuration, FLOAT_EPSILON);
            tiltAmount = Mathf.MoveTowards(tiltAmount, TILT_MIN_VALUE, decaySpeed * Time.deltaTime);
        }

        tiltAmount = Mathf.Clamp01(tiltAmount);

        if (showDebugLog)
        {
            Debug.Log($"[AccelTiltTest] tiltAmount = {tiltAmount:F2} (Q:{qHeld} E:{eHeld})");
        }
    }

    private void HandleReverseBrakeCheck()
    {
        Vector3 currentInputDir = GetCurrentInputDirection();

        // 入力がない、または直前の移動方向が無いなら逆転判定はしない
        if (currentInputDir.sqrMagnitude > FLOAT_EPSILON && previousMoveDirection.sqrMagnitude > FLOAT_EPSILON)
        {
            float dot = Vector3.Dot(currentInputDir.normalized, previousMoveDirection.normalized);

            if (dot < reverseDotThreshold && !isBraking)
            {
                // 進行方向と逆の入力が来た → 急ブレーキ発生
                isBraking = true;
                brakeTimer = brakeDuration;

                if (showDebugLog)
                {
                    Debug.Log($"[AccelTiltTest] 急ブレーキ発生！ dot = {dot:F2}");
                }
            }
        }

        if (currentInputDir.sqrMagnitude > FLOAT_EPSILON)
        {
            previousMoveDirection = currentInputDir;
        }
    }

    // MoveManager_Test.GetMoveDirection()と同じロジックで、現フレームの入力方向だけを取得する
    // (MoveManager_Test.moveDirectionはprivateで慣性減衰が混ざっているため、純粋な入力方向を別途计算する)
    private Vector3 GetCurrentInputDirection()
    {
        float moveX = 0f;
        float moveZ = 0f;

        if (moveManager.GetInputRight()) moveX += INPUT_DIRECTION_POSITIVE;
        if (moveManager.GetInputLeft()) moveX += INPUT_DIRECTION_NEGATIVE;
        if (moveManager.GetInputUp()) moveZ += INPUT_DIRECTION_POSITIVE;
        if (moveManager.GetInputDown()) moveZ += INPUT_DIRECTION_NEGATIVE;

        Vector3 inputVector = new Vector3(moveX, 0f, moveZ);
        return inputVector.sqrMagnitude > 0f ? inputVector.normalized : Vector3.zero;
    }

    private void ApplyTiltToSpeed()
    {
        moveManager.speed = baseSpeed * (SPEED_MULTIPLIER_BASE + (tiltAmount * accelMultiplier));
    }
}
