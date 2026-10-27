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
    [Tooltip("【未使用】チャージダッシュ導入前の旧設定。ChargeDashControllerは参照しない")]
    public float dashAmount = 1f;

    [Tooltip("ダッシュの持続時間（秒）")]
    public float dashDuration = 1f;

    [Tooltip("【未使用】チャージダッシュ導入前の旧設定。ChargeDashControllerは参照しない")]
    public float dashSpeedMultiplier = 1f;

    [Header("チャージダッシュ（ダッシュの持続時間は上のdashDurationを使う）")]
    [Tooltip("最大チャージに必要な長押し時間（秒）")]
    [Min(0.01f)]
    public float chargeMaxTime = 2.5f;

    [Tooltip("チャージ中の最大移動速度の倍率（0〜1）。小さいほど遅い。進行方向を決められる程度に残す")]
    [Range(0f, 1f)]
    public float chargeMoveSpeedMultiplier = 0.3f;

    [Tooltip("チャージ開始後、速度がチャージ中の上限まで落ちるときの減速の強さ（速度/秒）")]
    [Min(0f)]
    public float chargeDeceleration = 40f;

    [Tooltip("チャージ0秒（ちょい押し）のダッシュ速度。maxSpeedに対する倍率")]
    [Min(0f)]
    public float dashMinSpeedMultiplier = 1.5f;

    [Tooltip("最大チャージのダッシュ速度。maxSpeedに対する倍率")]
    [Min(0f)]
    public float dashMaxSpeedMultiplier = 3f;

    [Tooltip("最大チャージのダッシュで衝突したときのヒットストップ時間（秒・実時間）")]
    [Min(0f)]
    public float hitStopDuration = 0.05f;

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
