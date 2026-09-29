/// <summary>
/// ゲーム全体の画面状態。GameFlowManager だけが変更する
/// </summary>
public enum GameState
{
    Title,
    Option,
    OperationGuide,
    Settings,
    ControllerAssignment,
    CharacterSelect,
    Waiting,
    GameStartConfirm,
    Playing,
    Pause,
    Result,
    Quit
}

/// <summary>
/// 待機画面内のスライド状態
/// </summary>
public enum WaitingScreenMode
{
    OperationGuide,
    ReadyCheck
}

/// <summary>
/// コントローラの接続状態
/// </summary>
public enum ConnectionState
{
    Empty,
    Connected,
    Disconnected
}

/// <summary>
/// 入力やシステムからの「やりたいこと」。遷移のトリガー
/// </summary>
public enum GameEvent
{
    Start,
    OpenOption,
    OpenGuide,
    OpenSettings,
    Back,
    ToTitle,
    Quit,
    AssignmentAllOk,
    SelectionAllOk,
    CancelToCharacterSelect,
    BackToAssignment,
    WaitingAllConfirmed,
    StartGame,
    Pause,
    Resume,
    ToWaiting,
    MatchEnd,
    Retry,
    AllPlayersLeft
}
