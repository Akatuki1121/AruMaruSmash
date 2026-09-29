using System;

/// <summary>
/// 1人分のプレイヤー状態（0..3 = 1P..4P）。UIや入力は持たない
/// </summary>
[Serializable]
public class PlayerSlot
{
    public int index;                        // 0..3（表示は index + 1 P）
    public bool isJoined;                    // 参加状態
    public ConnectionState connection;       // コントローラ接続状態
    public int deviceId = -1;                // 割り当てられたデバイス
    public int characterId = -1;             // キャラクター選択状態
    public bool isCharacterConfirmed;        // キャラクター選択完了
    public bool hasAssignedOk;               // コントローラ割り当てOK
    public bool hasSeenGuide;                // 操作説明の確認
    public bool hasConfirmedReady;           // 待機画面のOK確認

    public bool IsActive
    {
        get { return isJoined && connection == ConnectionState.Connected; }
    }

    public PlayerSlot(int slot_index)
    {
        index = slot_index;
    }

    public void ResetAll()
    {
        isJoined = false;
        connection = ConnectionState.Empty;
        deviceId = -1;
        hasAssignedOk = false;
        ResetCharacter();
        ResetWaiting();
    }

    public void ResetCharacter()
    {
        characterId = -1;
        isCharacterConfirmed = false;
    }

    public void ResetWaiting()
    {
        hasSeenGuide = false;
        hasConfirmedReady = false;
    }
}
