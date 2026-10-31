using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// リザルト画面。勝敗の表示と、Retry / ToTitle の選択を担当する。
/// 勝敗は MatchStats（落とした回数 - 落ちた回数）で決まり、同スコアは引き分け。
/// </summary>
public class ResultUIManager : BaseUIManager
{
    private const int ITEM_RETRY = 0;
    private const int ITEM_TO_TITLE = 1;
    private const int ITEM_COUNT = 2;

    [Header("表示（未設定なら表示しない）")]
    [Tooltip("「引き分け」「1P の勝ち」などの見出し")]
    [SerializeField] private TMP_Text headlineText;

    [Tooltip("プレイヤーごとの成績。要素番号 = プレイヤー番号（0 = 1P）")]
    [SerializeField] private TMP_Text[] playerTexts;

    [Tooltip("表彰台(未設定なら、キャラクターは出さずテキストだけ)")]
    [SerializeField] private PodiumDisplay podium;

    private ButtonManager button;
    private int selected = ITEM_RETRY;
    private UIInputDirection previousDirection = UIInputDirection.None;

    private void Start()
    {
        button = FindAnyObjectByType<ButtonManager>();
        ShowResult();
    }

    private void Update()
    {
        if (button == null) return;
        if (JoyconManager.Instance == null || JoyconManager.Instance.j == null) return;

        UIInputDirection direction = GetStick();

        // スティックを倒した瞬間だけ選択位置を変更（縦並び・横並びのどちらでも動く）
        if (direction != UIInputDirection.None &&
            previousDirection == UIInputDirection.None)
        {
            switch (direction)
            {
                case UIInputDirection.Up:
                case UIInputDirection.Left:
                    selected--;
                    break;

                case UIInputDirection.Down:
                case UIInputDirection.Right:
                    selected++;
                    break;
            }
            selected = Mathf.Clamp(selected, 0, ITEM_COUNT - 1);
        }

        previousDirection = direction;

        if (!GetButtonDown()) return;

        if (selected == ITEM_RETRY)
        {
            button.OnRetryClicked();
        }
        else if (selected == ITEM_TO_TITLE)
        {
            button.OnBackTitleClicked();
        }
    }

    // ===== 勝敗表示 =====

    private void ShowResult()
    {
        List<MatchStats.Entry> ranking = MatchStats.Evaluate(GetParticipants());
        bool draw = MatchStats.IsDraw(ranking);

        if (headlineText != null)
        {
            headlineText.text = BuildHeadline(ranking, draw);
        }

        foreach (MatchStats.Entry e in ranking)
        {
            Debug.Log($"{e.rank}位: {e.playerIndex + 1}P 落とした{e.kills} / 落ちた{e.falls} (スコア {e.score})");

            if (playerTexts == null || e.playerIndex >= playerTexts.Length) continue;
            if (playerTexts[e.playerIndex] == null) continue;

            playerTexts[e.playerIndex].text =
                $"{e.playerIndex + 1}P  {e.rank}位\n落とした {e.kills}\n落ちた {e.falls}";
        }

        if (podium != null)
        {
            podium.Show(ranking, playerTexts);
        }
    }

    private static string BuildHeadline(List<MatchStats.Entry> ranking, bool draw)
    {
        if (ranking.Count == 0) return "試合の記録がありません";

        List<string> top = new List<string>();
        foreach (MatchStats.Entry e in ranking)
        {
            if (e.rank == 1) top.Add($"{e.playerIndex + 1}P");
        }

        string names = string.Join("・", top);
        return draw ? $"引き分け（{names}）" : $"{names} の勝ち！";
    }

    /// <summary>
    /// 試合に参加したプレイヤー番号。ロビーで参加したスロットを使い、
    /// Result シーン単体再生などでロビー情報が無い時は、記録のある番号で代用する
    /// </summary>
    private static List<int> GetParticipants()
    {
        List<int> ids = new List<int>();

        GameFlowManager flow = GameFlowManager.Instance;
        if (flow != null)
        {
            foreach (PlayerSlot slot in flow.Players.JoinedPlayers)
            {
                ids.Add(slot.index);
            }
        }

        if (ids.Count == 0)
        {
            for (int i = 0; i < MatchStats.MAX_PLAYERS; i++)
            {
                if (MatchStats.GetKills(i) + MatchStats.GetFalls(i) > 0) ids.Add(i);
            }
        }

        // 参加者も記録も無い(Result シーン単体再生など)時は、全員0点 = 全員引き分けとして出す
        if (ids.Count == 0)
        {
            for (int i = 0; i < MatchStats.MAX_PLAYERS; i++) ids.Add(i);
        }
        return ids;
    }
}
