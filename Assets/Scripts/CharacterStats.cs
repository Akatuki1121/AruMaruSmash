using UnityEngine;

/// <summary>
/// キャラクターのゲームプレイ数値（移動・ノックバック）を保持するScriptableObject。
/// プログラマー/企画がバランス調整用に使用する。
/// 見た目（マテリアル・プレハブ等）はCharacterVisualで別管理する。
/// </summary>
[CreateAssetMenu(fileName = "New CharacterStats", menuName = "AruMaruSmash/CharacterStats")]
public class CharacterStats : ScriptableObject
{
    [Header("移動")]
    public float maxSpeed = 8f;
    public float acceleration = 16f;
    public float brakeSpeed = 40f;
    public float rotationSpeed = 120f;   // TPS用。TopDownでは未使用でも可

    [Header("ノックバック")]
    [Tooltip("吹っ飛び・押し負け直後、入力での移動を完全に無効化する時間（秒）")]
    public float knockbackLockDuration = 0.25f;

    [Tooltip("ロック終了後、空中にいる間の入力の効き具合（0=入力無効、1=通常と同じ）")]
    [Range(0f, 1f)]
    public float airControlMultiplier = 0.3f;

    [Tooltip("この値以上のノックバック量が残っている間だけ「空中制御弱体化」とみなす")]
    public float airControlKnockbackThreshold = 0.5f;

    [Tooltip("ノックバック速度が時間経過でどれだけ早く0に近づくか")]
    public float knockbackDecaySpeed = 4f;
}
