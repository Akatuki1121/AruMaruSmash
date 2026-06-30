using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// プレイヤーの移動入力を集約するクラス。
///
/// 入力ソースは2種類あり、排他的に使う想定（同時押しは想定しない）：
/// - Unity Input System経由（キーボード・ゲームパッド）→ OnMove(InputValue)
/// - Joy-Con実機のスティック（JoyconLib経由でポーリング取得。Input Systemには
///   <Gamepad>として認識されないため、OnMoveでは検知できない）
///
/// 毎フレーム、Input System側の入力ベクトルとJoy-Conスティックの入力ベクトルを比較し、
/// 入力量（sqrMagnitude）が大きい方を「採用された入力」として以降のGetInputXX系が参照する。
/// デッドゾーンも採用元に応じて切り替える（Joy-Conはキャリブレーションのずれで
/// ゼロ点が微妙にずれることがあるため、専用の値を持つ）。
/// </summary>
public class InputManager_Test : MonoBehaviour
{
    // 入力のデッドゾーン（Input System側：キーボード・ゲームパッド用）
    private const float DEFAULT_DEADZONE = 0.3f;

    [HideInInspector] public float deadzone = DEFAULT_DEADZONE;
    [HideInInspector] public Rigidbody rb;

    [Header("Joy-Con設定")]
    [Tooltip("使用するJoy-Conのインデックス（JoyconManager.Instance.jの何番目か）")]
    public int joyconIndex = 0;

    [Tooltip("Joy-Conスティック専用のデッドゾーン。キャリブレーションのずれでゼロ点が安定しないことがあるため、Input System側のdeadzoneとは別に持つ")]
    public float joyconDeadzone = 0.3f;

    // Input System(OnMove)由来の入力ベクトル
    private Vector2 actionMoveInput = Vector2.zero;

    // Joy-Conスティック由来の入力ベクトル（毎フレームポーリングして更新）
    private Vector2 joyconMoveInput = Vector2.zero;

    // 今フレーム実際に採用されている入力ベクトルとデッドゾーン
    // （actionMoveInputとjoyconMoveInputのうち、入力量が大きい方を都度選ぶ）
    private Vector2 activeMoveInput = Vector2.zero;
    private float activeDeadzone = DEFAULT_DEADZONE;

    // 接続されているJoy-Con本体への参照。見つからない場合はnullのままで、
    // Joy-Con未接続環境（キーボードのみ等）でも問題なく動作する。
    private Joycon joycon;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    protected virtual void Update()
    {
        PollJoyconStick();
        SelectActiveInput();
    }

    // JoyconManagerからJoy-Con本体を取得する。
    // JoyconManagerがシーンに存在しない、またはまだ初期化されていない、
    // 指定インデックスのJoy-Conが接続されていない場合は何もしない（=Joy-Con入力は常にゼロ扱い）。
    private void TryAcquireJoycon()
    {
        if (joycon != null) return;
        if (JoyconManager.Instance == null) return;
        if (JoyconManager.Instance.j == null) return;
        if (joyconIndex < 0 || joyconIndex >= JoyconManager.Instance.j.Count) return;

        joycon = JoyconManager.Instance.j[joyconIndex];
    }

    // Joy-Con実機のスティック値をポーリングしてjoyconMoveInputに反映する。
    private void PollJoyconStick()
    {
        TryAcquireJoycon();

        if (joycon == null)
        {
            joyconMoveInput = Vector2.zero;
            return;
        }

        float[] stick = joycon.GetStick();
        float x = stick[0];
        float y = stick[1];

        // Joy-Con L/Rでスティック軸の符号が異なるため補正する
        // （実機計測メモ：L→X:左<0<右(標準通り)/Y:前<0<後(反転が必要) R→X:右<0<左(反転が必要)/Y:後<0<前(標準通り)）
        if (joycon.isLeft)
        {
            y = -y;
        }
        else
        {
            x = -x;
        }

        joyconMoveInput = new Vector2(x, y);
    }

    // Input System側とJoy-Con側、入力量が大きい方を採用する。
    private void SelectActiveInput()
    {
        if (joyconMoveInput.sqrMagnitude > actionMoveInput.sqrMagnitude)
        {
            activeMoveInput = joyconMoveInput;
            activeDeadzone = joyconDeadzone;
        }
        else
        {
            activeMoveInput = actionMoveInput;
            activeDeadzone = deadzone;
        }
    }

    // UnityのPlayer Inputから自動で呼び出される関数
    public void OnMove(InputValue value)
    {
        actionMoveInput = value.Get<Vector2>();

        //Debug.Log($"{gameObject.name} に入力が届いています！ 値: {actionMoveInput}");
    }

    //----- 継続入力 -----//
    public bool GetInputUp() => activeMoveInput.y > activeDeadzone;
    public bool GetInputDown() => activeMoveInput.y < -activeDeadzone;
    public bool GetInputRight() => activeMoveInput.x > activeDeadzone;
    public bool GetInputLeft() => activeMoveInput.x < -activeDeadzone;

    //----- 単発入力 -----//
    public bool GetInputUpNow() => activeMoveInput.y > activeDeadzone;
    public bool GetInputDownNow() => activeMoveInput.y < -activeDeadzone;
    public bool GetInputRightNow() => activeMoveInput.x > activeDeadzone;
    public bool GetInputLeftNow() => activeMoveInput.x < -activeDeadzone;

    public bool GetDetermination() => false;
}
