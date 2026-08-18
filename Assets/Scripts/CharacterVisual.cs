using UnityEngine;

/// <summary>
/// キャラクターの見た目・表示情報を保持するScriptableObject。
/// デザイナーが差し替える想定。数値バランス調整はCharacterStatsで別管理する。
/// </summary>
[CreateAssetMenu(fileName = "New CharacterVisual", menuName = "AruMaruSmash/CharacterVisual")]
public class CharacterVisual : ScriptableObject
{
    [Header("基本情報")]
    public string characterName;
    public Sprite icon;

    [Header("見た目")]
    public Material bodyMaterial;
    public GameObject characterPrefab;
}
