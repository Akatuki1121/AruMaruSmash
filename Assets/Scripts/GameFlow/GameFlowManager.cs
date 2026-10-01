using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 画面遷移の一元管理。GameState を変更できるのはこのクラスの Request だけ。
/// UI は OnStateChanged などを購読して表示を切り替えるだけにする
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    public GameState State { get; private set; } = GameState.Title;
    public WaitingScreenMode WaitingMode { get; private set; } = WaitingScreenMode.OperationGuide;
    public PlayerRegistry Players { get; } = new PlayerRegistry();

    /// <summary>
    /// フェード中などで true。入力による遷移リクエストを受け付けない
    /// </summary>
    public bool IsInputLocked { get; set; }

    /// <summary>(遷移前, 遷移後)</summary>
    public event Action<GameState, GameState> OnStateChanged;
    public event Action<WaitingScreenMode> OnWaitingModeChanged;
    /// <summary>新リーダーの index（いない場合は -1）</summary>
    public event Action<int> OnLeaderChanged;

    private const float TIME_SCALE_RUNNING = 1f;
    private const float TIME_SCALE_PAUSED = 0f;

    private class Transition
    {
        public GameState to;
        public bool leaderOnly;
        public Func<bool> guard;   // null なら条件なし
        public Action onEnter;     // 遷移時のリセット処理
    }

    private readonly Dictionary<(GameState, GameEvent), Transition> m_table
        = new Dictionary<(GameState, GameEvent), Transition>();
    private bool m_is_transitioning;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Players.OnLeaderChanged += HandleLeaderChanged;
        Players.OnPlayersChanged += EvaluateAuto;
        BuildTable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void HandleLeaderChanged(int leader_index)
    {
        if (OnLeaderChanged != null)
        {
            OnLeaderChanged(leader_index);
        }
    }

    private void Add(GameState from, GameEvent ev, GameState to, bool leader_only,
                     Func<bool> guard = null, Action on_enter = null)
    {
        m_table[(from, ev)] = new Transition
        {
            to = to,
            leaderOnly = leader_only,
            guard = guard,
            onEnter = on_enter
        };
    }

    private void BuildTable()
    {
        // タイトル周り（T_1 / O_1）
        Add(GameState.Title, GameEvent.Start, GameState.ControllerAssignment, true, null, Players.ResetAssignOk);
        Add(GameState.Title, GameEvent.OpenOption, GameState.Option, true);
        Add(GameState.Title, GameEvent.Quit, GameState.Quit, true);
        Add(GameState.Option, GameEvent.OpenGuide, GameState.OperationGuide, true);
        Add(GameState.Option, GameEvent.OpenSettings, GameState.Settings, true);
        Add(GameState.OperationGuide, GameEvent.Back, GameState.Option, true);
        Add(GameState.Settings, GameEvent.Back, GameState.Option, true);
        Add(GameState.Option, GameEvent.ToTitle, GameState.Title, true);
        Add(GameState.Option, GameEvent.Quit, GameState.Quit, true);

        // ロビー（S_1 + 画面遷移図）。キャラ選択 → 待機 → スタート確認 → ゲーム
        Add(GameState.ControllerAssignment, GameEvent.AssignmentAllOk, GameState.CharacterSelect, false,
            () => Players.AllAssigned);
        Add(GameState.ControllerAssignment, GameEvent.ToTitle, GameState.Title, true, null, ExitToTitle);
        Add(GameState.CharacterSelect, GameEvent.SelectionAllOk, GameState.Waiting, false,
            () => Players.AllSelected, EnterWaitingFresh);
        Add(GameState.CharacterSelect, GameEvent.BackToAssignment, GameState.ControllerAssignment, true,
            null, BackToAssignment);
        Add(GameState.Waiting, GameEvent.WaitingAllConfirmed, GameState.GameStartConfirm, false,
            () => Players.AllConfirmed);
        Add(GameState.Waiting, GameEvent.CancelToCharacterSelect, GameState.CharacterSelect, true,
            null, Players.ResetCharacterConfirm);
        Add(GameState.GameStartConfirm, GameEvent.StartGame, GameState.Playing, true);
        Add(GameState.GameStartConfirm, GameEvent.Back, GameState.Waiting, true, null, EnterWaitingFresh);

        // ゲーム中
        Add(GameState.Playing, GameEvent.Pause, GameState.Pause, true, null, PauseTime);
        Add(GameState.Playing, GameEvent.MatchEnd, GameState.Result, false);
        Add(GameState.Pause, GameEvent.Resume, GameState.Playing, true, null, ResumeTime);
        Add(GameState.Pause, GameEvent.ToTitle, GameState.Title, true, null, ExitToTitle);
        Add(GameState.Pause, GameEvent.ToWaiting, GameState.Waiting, true, null, ExitToWaitingRetry);
        Add(GameState.Result, GameEvent.Retry, GameState.Waiting, true, null, ExitToWaitingRetry);
        Add(GameState.Result, GameEvent.ToTitle, GameState.Title, true, null, ExitToTitle);
    }

    // ===== 唯一の入口 =====

    /// <summary>
    /// 遷移リクエスト。slot は操作したプレイヤー番号（0-3）、システム発は -1
    /// </summary>
    public bool Request(GameEvent ev, int slot = -1)
    {
        if (ev == GameEvent.AllPlayersLeft)
        {
            return HandleAllPlayersLeft();
        }
        if (IsInputLocked)
        {
            return false;
        }

        Transition t;
        if (!m_table.TryGetValue((State, ev), out t))
        {
            return false;
        }
        if (t.leaderOnly && !CanOperateAsLeader(slot))
        {
            return false;
        }
        if (t.guard != null && !t.guard())
        {
            return false;
        }

        m_is_transitioning = true;
        if (t.onEnter != null)
        {
            t.onEnter();
        }
        Change(t.to);
        m_is_transitioning = false;

        EvaluateAuto();
        return true;
    }

    /// <summary>
    /// リーダー権限で操作できるか。プレイヤー登録前のタイトル周りは誰でも操作できる
    /// </summary>
    public bool CanOperateAsLeader(int slot)
    {
        if (IsTitleMenuState(State))
        {
            return true;
        }
        return Players.IsLeader(slot);
    }

    private static bool IsTitleMenuState(GameState state)
    {
        return state == GameState.Title
            || state == GameState.Option
            || state == GameState.OperationGuide
            || state == GameState.Settings;
    }

    private static bool IsLobbyOrLaterState(GameState state)
    {
        return state == GameState.CharacterSelect
            || state == GameState.Waiting
            || state == GameState.GameStartConfirm
            || state == GameState.Playing
            || state == GameState.Pause
            || state == GameState.Result;
    }

    private void Change(GameState to)
    {
        GameState from = State;
        State = to;
        if (OnStateChanged != null)
        {
            OnStateChanged(from, to);
        }
    }

    private bool HandleAllPlayersLeft()
    {
        if (!IsLobbyOrLaterState(State))
        {
            return false;
        }
        m_is_transitioning = true;
        ExitToTitle();
        Change(GameState.Title);
        m_is_transitioning = false;
        return true;
    }

    // ===== 自動遷移（プレイヤー状態が変わるたびに評価） =====

    public void EvaluateAuto()
    {
        if (m_is_transitioning)
        {
            return;
        }

        if (IsLobbyOrLaterState(State) && Players.ActiveCount == 0)
        {
            Request(GameEvent.AllPlayersLeft);
            return;
        }

        switch (State)
        {
            case GameState.ControllerAssignment:
                Request(GameEvent.AssignmentAllOk);
                break;
            case GameState.CharacterSelect:
                Request(GameEvent.SelectionAllOk);
                break;
            case GameState.Waiting:
                Request(GameEvent.WaitingAllConfirmed);
                break;
        }
    }

    // ===== 待機画面 =====

    public void SetWaitingMode(WaitingScreenMode mode)
    {
        WaitingMode = mode;
        if (OnWaitingModeChanged != null)
        {
            OnWaitingModeChanged(mode);
        }
    }

    /// <summary>
    /// 操作説明を見終えたプレイヤーごとに呼ぶ。全員済みなら OK確認 へスライド
    /// </summary>
    public void MarkGuideSeen(int slot)
    {
        if (State != GameState.Waiting)
        {
            return;
        }
        Players.slots[slot].hasSeenGuide = true;
        if (Players.AllActive(p => p.hasSeenGuide))
        {
            SetWaitingMode(WaitingScreenMode.ReadyCheck);
        }
        Players.NotifyChanged();
    }

    public void ConfirmReady(int slot)
    {
        if (State != GameState.Waiting || WaitingMode != WaitingScreenMode.ReadyCheck)
        {
            return;
        }
        Players.slots[slot].hasConfirmedReady = true;
        Players.NotifyChanged();
    }

    // ===== ロビーでのプレイヤー操作（UI/入力層から呼ぶ） =====

    public void ConfirmAssignment(int slot)
    {
        if (State != GameState.ControllerAssignment)
        {
            return;
        }
        Players.slots[slot].hasAssignedOk = true;
        Players.NotifyChanged();
    }

    /// <summary>
    /// キャラクター(色)を選ぶ。選び直すと準備完了は解除される
    /// </summary>
    public void SelectCharacter(int slot, int character_id)
    {
        if (State != GameState.CharacterSelect)
        {
            return;
        }
        PlayerSlot s = Players.slots[slot];
        s.characterId = character_id;
        s.isCharacterConfirmed = false;
        Players.NotifyChanged();
    }

    /// <summary>
    /// 決定ボタン。決定と同時に準備完了状態になる（仕様書 S_1）
    /// </summary>
    public void ConfirmCharacter(int slot)
    {
        if (State != GameState.CharacterSelect || Players.slots[slot].characterId < 0)
        {
            return;
        }
        Players.slots[slot].isCharacterConfirmed = true;
        Players.NotifyChanged();
    }

    /// <summary>
    /// 準備完了のキャンセル（仕様書 S_1: ボタンでキャンセル）
    /// </summary>
    public void CancelCharacter(int slot)
    {
        if (State != GameState.CharacterSelect)
        {
            return;
        }
        Players.slots[slot].isCharacterConfirmed = false;
        Players.NotifyChanged();
    }

    // ===== 遷移時のリセット処理 =====

    private void EnterWaitingFresh()
    {
        Time.timeScale = TIME_SCALE_RUNNING;
        Players.ResetWaitingFlags();
        SetWaitingMode(WaitingScreenMode.OperationGuide);
    }

    /// <summary>
    /// リトライ扱い。操作説明は確認済みとして OK確認 から始める
    /// </summary>
    private void ExitToWaitingRetry()
    {
        Time.timeScale = TIME_SCALE_RUNNING;
        Players.ResetWaitingFlags();
        foreach (PlayerSlot p in Players.JoinedPlayers)
        {
            p.hasSeenGuide = true;
        }
        SetWaitingMode(WaitingScreenMode.ReadyCheck);
    }

    private void BackToAssignment()
    {
        Players.ResetAssignOk();
        Players.ResetCharacterConfirm();
    }

    private void ExitToTitle()
    {
        Time.timeScale = TIME_SCALE_RUNNING;
        Players.ResetAll();
    }

    private void PauseTime()
    {
        Time.timeScale = TIME_SCALE_PAUSED;
    }

    private void ResumeTime()
    {
        Time.timeScale = TIME_SCALE_RUNNING;
    }
}
