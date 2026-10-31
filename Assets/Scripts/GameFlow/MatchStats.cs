using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1試合分の「落ちた回数」「落とした回数」を数える静的クラス。
/// Game シーンで集計し、Result シーンで読む（static なのでシーンをまたいで残る）。
/// 試合開始時（RespawnManager.Start）に Reset する。
///
/// 勝敗ルール: スコア = 落とした回数 - 落ちた回数。スコアが高いほど上位。
/// 同スコアは同順位（引き分けあり。タイブレークはしない）。
/// ルールを変えたい時は GetScore だけ直せばよい
/// </summary>
public static class MatchStats
{
    public const int MAX_PLAYERS = 4;

    /// <summary>最後に触れられてからこの秒数以内に落ちたら、触れた相手が「落とした」扱い</summary>
    public const float KILL_CREDIT_SECONDS = 5f;

    private const int NONE = -1;

    private static readonly int[] s_falls = new int[MAX_PLAYERS];
    private static readonly int[] s_kills = new int[MAX_PLAYERS];
    private static readonly int[] s_last_attacker = new int[MAX_PLAYERS];
    private static readonly float[] s_last_hit_time = new float[MAX_PLAYERS];

    public struct Entry
    {
        public int playerIndex;   // 0..3（表示は +1 P）
        public int kills;
        public int falls;
        public int score;
        public int rank;          // 1始まり。同スコアは同順位（1,1,3 のように詰めない）
    }

    static MatchStats()
    {
        Reset();
    }

    public static void Reset()
    {
        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            s_falls[i] = 0;
            s_kills[i] = 0;
            s_last_attacker[i] = NONE;
            s_last_hit_time[i] = 0f;
        }
    }

    /// <summary>attacker が victim にぶつかって飛ばした（落下時の撃墜判定に使う）</summary>
    public static void RegisterHit(int attacker, int victim)
    {
        if (!IsValid(attacker) || !IsValid(victim) || attacker == victim)
        {
            return;
        }
        s_last_attacker[victim] = attacker;
        s_last_hit_time[victim] = Time.time;
    }

    /// <summary>victim がステージ外に落ちた</summary>
    public static void RegisterFall(int victim)
    {
        if (!IsValid(victim))
        {
            return;
        }
        s_falls[victim]++;

        int attacker = s_last_attacker[victim];
        if (IsValid(attacker) && Time.time - s_last_hit_time[victim] <= KILL_CREDIT_SECONDS)
        {
            s_kills[attacker]++;
        }
        // リスポーンするので、次の落下に持ち越さない
        s_last_attacker[victim] = NONE;
    }

    public static int GetKills(int player) { return IsValid(player) ? s_kills[player] : 0; }
    public static int GetFalls(int player) { return IsValid(player) ? s_falls[player] : 0; }

    public static int GetScore(int player)
    {
        return GetKills(player) - GetFalls(player);
    }

    /// <summary>
    /// 参加者の順位表を返す（スコア降順、同点は番号順）。rank は同スコアなら同じ値
    /// </summary>
    public static List<Entry> Evaluate(IEnumerable<int> participants)
    {
        List<Entry> list = new List<Entry>();
        foreach (int p in participants)
        {
            if (!IsValid(p))
            {
                continue;
            }
            list.Add(new Entry
            {
                playerIndex = p,
                kills = GetKills(p),
                falls = GetFalls(p),
                score = GetScore(p)
            });
        }

        list.Sort((a, b) =>
        {
            int c = b.score.CompareTo(a.score);
            return c != 0 ? c : a.playerIndex.CompareTo(b.playerIndex);
        });

        for (int i = 0; i < list.Count; i++)
        {
            Entry e = list[i];
            int better = 0;
            foreach (Entry other in list)
            {
                if (other.score > e.score)
                {
                    better++;
                }
            }
            e.rank = better + 1;
            list[i] = e;
        }
        return list;
    }

    /// <summary>1位が複数なら引き分け</summary>
    public static bool IsDraw(List<Entry> ranking)
    {
        int top = 0;
        foreach (Entry e in ranking)
        {
            if (e.rank == 1)
            {
                top++;
            }
        }
        return top > 1;
    }

    private static bool IsValid(int player)
    {
        return player >= 0 && player < MAX_PLAYERS;
    }
}
