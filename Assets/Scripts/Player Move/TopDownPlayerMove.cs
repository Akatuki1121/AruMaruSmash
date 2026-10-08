using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 見下ろし視点のプレイヤー移動を制御するクラス。
/// JoyconDemo.csから一部を抜粋しコピペしたもの。
/// </summary>

public class TopDownPlayerMove : MonoBehaviour
{
    JoyconAccelReceiver JoyAccelRec;
    public Vector3 JoyAccel;

    [Header("キャラクターデータ（各値のデフォルト。未設定ならコード上の初期値を使う）")]
    public CharacterStats stats;

    // 値の決定順：このコンポーネントの「上書き」がON → その値 / OFF → CharacterStatsの値
    private CharacterStats Stats => stats != null ? stats : CharacterStats.Fallback;

    [Header("通常移動（接地中）")]
    [Tooltip("接地中の最大移動速度（m/s）。傾きが最大のときに到達する速さ。上げるほど速く走る")]
    public FloatOverride groundMoveSpeed;

    [Tooltip("接地中に入力方向へ加速する強さ。上げるほど目標速度に素早く届く（慣性が弱くなる）。下げるほど立ち上がりが重い")]
    public FloatOverride groundAcceleration;

    [Tooltip("入力を離した/逆向きに入力したときの減速量（速度/秒）。上げるほど早く止まる（慣性が弱くなる）。下げるほど滑る")]
    public FloatOverride groundDeceleration;

    [Header("空中制御")]
    [Tooltip("空中（接地していない間）の入力の効き具合。0=空中では入力で動けない（慣性のまま飛ぶ）/ 1=地上と同じ。" +
             "吹っ飛び中は別途ロックされるため、ここは歩いて落ちたときなどの通常移動中の空中に適用される。")]
    public FloatOverride airControlMultiplier;

    [Header("速度の現在値")]
    public float currentSpeed = 0f;    // 現在の速度（実行時の確認用）

    private float GroundMoveSpeed => groundMoveSpeed.Resolve(Stats.maxSpeed);
    private float GroundAcceleration => groundAcceleration.Resolve(Stats.acceleration);
    private float GroundDeceleration => groundDeceleration.Resolve(Stats.brakeSpeed);
    private float AirControlMultiplier => Mathf.Clamp01(airControlMultiplier.Resolve(Stats.airControlMultiplier));

    private Vector3 moveDirection = Vector3.zero;
    private const float MOVEMENT_VELOCITY_EPSILON = 0.0001f;

    public Rigidbody rb;

    // 現在の傾き量（0〜1）。これがそのまま速度倍率の元になる
    public float tiltAmount = 0f;

    [Header("ダッシュ（加速ボタン）")]
    [Tooltip("ボタンを押した瞬間に、ダッシュの強さ（0〜1）をこの値まで一気に上げる。基本1。下げると最大ダッシュに届かない")]
    public FloatOverride dashAmount;

    [Tooltip("ボタンを離してから、ダッシュの強さが0に戻るまでの秒数。上げるほどダッシュ後も速さが長く残る")]
    public FloatOverride dashDuration;

    [Tooltip("ダッシュ最大時に、移動速度へ上乗せする倍率。1なら通常の2倍速（1+1）。上げるほどダッシュが速くなる")]
    public FloatOverride dashSpeedMultiplier;

    private float DashAmount => dashAmount.Resolve(Stats.dashAmount);
    private float DashDuration => dashDuration.Resolve(Stats.dashDuration);
    private float DashSpeedMultiplier => dashSpeedMultiplier.Resolve(Stats.dashSpeedMultiplier);

    // 現在のダッシュブースト
    [SerializeField] private float dashTiltAmount = 0f;

    [Header("Joy-Con設定")]
    public int joyconIndex = 0; // 使用するJoy-Conのインデックス（0または1）
    private Joycon joycon; // 接続されているJoy-Con本体への参照

    private float knockbackTimer = 0f;
    private bool knockedAirborne = false;                 // 吹っ飛ばされて着地するまでの間true
    private KnockbackController knockback;                // 着地判定の参照用
    private const float KNOCKBACK_MAX_AIR_TIME = 3f;      // 着地を検知できなかった場合の安全弁（秒）
    public bool IsKnockedBack => knockbackTimer > 0f || knockedAirborne;   // ノックバック中フラグ（着地まで継続）

    public bool IsAttack;   // 攻撃フラグ

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //if (JoyAccelRec == null)
        //{
        //    JoyAccelRec = GetComponent<JoyconAccelReceiver>();
        //}
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        // 移動・ダッシュ・空中制御の値は、上のFloatOverride経由でCharacterStats（またはその上書き）から都度取得する

        // 着地判定用にKnockbackControllerを取得しておく
        knockback = GetComponent<KnockbackController>();

        // Joy-Conの接続を試みる
        TryAcquireJoyCon();

        // 攻撃時判定をリセット
        IsAttack = false;
    }

    // Update is called once per frame
    void Update()
    {
        // 吹っ飛び中：最低限のロック時間が過ぎたうえで着地したら、通常操作に復帰する
        // （着地を検知できない場合でも、安全弁の時間を超えたら復帰する）
        if (knockedAirborne && knockbackTimer <= 0f &&
            (knockback == null || knockback.IsGrounded || knockbackTimer < -KNOCKBACK_MAX_AIR_TIME))
        {
            knockedAirborne = false;
        }

        if (IsKnockedBack)
        {
            knockbackTimer -= Time.deltaTime;
            return;
        }

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

        HandleDashInput();

        bool hasInput = GetTiltX() > 0.2f || GetTiltX() < -0.2f || GetTiltY() > 0.2f || GetTiltY() < -0.2f;

        Vector3 currentPhysicalDir = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).normalized;

        if (hasInput)
        {
            // 前回の移動方向と現在の移動方向が逆向きの場合、減速する
            if (currentSpeed > 0.1f && Vector3.Dot(oldDir, moveDirection) < -0.1f && Vector3.Dot(oldDir, moveDirection) < -0.1f)
            {
                currentSpeed -= GroundDeceleration * Time.deltaTime;
                currentSpeed = Mathf.Max(currentSpeed, 0f);

                moveDirection = oldDir; // 逆方向入力時は前回の方向を維持する
            }
            else
            {
                float speedBoostFactor = 1.0f + (dashTiltAmount * DashSpeedMultiplier);
                float targetMaxSpeed = GroundMoveSpeed * tiltAmount * speedBoostFactor;

                currentSpeed += targetMaxSpeed * GroundAcceleration * Time.deltaTime;
                currentSpeed = Mathf.Min(currentSpeed, targetMaxSpeed);
            }
        }
        else
        {
            // 入力が無いときは、現在の速度が0より大きい場合にのみ加速する
            if (currentSpeed > 0.01f)
            {
                currentSpeed -= GroundDeceleration * Time.deltaTime;
                currentSpeed = Mathf.Max(currentSpeed, 0f);

                // 入力がない場合でも、現在の物理的な移動方向を維持する
                if (currentPhysicalDir.sqrMagnitude > 0)
                {
                    moveDirection = currentPhysicalDir;
                }
            }
            else
            {
                currentSpeed = 0f;
                moveDirection = Vector3.zero; // 入力がない場合は方向をリセット
            }
        }
    }

    private void FixedUpdate()
    {
        if (IsKnockedBack) return;
        PlayerMove(currentSpeed);
    }

    public void PlayerMove(float speed)
    {
        // 空中（接地していない）間は、入力による水平速度の変更を airControlMultiplier 分だけに絞る。
        // 0のときはXZ速度に一切触れず、そのまま慣性で飛ぶ（入力なしでXZ速度を0にする処理も行わない）。
        bool isAirborne = knockback != null && !knockback.IsGrounded;
        float control = isAirborne ? AirControlMultiplier : 1f;
        if (control <= 0f) return;

        if (moveDirection.sqrMagnitude > 0 && speed > 0.01f)
        {
            // 入力方向をワールド座標に変換
            Vector3 localMoveDir = transform.rotation * moveDirection;
            Vector3 targetVelocity = localMoveDir * speed;

            // 足元にレイを飛ばして地面の傾きを検知
            Ray ray = new(rb.position + (Vector3.up * 0.2f), Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, 0.6f))
            {
                // 地面の斜面に沿うように速度ベクトルを曲げる
                targetVelocity = Vector3.ProjectOnPlane(targetVelocity, hit.normal);
            }

            // XZ方向の速度を設定（Y方向の速度はそのままにする）。空中では現在速度から目標速度へcontrol分だけ近づける
            Vector3 currentXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            Vector3 newXZ = Vector3.Lerp(currentXZ, new Vector3(targetVelocity.x, 0f, targetVelocity.z), control);
            rb.linearVelocity = new Vector3(newXZ.x, rb.linearVelocity.y, newXZ.z);
        }
        else
        {
            // 入力がない場合、XZ方向の速度をゼロにする（Y方向の速度はそのままにする）。空中ではcontrol分だけ減速
            Vector3 currentXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            Vector3 newXZ = Vector3.Lerp(currentXZ, Vector3.zero, control);
            rb.linearVelocity = new Vector3(newXZ.x, rb.linearVelocity.y, newXZ.z);
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

        // 以下のログを追加して、どっちのプレイヤーがどの値を検知しているか確認
        Debug.Log($"{gameObject.name} (Index:{joyconIndex}) のAccel: {JoyAccel}");

        float moveX = 0;
        float moveZ = 0;

        // ジョイコンの前後傾き（Y）で、画面の上下（Z軸）を移動
        if (JoyAccel.y > 0.2f) moveZ -= 1f;
        if (JoyAccel.y < -0.2f) moveZ += 1f;

        // ジョイコンの左右傾き（X）で、画面の左右（X軸）を移動
        if (JoyAccel.x > 0.2f) moveX += 1f;
        if (JoyAccel.x < -0.2f) moveX -= 1f;

        Vector3 inputVector = new(moveX, 0f, moveZ);

        // 現在の傾き量を計算（0〜1）
        tiltAmount = Mathf.Clamp01(inputVector.magnitude);

        if (inputVector.sqrMagnitude > 0)
        {
            moveDirection = inputVector.normalized;

            // 傾き量を計算するために、X軸とY軸の傾きの絶対値の最大値を使用
            float rawTilt = Mathf.Max(Mathf.Abs(JoyAccel.x), Mathf.Abs(JoyAccel.y));
            float normalizedTilt = (rawTilt - 0.2f) / (1f - 0.2f); // 0.2〜1の範囲を0〜1に正規化
            tiltAmount = Mathf.Clamp01(normalizedTilt);
        }
        else
        {
            moveDirection = Vector3.zero;
            tiltAmount = 0f;
        }
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

    // 現在速度と入力方向を掛け合わせた移動ベクトルを返す
    public Vector3 GetInputMoveVelocity()
    {
        return moveDirection * currentSpeed;
    }

    public void HandleDashInput()
    {
        //TryAcquireJoyCon();

        bool isDashButtonDown = false;
        bool isDashButtonHeld = false;

        if (joycon != null)
        {
            // 横持ち時に「物理的に一番右側（進行方向）」にあるボタンを設定
            if (joycon.isLeft)
            {
                // Lジョイコンを横持ち（反時計回りに90度回転）すると、縦持ち時の「下（▼）」ボタン（DPAD_DOWN）が物理的な右側
                isDashButtonDown = joycon.GetButtonDown(Joycon.Button.DPAD_DOWN);
                isDashButtonHeld = joycon.GetButton(Joycon.Button.DPAD_DOWN);
            }
            else
            {
                // Rジョイコンを横持ち（時計回りに90度回転）すると、縦持ち時の「上（X）」ボタン（DPAD_UP）が物理的な右側
                isDashButtonDown = joycon.GetButtonDown(Joycon.Button.DPAD_UP);
                isDashButtonHeld = joycon.GetButton(Joycon.Button.DPAD_UP);
            }
        }
        // ジョイコンが無いときはデバッグ用にキーボードのEキーでも動くようにPC用フォールバックを用意
        else
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                isDashButtonDown = kb.eKey.wasPressedThisFrame;
                isDashButtonHeld = kb.eKey.isPressed;
            }
        }

        // 統合した判定フラグを使ってダッシュの計算を行う
        if (isDashButtonDown)
        {
            IsAttack = true;
            // ボタンを押した瞬間、一気に最大までジャンプ
            dashTiltAmount = Mathf.Max(dashTiltAmount, DashAmount);
        }

        if (isDashButtonHeld)
        {
            IsAttack = true;
            // 押しっぱなしの間は高いダッシュ値を維持する
            dashTiltAmount = Mathf.Max(dashTiltAmount, DashAmount);
        }
        else
        {
            // 離されたら、指定された秒数(dashDuration)をかけて滑らかに減衰して0に戻る
            float decaySpeed = 1f / Mathf.Max(DashDuration, MOVEMENT_VELOCITY_EPSILON);
            dashTiltAmount = Mathf.MoveTowards(dashTiltAmount, 0f, decaySpeed * Time.deltaTime);
        }

        if(dashTiltAmount <= 0)
        {
            IsAttack = false;
        }

        dashTiltAmount = Mathf.Clamp01(dashTiltAmount);
    }

    public void StartKnockback(float duation)
    {
        knockbackTimer = duation;
        knockedAirborne = true;   // 着地するまで入力による速度の上書きを止める
        currentSpeed = 0f;
        moveDirection = Vector3.zero;
    }
}
