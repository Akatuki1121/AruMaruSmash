using UnityEngine;

/// <summary>
/// キャラクターのゲームプレイ数値（移動・ダッシュ・ノックバック・衝突）を保持するScriptableObject。
/// プログラマー/企画がバランス調整用に使用する。
/// 見た目（マテリアル・プレハブ等）はCharacterVisualで別管理する。
///
/// ここの値は「デフォルト値」として扱う。数値は元のPrefab A/Bの保存値に合わせてある（空中制御・吹っ飛び中の重力・壁の抑制は新規の調整値）。
/// 各コンポーネント側のInspectorで「上書き」にチェックを入れた項目だけ、コンポーネント側の値が優先される（FloatOverride）。
/// </summary>
[CreateAssetMenu(fileName = "New CharacterStats", menuName = "AruMaruSmash/CharacterStats")]
public class CharacterStats : ScriptableObject
{
    [Header("移動")]
    public float maxSpeed = 40f;
    public float acceleration = 4f;
    public float brakeSpeed = 10f;
    public float rotationSpeed = 120f;   // TPS用。TopDownでは未使用でも可

    [Header("ダッシュ")]
    [Tooltip("ダッシュ量をこの値までジャンプ（基本1.0）")]
    public float dashAmount = 1f;

    [Tooltip("ダッシュの持続時間（秒）")]
    public float dashDuration = 1f;

    [Tooltip("ダッシュ最大時、スピードを何倍ブーストするか（1.0なら2倍速）")]
    public float dashSpeedMultiplier = 1f;

    [Header("ノックバック")]
    [Tooltip("吹っ飛び・押し負け直後、入力での移動を完全に無効化する時間（秒）")]
    public float knockbackLockDuration = 0.25f;

    [Tooltip("ロック終了後、空中にいる間の入力の効き具合（0=入力無効、1=通常と同じ）")]
    [Range(0f, 1f)]
    public float airControlMultiplier = 0f;

    [Tooltip("この値以上のノックバック量が残っている間だけ「空中制御弱体化」とみなす")]
    public float airControlKnockbackThreshold = 0.5f;

    [Tooltip("ノックバック速度が時間経過でどれだけ早く0に近づくか")]
    public float knockbackDecaySpeed = 4f;

    [Tooltip("吹っ飛ばされて着地するまでの間、重力を何倍にするか（1=通常の重力。大きいほど速く落ちる）")]
    [Min(1f)]
    public float knockbackGravityMultiplier = 3f;

    [Header("衝突反応（プレイヤー同士）")]
    [Tooltip("0=完全に物理的な弾性衝突 / 1=強い方は法線速度をほぼ変えず弱い方だけが強く飛ぶ")]
    [Range(0f, 1f)]
    public float asymmetryFactor = 0.85f;

    [Tooltip("反発の勢い全体に対する倍率")]
    public float bounceForceMultiplier = 1f;

    [Tooltip("衝突の相対速度1あたりに加える上方向初速")]
    public float upwardForcePerSpeed = 0.6f;

    [Tooltip("上方向初速の最小値")]
    public float minUpwardForce = 1.5f;

    [Tooltip("上方向初速の最大値")]
    public float maxUpwardForce = 8f;

    [Tooltip("同じ相手と衝突した後、この時間内は同じ相手との衝突を無視する（秒）")]
    public float sameTargetCooldown = 0.4f;

    [Header("壁との衝突")]
    [Tooltip("接触面の法線のY成分の絶対値がこの値未満なら「壁」とみなす（0=完全に垂直な面のみ、大きいほど傾いた面も壁扱い）")]
    [Range(0f, 1f)]
    public float wallNormalYThreshold = 0.5f;

    [Tooltip("壁に接触している間、上向きの速度をこの値までに制限する（0=壁に当たっても上へは飛ばない）")]
    [Min(0f)]
    public float wallMaxUpwardSpeed = 0f;

    private static CharacterStats fallback;

    /// <summary>
    /// statsが未設定のときに使う、コード上の初期値（このクラスのフィールド初期値そのもの）。
    /// </summary>
    public static CharacterStats Fallback
    {
        get
        {
            if (fallback == null)
            {
                fallback = CreateInstance<CharacterStats>();
                fallback.hideFlags = HideFlags.HideAndDontSave;
            }
            return fallback;
        }
    }
}
