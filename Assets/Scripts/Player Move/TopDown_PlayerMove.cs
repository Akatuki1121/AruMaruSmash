using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 見下ろし視点のプレイヤー移動を制御するクラス。
/// JoyconDemo.csから一部を抜粋しコピペしたもの。
/// </summary>

public class TopDown_PlayerMove : MonoBehaviour
{
    Joycon_accel_Receiver JoyAccelRec;
    public Vector3 JoyAccel;

    [Header("速度設定")]
    public float maxSpeed = 10f;       // 最大速度
    public float currentSpeed = 0f;    // 現在の速度
    public float brakeSpeed = 5f;     // 減速（ブレーキ）速度
    public float acceleration = 2f;   // 加速速度

    private Vector3 moveDirection = Vector3.zero;
    private const float MOVEMENT_VELOCITY_EPSILON = 0.0001f;

    public Rigidbody rb;

    // 現在の傾き量（0〜1）。これがそのまま速度倍率の元になる
    public float tiltAmount = 0f;

    [Header("急加速")]
    [Tooltip("ダッシュ量をこの値までジャンプ（基本1.0）")]
    public float dashAmount = 1.0f;

    [Tooltip("ダッシュの持続時間（秒）")]
    public float dashDuration = 1.0f;

    [Tooltip("ダッシュ最大字、スピードを何倍ブーストするか（1.0なら2倍速）")]
    public float dashSpeedMultiplier = 1.0f;

    // 現在のダッシュブースト
    [SerializeField] private float dashTiltAmount = 0f;

    [Header("Joy-Con設定")]
    public int joyconIndex = 0; // 使用するJoy-Conのインデックス（0または1）
    private Joycon joycon; // 接続されているJoy-Con本体への参照

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //if (JoyAccelRec == null)
        //{
        //    JoyAccelRec = GetComponent<Joycon_accel_Receiver>();
        //}
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        // Joy-Conの接続を試みる
        TryAcquireJoyCon();
    }

    // Update is called once per frame
    void Update()
    {
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

        bool hasInput = (GetTiltX() > 0.2f || GetTiltX() < -0.2f || GetTiltY() > 0.2f || GetTiltY() < -0.2f);

        Vector3 currentPhysicalDir = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).normalized;

        if (hasInput)
        {
            // 前回の移動方向と現在の移動方向が逆向きの場合、減速する
            if (currentSpeed > 0.1f && Vector3.Dot(oldDir, moveDirection) < -0.1f && Vector3.Dot(oldDir, moveDirection) < -0.1f)
            {
                currentSpeed -= brakeSpeed * Time.deltaTime;
                currentSpeed = Mathf.Max(currentSpeed, 0f);

                moveDirection = oldDir; // 逆方向入力時は前回の方向を維持する
            }
            else
            {
                float speedBoostFactor = 1.0f + (dashTiltAmount * dashSpeedMultiplier);
                float targetMaxSpeed = maxSpeed * tiltAmount * speedBoostFactor;

                currentSpeed += targetMaxSpeed * acceleration * Time.deltaTime;
                currentSpeed = Mathf.Min(currentSpeed, targetMaxSpeed);
            }
        }
        else
        {
            // 入力が無いときは、現在の速度が0より大きい場合にのみ加速する
            if (currentSpeed > 0.01f)
            {
                currentSpeed -= brakeSpeed * Time.deltaTime;
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
        PlayerMove(currentSpeed);
    }

    public void PlayerMove(float speed)
    {
        if (moveDirection.sqrMagnitude > 0 && speed > 0.01f)
        {
            // 入力方向をワールド座標に変換
            Vector3 localMoveDir = transform.rotation * moveDirection;
            Vector3 targetVelocity = localMoveDir * speed;

            // 足元にレイを飛ばして地面の傾きを検知
            Ray ray = new Ray(rb.position + Vector3.up * 0.2f, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, 0.6f))
            {
                // 地面の斜面に沿うように速度ベクトルを曲げる
                targetVelocity = Vector3.ProjectOnPlane(targetVelocity, hit.normal);
            }

            // XZ方向の速度を設定（Y方向の速度はそのままにする）
            rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
        }
        else
        {
            // 入力がない場合、XZ方向の速度をゼロにする（Y方向の速度はそのままにする）
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
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

        Vector3 inputVector = new Vector3(moveX, 0f, moveZ);

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
            // ボタンを押した瞬間、一気に最大までジャンプ
            dashTiltAmount = Mathf.Max(dashTiltAmount, dashAmount);
        }

        if (isDashButtonHeld)
        {
            // 押しっぱなしの間は高いダッシュ値を維持する
            dashTiltAmount = Mathf.Max(dashTiltAmount, dashAmount);
        }
        else
        {
            // 離されたら、指定された秒数(dashDuration)をかけて滑らかに減衰して0に戻る
            float decaySpeed = 1f / Mathf.Max(dashDuration, MOVEMENT_VELOCITY_EPSILON);
            dashTiltAmount = Mathf.MoveTowards(dashTiltAmount, 0f, decaySpeed * Time.deltaTime);
        }

        dashTiltAmount = Mathf.Clamp01(dashTiltAmount);
    }
}
