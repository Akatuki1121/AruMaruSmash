/// <summary>
/// コントローラ切断時の共通処理（Input System / Joy-Con の両監視から呼ぶ）
/// </summary>
public static class ControllerDisconnectHandler
{
    public static void Handle(GameFlowManager flow, int slot)
    {
        bool was_leader = flow.Players.IsLeader(slot);
        flow.Players.Disconnect(slot);   // 内部でリーダー移譲

        // ゲーム中にリーダーが切れたら自動ポーズ。新リーダーが再開する
        bool should_pause = was_leader
            && flow.State == GameState.Playing
            && flow.Players.ActiveCount > 0;
        if (should_pause)
        {
            flow.Request(GameEvent.Pause, flow.Players.LeaderIndex);
        }
    }
}
