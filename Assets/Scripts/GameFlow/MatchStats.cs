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

    private const int NONE = -1;

    private static readonly int[] s_falls = new int[MAX_PLAYERS];
    private static readonly int[] s_kills = new int[MAX_PLAYERS];
    private static readonly int[] s_last_attacker = new int[MAX_PLAYERS];
    private struct NonPlayerHit
    {
        public int attacker;
    }
    private static readonly System.Collections.Generic.Dictionary<EntityId, NonPlayerHit> s_nonPlayerHits =
        new();

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
        }
        s_nonPlayerHits.Clear();
    }

    /// <summary>attacker が victim にぶつかって飛ばした（落下時の撃墜判定に使う）</summary>
    public static void RegisterHit(int attacker, int victim)
    {
        if (!IsValid(attacker) || !IsValid(victim) || attacker == victim)
        {
            return;
        }
        s_last_attacker[victim] = attacker;
    }

    public static void RegisterNonPlayerHit(int attacker, GameObject victim)
    {
        if (!IsValid(attacker) || victim == null)
        {
            return;
        }

        s_nonPlayerHits[victim.GetEntityId()] = new NonPlayerHit
        {
            attacker = attacker
        };
    }

    public static void RegisterNonPlayerFall(GameObject victim)
    {
        if (victim == null)
        {
            return;
        }

        EntityId victimId = victim.GetEntityId();
        if (!s_nonPlayerHits.TryGetValue(victimId, out NonPlayerHit hit))
        {
            return;
        }

        s_nonPlayerHits.Remove(victimId);
        if (IsValid(hit.attacker))
        {
            s_kills[hit.attacker]++;
        }
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
        if (IsValid(attacker))
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
    public static System.Collections.Generic.List<Entry> Evaluate(System.Collections.Generic.IEnumerable<int> participants)
    {
        System.Collections.Generic.List<Entry> list = new();
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
    public static bool IsDraw(System.Collections.Generic.List<Entry> ranking)
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
