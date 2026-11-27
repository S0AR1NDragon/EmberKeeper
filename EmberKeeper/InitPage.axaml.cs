using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

public partial class InitPage : UserControl
{
    private List<Combatant> _list = new();
    private int _round = 1;
    private int _turn;
    private readonly Random _rng = new();

    public InitPage()
    {
        InitializeComponent();
        _list = InitStore.Load();
        Sort();
        Render();
    }

    private void Sort() => _list = _list.OrderByDescending(c => c.Init).ToList();

    private void Render()
    {
        List.Children.Clear();
        BattleState.Set(_list, _turn, _round);
        RoundText.Text = $"Раунд {_round}";
        TurnText.Text = _list.Count == 0 ? "" : $"Ход: {_list[_turn % _list.Count].Name}";

        for (int i = 0; i < _list.Count; i++)
        {
            var c = _list[i];
            int idx = i;
            bool current = _list.Count > 0 && idx == _turn % _list.Count;

            var border = new Border
            {
                Background = Pal.B(current ? "#3a2d24" : "#15110e"),
                BorderBrush = Pal.B(current ? "#4a3728" : "#2a211a"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 8),
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto,Auto,Auto") };

            var initBadge = new Border
            {
                Background = Pal.B("#4a3728"),
                BorderBrush = Pal.B("#4a3728"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = c.Init.ToString(), FontWeight = FontWeight.Bold, Foreground = Pal.B("#f0c27b") },
            };
            grid.Children.Add(initBadge);
            Grid.SetColumn(initBadge, 0);

            var name = new TextBlock
            {
                Text = (c.IsPc ? "◆ " : "") + c.Name,
                FontSize = 14,
                FontWeight = current ? FontWeight.Bold : FontWeight.Normal,
                Foreground = Pal.B(current ? "#f6e7cf" : "#efe3d2"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
            };
            grid.Children.Add(name);
            Grid.SetColumn(name, 1);

            var hp = new NumericUpDown { Value = c.Hp, Minimum = -999, Maximum = 9999, Width = 76, VerticalAlignment = VerticalAlignment.Center };
            hp.ValueChanged += (_, _) => { c.Hp = (int)hp.Value; c.MaxHp = Math.Max(c.Hp, c.MaxHp); InitStore.Save(_list); Render(); };
            grid.Children.Add(hp);
            Grid.SetColumn(hp, 2);

            var maxHp = new TextBlock { Text = "/ " + c.MaxHp, Foreground = Pal.B("#9c8b78"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            grid.Children.Add(maxHp);
            Grid.SetColumn(maxHp, 3);

            var ac = new TextBlock { Text = "КД " + c.Ac, Foreground = Pal.B("#9c8b78"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            grid.Children.Add(ac);
            Grid.SetColumn(ac, 4);

            var up = new Button { Content = "▲", Classes = { "tool" }, Width = 40, Padding = new Thickness(6, 4), Margin = new Thickness(10, 0, 0, 0) };
            up.Click += (_, _) => { if (idx > 0) { (_list[idx], _list[idx - 1]) = (_list[idx - 1], _list[idx]); InitStore.Save(_list); Render(); } };
            grid.Children.Add(up);
            Grid.SetColumn(up, 5);

            var del = new Button { Content = "✕", Classes = { "tool" }, Width = 40, Padding = new Thickness(6, 4) };
            del.Click += (_, _) => { _list.RemoveAt(idx); _turn = Math.Min(_turn, Math.Max(0, _list.Count - 1)); InitStore.Save(_list); Render(); };
            grid.Children.Add(del);
            Grid.SetColumn(del, 6);

            border.Child = grid;
            List.Children.Add(border);
        }
    }

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        string name = AddName.Text?.Trim() ?? "";
        if (name.Length == 0) return;
        var c = new Combatant
        {
            Name = name,
            Init = int.TryParse(AddInit.Text, out var ini) ? ini : 0,
            Hp = int.TryParse(AddHp.Text, out var hp) ? hp : 10,
            MaxHp = int.TryParse(AddHp.Text, out var mhp) ? mhp : 10,
            Ac = int.TryParse(AddAc.Text, out var ac) ? ac : 10,
            IsPc = AddKind.SelectedIndex == 0,
        };
        _list.Add(c);
        Sort();
        InitStore.Save(_list);
        AddName.Text = "";
        Render();
    }

    private void OnNext(object? sender, RoutedEventArgs e)
    {
        if (_list.Count == 0) return;
        _turn++;
        if (_turn >= _list.Count) { _turn = 0; _round++; }
        Render();
    }

    private void OnRollAll(object? sender, RoutedEventArgs e)
    {
        foreach (var c in _list) c.Init = _rng.Next(1, 21) + (_rng.Next(1, 5) - 1);
        Sort();
        _turn = 0;
        _round = 1;
        InitStore.Save(_list);
        Render();
    }

    private MapDoc? ActiveMap()
    {
        try
        {
            var book = MapStore.Load();
            if (book.Maps.Count == 0) return null;
            return book.Maps[Math.Clamp(book.Active, 0, book.Maps.Count - 1)];
        }
        catch { return null; }
    }

    private void OnFromMap(object? sender, RoutedEventArgs e)
    {
        var doc = ActiveMap();
        if (doc == null) { SyncText.Text = "нет карты"; return; }

        int added = 0;
        foreach (var t in doc.Tokens)
        {
            if (_list.Any(c => string.Equals(c.Name, t.Name, StringComparison.OrdinalIgnoreCase))) continue;
            _list.Add(new Combatant
            {
                Name = t.Name,
                Init = t.Init,
                Hp = t.Hp,
                MaxHp = Math.Max(1, t.MaxHp),
                Ac = t.Ac,
                IsPc = t.Type == "player",
            });
            added++;
        }
        Sort();
        InitStore.Save(_list);
        Render();
        SyncText.Text = added == 0 ? "все уже в списке" : "добавлено с карты: " + added;
    }

    private void OnToMap(object? sender, RoutedEventArgs e)
    {
        MapBook book;
        try { book = MapStore.Load(); }
        catch { SyncText.Text = "нет карты"; return; }
        if (book.Maps.Count == 0) { SyncText.Text = "нет карты"; return; }

        var doc = book.Maps[Math.Clamp(book.Active, 0, book.Maps.Count - 1)];

        int updated = 0;
        foreach (var c in _list)
        {
            var t = doc.Tokens.FirstOrDefault(x => string.Equals(x.Name, c.Name, StringComparison.OrdinalIgnoreCase));
            if (t == null) continue;
            t.Hp = c.Hp;
            t.MaxHp = Math.Max(1, c.MaxHp);
            t.Init = c.Init;
            t.Ac = c.Ac;
            updated++;
        }

        MapStore.Save(book);
        Live.RaiseMapChanged();
        SyncText.Text = updated == 0 ? "нет совпадений на карте" : "обновлено на карте: " + updated;
    }

    private void OnSort(object? sender, RoutedEventArgs e) { Sort(); InitStore.Save(_list); Render(); }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        _round = 1;
        _turn = 0;
        Render();
    }
}
