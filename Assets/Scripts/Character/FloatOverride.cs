using System;

/// <summary>
/// 「CharacterStatsのデフォルト値を使う」か「このコンポーネントのInspector値で上書きする」かを選べるfloat。
/// useOverrideがfalseならデフォルト値、trueならvalueが使われる。
/// </summary>
[Serializable]
public struct FloatOverride
{
    public bool useOverride;
    public float value;

    public float Resolve(float defaultValue)
    {
        return useOverride ? value : defaultValue;
    }
}
