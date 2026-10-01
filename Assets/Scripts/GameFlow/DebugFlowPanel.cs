using System;
using UnityEngine;

/// <summary>
/// 仮UI（デバッグ用）。実機のJoy-Conが無くても遷移を確認できる。
/// 本UI(黒田さん担当)ができたら、GameFlowBootstrap から外して削除する
/// </summary>
public class DebugFlowPanel : MonoBehaviour
{
    private const float PANEL_X = 10f;
    private const float PANEL_Y = 10f;
    private const float PANEL_WIDTH = 360f;
    private const float PANEL_HEIGHT = 560f;
    private const int PLAYER_COUNT = PlayerRegistry.MAX_PLAYERS;

    private bool m_visible = true;

    private void OnGUI()
    {
        GameFlowManager flow = GameFlowManager.Instance;
        if (flow == null)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(PANEL_X, PANEL_Y, PANEL_WIDTH, PANEL_HEIGHT), GUI.skin.box);
        if (GUILayout.Button(m_visible ? "仮UIを隠す" : "仮UIを表示"))
        {
            m_visible = !m_visible;
        }
        if (m_visible)
        {
            DrawHeader(flow);
            DrawPlayers(flow);
            DrawActions(flow);
        }
        GUILayout.EndArea();
    }

    private static void DrawHeader(GameFlowManager flow)
    {
        string leader = flow.Players.LeaderIndex >= 0 ? (flow.Players.LeaderIndex + 1) + "P" : "なし";
        GUILayout.Label("状態: " + flow.State + " / リーダー: " + leader
            + (flow.IsInputLocked ? " / (フェード中)" : string.Empty));
        if (flow.State == GameState.Waiting)
        {
            GUILayout.Label("待機画面: " + flow.WaitingMode);
        }
        bool solo = flow.Players.RequiredPlayers == 1;
        bool next = GUILayout.Toggle(solo, "1人でも進める(デバッグ)");
        if (next != solo)
        {
            flow.Players.RequiredPlayers = next ? 1 : PlayerRegistry.MAX_PLAYERS;
            flow.Players.NotifyChanged();
        }
    }

    private static void DrawPlayers(GameFlowManager flow)
    {
        for (int i = 0; i < PLAYER_COUNT; i++)
        {
            PlayerSlot p = flow.Players.slots[i];
            GUILayout.BeginHorizontal();
            GUILayout.Label((i + 1) + "P " + p.connection
                + (p.hasAssignedOk ? " 割当" : string.Empty)
                + (p.isCharacterConfirmed ? " 準備" : string.Empty)
                + (p.hasConfirmedReady ? " 確認" : string.Empty), GUILayout.Width(190f));
            if (!p.isJoined)
            {
                Button("参加", () => flow.Players.Join(p.index, p.index));
            }
            else if (p.connection == ConnectionState.Connected)
            {
                Button("切断", () => ControllerDisconnectHandler.Handle(flow, p.index));
            }
            else
            {
                Button("再接続", () => flow.Players.Reconnect(p.index, p.index));
            }
            GUILayout.EndHorizontal();
        }
    }

    private static void DrawActions(GameFlowManager flow)
    {
        GUILayout.Space(8f);
        int leader = flow.Players.LeaderIndex;
        switch (flow.State)
        {
            case GameState.Title:
                Req(flow, "スタート", GameEvent.Start, leader);
                Req(flow, "オプション", GameEvent.OpenOption, leader);
                Req(flow, "終了", GameEvent.Quit, leader);
                break;
            case GameState.Option:
                Req(flow, "操作説明", GameEvent.OpenGuide, leader);
                Req(flow, "設定", GameEvent.OpenSettings, leader);
                Req(flow, "タイトルへ戻る", GameEvent.ToTitle, leader);
                Req(flow, "ゲームを終了する", GameEvent.Quit, leader);
                break;
            case GameState.OperationGuide:
            case GameState.Settings:
                Req(flow, "戻る", GameEvent.Back, leader);
                break;
            default:
                DrawGameActions(flow, leader);
                break;
        }
    }

    private static void DrawGameActions(GameFlowManager flow, int leader)
    {
        switch (flow.State)
        {
            case GameState.ControllerAssignment:
                EachActive(flow, "割当OK", i => flow.ConfirmAssignment(i));
                Req(flow, "タイトルへ", GameEvent.ToTitle, leader);
                break;
            case GameState.CharacterSelect:
                EachActive(flow, "選ぶ(色=番号)", i => flow.SelectCharacter(i, i));
                EachActive(flow, "決定(準備完了)", i => flow.ConfirmCharacter(i));
                EachActive(flow, "取消", i => flow.CancelCharacter(i));
                Req(flow, "一つ戻る(割当へ)", GameEvent.BackToAssignment, leader);
                break;
            case GameState.Waiting:
                EachActive(flow, "操作説明を見た", i => flow.MarkGuideSeen(i));
                EachActive(flow, "OK確認", i => flow.ConfirmReady(i));
                Req(flow, "キャンセル(キャラ選択へ)", GameEvent.CancelToCharacterSelect, leader);
                break;
            case GameState.GameStartConfirm:
                Req(flow, "スタート", GameEvent.StartGame, leader);
                Req(flow, "戻る(待機画面へ)", GameEvent.Back, leader);
                break;
            case GameState.Playing:
                Req(flow, "ポーズ", GameEvent.Pause, leader);
                Req(flow, "試合終了", GameEvent.MatchEnd, leader);
                break;
            case GameState.Pause:
                Req(flow, "再開", GameEvent.Resume, leader);
                Req(flow, "待機画面へ", GameEvent.ToWaiting, leader);
                Req(flow, "タイトルへ", GameEvent.ToTitle, leader);
                break;
            case GameState.Result:
                Req(flow, "リトライ", GameEvent.Retry, leader);
                Req(flow, "タイトルへ", GameEvent.ToTitle, leader);
                break;
        }
    }

    // ---- ボタン補助 ----

    // IMGUIの描画中に状態を変えるとレイアウトが崩れるため、実行は次の Update に回す
    private static Action s_pending;

    private void Update()
    {
        if (s_pending != null)
        {
            Action action = s_pending;
            s_pending = null;
            action();
        }
    }

    private static void Button(string label, Action action)
    {
        if (GUILayout.Button(label))
        {
            s_pending = action;
        }
    }

    private static void Req(GameFlowManager flow, string label, GameEvent ev, int slot)
    {
        Button(label, () => flow.Request(ev, slot));
    }

    // 接続中のプレイヤーごとに横並びでボタンを出す
    private static void EachActive(GameFlowManager flow, string label, Action<int> action)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(120f));
        foreach (PlayerSlot p in flow.Players.ActivePlayers)
        {
            int index = p.index;
            Button((index + 1) + "P", () => action(index));
        }
        GUILayout.EndHorizontal();
    }
}
