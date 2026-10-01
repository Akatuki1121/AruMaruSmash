using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 全プレイヤーの状態とリーダーを管理する（純C#。MonoBehaviourではない）
/// </summary>
public class PlayerRegistry
{
    public const int MAX_PLAYERS = 4;
    private const int NO_LEADER = -1;

    public readonly PlayerSlot[] slots;
    public int LeaderIndex { get; private set; } = NO_LEADER;

    /// <summary>新リーダーの index（いない場合は -1）</summary>
    public event Action<int> OnLeaderChanged;
    /// <summary>プレイヤー状態が変わった（UI更新・自動遷移判定用）</summary>
    public event Action OnPlayersChanged;

    public PlayerRegistry()
    {
        slots = new PlayerSlot[MAX_PLAYERS];
        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            slots[i] = new PlayerSlot(i);
        }
    }

    public IEnumerable<PlayerSlot> JoinedPlayers
    {
        get { return slots.Where(s => s.isJoined); }
    }

    public IEnumerable<PlayerSlot> ActivePlayers
    {
        get { return slots.Where(s => s.IsActive); }
    }

    public int ActiveCount
    {
        get { return ActivePlayers.Count(); }
    }

    // ---- リーダー ----

    public bool IsLeader(int slot_index)
    {
        return slot_index >= 0 && slot_index == LeaderIndex;
    }

    /// <summary>
    /// 参加中かつ接続中の最小番号にリーダー権限を移す。番号は詰めない
    /// </summary>
    public void TransferLeader()
    {
        PlayerSlot next = slots.FirstOrDefault(s => s.IsActive);
        SetLeader(next != null ? next.index : NO_LEADER);
    }

    private void SetLeader(int index)
    {
        if (LeaderIndex == index)
        {
            return;
        }
        LeaderIndex = index;
        if (OnLeaderChanged != null)
        {
            OnLeaderChanged(index);
        }
    }

    // ---- 参加・離脱 ----

    public PlayerSlot Join(int slot_index, int device_id)
    {
        PlayerSlot slot = slots[slot_index];
        slot.isJoined = true;
        slot.connection = ConnectionState.Connected;
        slot.deviceId = device_id;
        if (LeaderIndex == NO_LEADER)
        {
            TransferLeader();
        }
        NotifyChanged();
        return slot;
    }

    /// <summary>
    /// 自発的な離脱。スロットを空にする
    /// </summary>
    public void Leave(int slot_index)
    {
        slots[slot_index].ResetAll();
        if (slot_index == LeaderIndex)
        {
            TransferLeader();
        }
        NotifyChanged();
    }

    /// <summary>
    /// コントローラ切断。スロットは保持し、リーダーなら移譲する
    /// </summary>
    public void Disconnect(int slot_index)
    {
        PlayerSlot slot = slots[slot_index];
        if (!slot.isJoined)
        {
            return;
        }
        slot.connection = ConnectionState.Disconnected;
        slot.ResetWaiting();
        if (slot_index == LeaderIndex)
        {
            TransferLeader();
        }
        NotifyChanged();
    }

    /// <summary>
    /// 再接続。リーダー権限は戻さない（リーダー不在の場合のみ設定）
    /// </summary>
    public void Reconnect(int slot_index, int device_id)
    {
        PlayerSlot slot = slots[slot_index];
        if (!slot.isJoined)
        {
            return;
        }
        slot.connection = ConnectionState.Connected;
        slot.deviceId = device_id;
        slot.ResetWaiting();
        if (LeaderIndex == NO_LEADER)
        {
            TransferLeader();
        }
        NotifyChanged();
    }

    // ---- 一括判定（参加中かつ接続中の全員が対象。RequiredPlayers 人未満は false） ----

    /// <summary>
    /// 先へ進むのに必要な人数。仕様書は「4人準備完了」。
    /// 1台だけで動作確認したい時は、デバッグ用に 1 へ下げる
    /// </summary>
    public int RequiredPlayers { get; set; } = MAX_PLAYERS;

    public bool AllActive(Func<PlayerSlot, bool> condition)
    {
        List<PlayerSlot> list = ActivePlayers.ToList();
        return list.Count >= RequiredPlayers && list.All(condition);
    }

    public bool AllAssigned
    {
        get { return AllActive(p => p.hasAssignedOk); }
    }

    public bool AllSelected
    {
        get { return AllActive(p => p.isCharacterConfirmed); }
    }

    public bool AllConfirmed
    {
        get { return AllActive(p => p.hasConfirmedReady); }
    }

    // ---- リセット ----

    public void ResetAll()
    {
        foreach (PlayerSlot s in slots)
        {
            s.ResetAll();
        }
        LeaderIndex = NO_LEADER;
        if (OnLeaderChanged != null)
        {
            OnLeaderChanged(NO_LEADER);
        }
        NotifyChanged();
    }

    public void ResetWaitingFlags()
    {
        foreach (PlayerSlot s in slots)
        {
            s.ResetWaiting();
        }
        NotifyChanged();
    }

    /// <summary>キャラ選択の決定フラグと待機画面の確認を解除（選択キャラ自体は保持）</summary>
    public void ResetCharacterConfirm()
    {
        foreach (PlayerSlot s in slots)
        {
            s.isCharacterConfirmed = false;
            s.ResetWaiting();
        }
        NotifyChanged();
    }

    public void ResetAssignOk()
    {
        foreach (PlayerSlot s in slots)
        {
            s.hasAssignedOk = false;
        }
        NotifyChanged();
    }

    public void NotifyChanged()
    {
        if (OnPlayersChanged != null)
        {
            OnPlayersChanged();
        }
    }
}
