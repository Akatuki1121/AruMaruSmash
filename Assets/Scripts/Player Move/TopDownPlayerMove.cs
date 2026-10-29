using UnityEngine;

/// <summary>
/// 見下ろし視点のプレイヤー移動を制御するクラス。
/// JoyconDemo.csから一部を抜粋しコピペしたもの。
/// </summary>

public class TopDownPlayerMove : MonoBehaviour
{
    public Vector3 JoyAccel;

    [Header("通常移動（接地中）")]
    [Tooltip("接地中の最大移動速度（m/s）。傾きが最大のときに到達する速さ。上げるほど速く走る")]
    public float groundMoveSpeed = 50f;

    [Tooltip("接地中に入力方向へ加速する強さ。上げるほど目標速度に素早く届く（慣性が弱くなる）。下げるほど立ち上がりが重い")]
    public float groundAcceleration = 2.5f;

    [Tooltip("入力を離した/逆向きに入力したときの減速量（速度/秒）。上げるほど早く止まる（慣性が弱くなる）。下げるほど滑る")]
    public float groundDeceleration = 6f;

    [Header("空中制御")]
    [Tooltip("空中（接地していない間）の入力の効き具合。0=空中では入力で動けない（慣性のまま飛ぶ）/ 1=地上と同じ。" +
             "吹っ飛び中は別途ロックされるため、ここは歩いて落ちたときなどの通常移動中の空中に適用される。")]
    public float airControlMultiplier = 0f;

    [Header("速度の現在値")]
    public float currentSpeed = 0f;    // 現在の速度（実行時の確認用）

    private float GroundMoveSpeed => groundMoveSpeed;
    private float GroundAcceleration => groundAcceleration;
    private float GroundDeceleration => groundDeceleration;
    public float AirControlMultiplier => Mathf.Clamp01(airControlMultiplier);

    // 接地中の最大移動速度（ChargeDashControllerがダッシュ速度の基準にする）
    public float GroundMaxSpeed => GroundMoveSpeed;

    private Vector3 moveDirection = Vector3.zero;
    private const float MOVEMENT_VELOCITY_EPSILON = 0.0001f;

    public Rigidbody rb;

    // 現在の傾き量（0〜1）。これがそのまま速度倍率の元になる
    public float tiltAmount = 0f;

    [Header("Joy-Con設定")]
    public int joyconIndex = 0; // 使用するJoy-Conのインデックス（0または1）
    private Joycon joycon; // 接続されているJoy-Con本体への参照
    public Joycon ActiveJoycon => joycon;   // ChargeDashControllerがボタン入力を読むために公開

    private KnockbackController knockback;                // 吹っ飛び状態の参照用（吹っ飛び状態の管理はKnockbackController側）
    private bool wasKnockedBack = false;                  // 吹っ飛び開始の瞬間を検知するための前フレームの状態
    public bool IsKnockedBack => knockback != null && knockback.IsKnockedBack;   // ノックバック中フラグ（着地まで継続）

    // 衝突後の入力無効期間（knockbackLockDurationの秒数ぶん。着地したかどうかでは解除されない）
    public bool IsInputLocked => knockback != null && knockback.IsLocked;   // 時間だけで判定（接地・空中は関係ない）

    private ChargeDashController chargeDash;              // チャージ→ダッシュの担当（無くても動く）

    // 衝突の勝敗判定に使う「ダッシュ中」フラグ（実体はChargeDashController）
    public bool IsAttack => chargeDash != null && chargeDash.IsAttack;

    private bool IsCharging => chargeDash != null && chargeDash.IsCharging;

    // チャージ中は最大移動速度が下がり、減速も強くなる（進行方向を決められる程度の速さまで落とす）
    private float ChargeSpeedMultiplier => chargeDash != null ? chargeDash.MoveSpeedMultiplier : 1f;
    private float BrakeRate => IsCharging ? chargeDash.ChargeDecelerationRate : GroundDeceleration;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        // 移動・空中制御の値は、上のInspector値をそのまま使う

        // 吹っ飛び状態の参照を取得しておく
        knockback = GetComponent<KnockbackController>();

        // チャージ→ダッシュの参照を取得しておく（ダッシュ関連はChargeDashController側）
        chargeDash = GetComponent<ChargeDashController>();

        // Joy-Conの接続を試みる
        TryAcquireJoyCon();
    }

    // Update is called once per frame
    void Update()
    {
        TryAcquireJoyCon();

        // 吹っ飛び中・入力ロック中は入力による移動を止める。無効期間が始まった瞬間だけ、移動の状態をリセットする
        // （吹っ飛び状態・ロックの終了判定はKnockbackController側で行う）
        if (IsInputLocked)
        {
            if (!wasKnockedBack)
            {
                currentSpeed = 0f;
                moveDirection = Vector3.zero;
            }
            wasKnockedBack = true;
            return;
        }
        wasKnockedBack = false;

        if (JoyconManager.Instance != null && JoyconManager.Instance.j != null)
        {
            // 接続されているジョイコンの数を画面上に常時出す
            Debug.Log($"現在Unityが認識しているジョイコンの数: {JoyconManager.Instance.j.Count}台");

            for (int i = 0; i < JoyconManager.Instance.j.Count; i++)
            {
                var j = JoyconManager.Instance.j[i];
                Debug.Log($"Index [{i}]: {(j.isLeft ? "L(左)" : "R(右)")} - 接続状態: {j.state}");
            }
        }

        Vector3 oldDir = moveDirection;

        GetMoveDirection();

        bool hasInput = GetTiltX() > 0.2f || GetTiltX() < -0.2f || GetTiltY() > 0.2f || GetTiltY() < -0.2f;

        if (hasInput)
        {
            // 前回の移動方向と現在の移動方向が逆向きの場合、減速する
            if (currentSpeed > 0.1f && Vector3.Dot(oldDir, moveDirection) < -0.1f)
            {
                currentSpeed -= BrakeRate * Time.deltaTime;
                currentSpeed = Mathf.Max(currentSpeed, 0f);

                moveDirection = oldDir; // 逆方向入力時は前回の方向を維持する
            }
            else
            {
                float targetMaxSpeed = GroundMoveSpeed * tiltAmount * ChargeSpeedMultiplier;

                if (IsCharging && currentSpeed > targetMaxSpeed)
                {
                    // チャージ開始直後は、急に止めずに徐々に減速してチャージ中の上限へ近づける
                    currentSpeed = Mathf.MoveTowards(currentSpeed, targetMaxSpeed, BrakeRate * Time.deltaTime);
                }
                else
                {
                    currentSpeed += targetMaxSpeed * GroundAcceleration * Time.deltaTime;
                    currentSpeed = Mathf.Min(currentSpeed, targetMaxSpeed);
                }
            }
        }
        else
        {
            // 入力が無いときは、現在の速度が0より大きい場合に減速する
            if (currentSpeed > 0.01f)
            {
                currentSpeed -= BrakeRate * Time.deltaTime;
                currentSpeed = Mathf.Max(currentSpeed, 0f);

                // 入力がない場合も、直前の移動方向を維持して減速する
            }
            else
            {
                currentSpeed = 0f;
                moveDirection = Vector3.zero; // 入力がない場合は方向をリセット
            }
        }
    }

    public void GetMoveDirection()
    {
        if (joycon == null)
        {
            JoyAccel = Vector3.zero;
            tiltAmount = 0f;
            moveDirection = Vector3.zero;
            return;
        }

        JoyAccel = joycon.GetAccel();

        Debug.Log($"{gameObject.name} (Index:{joyconIndex}) のAccel: {JoyAccel}");

        const float deadZone = 0.1f;

        // 傾きをそのまま方向成分として使う（360度）
        float moveX = JoyAccel.x;
        float moveZ = -JoyAccel.y;

        Vector3 inputVector = new(moveX, 0f, moveZ);

        // デッドゾーン処理：小さい傾きは入力なしとして扱う
        float inputMagnitude = inputVector.magnitude;
        if (inputMagnitude <= deadZone)
        {
            moveDirection = Vector3.zero;
            tiltAmount = 0f;
            return;
        }

        // デッドゾーンを越えた分だけを0〜1へ正規化する
        float usableMagnitude = Mathf.Clamp01(
            (inputMagnitude - deadZone) / (1f - deadZone)
        );

        // 方向は360度、速度倍率は傾き量で決める
        moveDirection = inputVector.normalized;
        tiltAmount = usableMagnitude;
    }

    private void TryAcquireJoyCon()
    {
        if(joycon != null) return; // すでにJoy-Conを取得済み
        if(JoyconManager.Instance == null) return; // JoyconManagerが存在しない場合は何もしない
        if (JoyconManager.Instance.j == null) return; // Joy-Conが接続されていない場合は何もしない
        if(joyconIndex < 0 || joyconIndex >= JoyconManager.Instance.j.Count) return; // インデックスが範囲外の場合は何もしない

        joycon = JoyconManager.Instance.j[joyconIndex];

        Debug.Log($"<color=yellow>【デバッグ】Playerオブジェクト {gameObject.name} が Index {joyconIndex} のジョイコンを取得しました！ (L/R: {(joycon.isLeft ? "L" : "R")})</color>");
    }

    public float GetTiltX()
    {
        return JoyAccel.x;
    }

    public float GetTiltY()
    {
        return JoyAccel.y;
    }

    // 通常移動速度を返す（入力ロック中は入力由来の移動なし）
    public Vector3 GetNormalMoveVelocity()
    {
        if (IsKnockedBack) return Vector3.zero;
        return GetWorldMoveDirection() * currentSpeed;
    }

    // ダッシュによる追加速度を返す（実体はChargeDashController）
    public Vector3 GetDashVelocity()
    {
        if (IsInputLocked || chargeDash == null) return Vector3.zero;
        return chargeDash.GetDashVelocity();
    }

    // 通常移動とダッシュを合わせた入力由来の移動ベクトル
    public Vector3 GetInputMoveVelocity()
    {
        return GetNormalMoveVelocity() + GetDashVelocity();
    }

    // 現在の入力方向（ワールド座標・接地面に沿わせたもの）。入力が無ければ0
    public Vector3 WorldMoveDirection => GetWorldMoveDirection();

    private Vector3 GetWorldMoveDirection()
    {
        if (moveDirection.sqrMagnitude <= 0f) return Vector3.zero;

        return ProjectOnGround(transform.rotation * moveDirection);
    }

    // 方向ベクトルを足元の接地面に沿わせる（坂では斜面方向になる）。接地面が見つからなければそのまま返す
    public Vector3 ProjectOnGround(Vector3 direction)
    {
        Ray ray = new(rb.position + (Vector3.up * 0.2f), Vector3.down);
        if (Physics.Raycast(ray, out RaycastHit hit, 0.6f))
        {
            direction = Vector3.ProjectOnPlane(direction, hit.normal);
        }

        return direction;
    }

}
