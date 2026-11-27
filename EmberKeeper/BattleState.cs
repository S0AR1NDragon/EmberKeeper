using System;
using System.Collections.Generic;

namespace EmberKeeper;

/// <summary>Общее боевое состояние: текущий ход и порядок инициативы (для экрана игроков и подсветки на карте).</summary>
public static class BattleState
{
    public static int Round { get; private set; } = 1;
    public static int Turn { get; private set; }
    public static List<Combatant> Order { get; private set; } = new();

    public static event Action? Changed;

    public static string? CurrentName =>
        Order.Count == 0 ? null : Order[Math.Clamp(Turn, 0, Order.Count - 1)].Name;

    public static void Set(List<Combatant> order, int turn, int round)
    {
        Order = order ?? new List<Combatant>();
        Turn = order == null || order.Count == 0 ? 0 : Math.Clamp(turn, 0, order.Count - 1);
        Round = Math.Max(1, round);
        Changed?.Invoke();
    }

    public static void SetTurn(int turn, int round)
    {
        Turn = Order.Count == 0 ? 0 : Math.Clamp(turn, 0, Order.Count - 1);
        Round = Math.Max(1, round);
        Changed?.Invoke();
    }
}
