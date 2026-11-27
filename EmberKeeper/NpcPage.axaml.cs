using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

public partial class NpcPage : UserControl
{
    private List<Npc> _npcs = new();
    private List<Npc> _view = new();
    private bool _sync;
    private string _q = "";
    public Action<Npc>? ToHero;

    private readonly Dictionary<string, ComboBox> _filters = new();

    private static readonly (string Key, string Label)[] FilterDefs =
    {
        ("faction", "Фракция"),
        ("city", "Город"),
        ("org", "Организация"),
        ("campaign", "Кампания"),
        ("tags", "Теги"),
    };

    public NpcPage()
    {
        InitializeComponent();
        _npcs = NpcStore.Load();
        BuildFilters();
        Refresh();
    }

    private void BuildFilters()
    {
        foreach (var (key, label) in FilterDefs)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 0, 6, 0) };
            sp.Children.Add(new TextBlock { Text = label, Foreground = Pal.B("#9c8b78"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            var cb = new ComboBox { Width = 108, Background = Pal.B("#15110e"), Foreground = Pal.B("#efe3d2"), BorderBrush = Pal.B("#352a21"), CornerRadius = new CornerRadius(8), FontSize = 11.5 };
            cb.SelectionChanged += (_, _) => ApplyFilter();
            _filters[key] = cb;
            sp.Children.Add(cb);
            FilterHost.Children.Add(sp);
        }
    }

    private void RefreshFilterOptions()
    {
        foreach (var (key, _) in FilterDefs)
        {
            var cb = _filters[key];
            var current = cb.SelectedItem as string ?? "все";
            var values = new List<string> { "все" };
            foreach (var n in _npcs)
            {
                var vals = key switch
                {
                    "faction" => new[] { n.Faction },
                    "city" => new[] { n.City },
                    "org" => new[] { n.Organization },
                    "campaign" => new[] { n.Campaign },
                    "tags" => n.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    _ => Array.Empty<string>(),
                };
                foreach (var v in vals)
                {
                    var t = v.Trim();
                    if (t.Length > 0 && !values.Contains(t)) values.Add(t);
                }
            }
            cb.ItemsSource = values;
            cb.SelectedItem = values.Contains(current) ? current : "все";
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<Npc> src = _npcs;
        if (!string.IsNullOrWhiteSpace(_q))
        {
            var q = _q.Trim().ToLowerInvariant();
            src = src.Where(n => $"{n.Name} {n.Class} {n.Race} {n.Tags} {n.Faction} {n.City} {n.Organization} {n.Campaign}".ToLowerInvariant().Contains(q));
        }
        foreach (var (key, _) in FilterDefs)
        {
            var sel = _filters[key].SelectedItem as string;
            if (string.IsNullOrEmpty(sel) || sel == "все") continue;
            src = key switch
            {
                "faction" => src.Where(n => n.Faction == sel),
                "city" => src.Where(n => n.City == sel),
                "org" => src.Where(n => n.Organization == sel),
                "campaign" => src.Where(n => n.Campaign == sel),
                "tags" => src.Where(n => n.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(sel)),
                _ => src,
            };
        }

        _view = src.OrderBy(n => n.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        NpcList.ItemsSource = _view;

        if (EmptyHint != null) EmptyHint.IsVisible = NpcList.ItemCount == 0;
        NpcList.ItemTemplate = new FuncDataTemplate<Npc>((n, _) =>
        {
            var name = new TextBlock { Text = n.Name, FontSize = 13.5, FontWeight = FontWeight.SemiBold, Foreground = Pal.B("#f6e7cf") };
            var sub = new TextBlock
            {
                Text = (string.IsNullOrEmpty(n.Class) ? "" : n.Class + " · ") + n.Hp + " ХП" + (string.IsNullOrEmpty(n.Tags) ? "" : " · " + n.Tags),
                FontSize = 11.5,
                Foreground = Pal.B("#9c8b78"),
                Margin = new Thickness(0, 3, 0, 0),
            };
            var sp = new StackPanel { Margin = new Thickness(10, 7) };
            sp.Children.Add(name);
            sp.Children.Add(sub);
            var meta = string.Join(" · ", new[] { n.Faction, n.City, n.Organization }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (!string.IsNullOrWhiteSpace(meta))
                sp.Children.Add(new TextBlock { Text = meta, FontSize = 10.5, Foreground = Pal.B("#83725f"), Margin = new Thickness(0, 2, 0, 0) });
            return sp;
        }, true);

        CountText.Text = $"{_view.Count} из {_npcs.Count}";
        if (_view.Count > 0 && NpcList.SelectedIndex < 0) NpcList.SelectedIndex = 0;
    }

    /// <summary>Создать НПС из героя (переносим имя, класс, вид, ХП, КД, заметки и теги).</summary>
    public void AddFromHero(Hero h)
    {
        var n = _npcs.FirstOrDefault(x => string.Equals(x.Name, h.Name, StringComparison.OrdinalIgnoreCase)) ?? new Npc { Name = h.Name };
        if (!_npcs.Contains(n)) _npcs.Add(n);
        n.Race = h.Race;
        n.Class = h.Class;
        n.Hp = h.MaxHp > 0 ? h.MaxHp : h.Hp;
        n.Ac = h.Ac;
        if (!string.IsNullOrEmpty(h.Tags)) n.Tags = h.Tags;
        if (string.IsNullOrEmpty(n.Faction)) n.Faction = h.Faction;
        if (string.IsNullOrEmpty(n.City)) n.City = h.City;
        if (string.IsNullOrEmpty(n.Organization)) n.Organization = h.Organization;
        if (string.IsNullOrEmpty(n.Campaign)) n.Campaign = h.Campaign;

        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(h.Subclass)) notes.Add("Подкласс: " + h.Subclass);
        if (!string.IsNullOrWhiteSpace(h.Background)) notes.Add("Предыстория: " + h.Background);
        if (!string.IsNullOrWhiteSpace(h.Notes)) notes.Add(h.Notes);
        if (!string.IsNullOrWhiteSpace(h.Goals)) notes.Add("Цели: " + h.Goals);
        if (notes.Count > 0) n.Notes = string.Join("\n", notes);

        NpcStore.Save(_npcs);
        Refresh();
        NpcList.SelectedItem = n;
    }

    public void SelectByName(string name)
    {
        var n = _npcs.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (n == null) return;
        ApplyFilter();
        NpcList.SelectedItem = n;
        LoadInto(n);
    }

    private Npc? Cur => NpcList.SelectedItem as Npc;

    private void Refresh()
    {
        RefreshFilterOptions();
        ApplyFilter();
    }

    private void LoadInto(Npc n)
    {
        _sync = true;
        EdName.Text = n.Name;
        EdRace.Text = n.Race;
        EdClass.Text = n.Class;
        EdHp.Text = n.Hp.ToString();
        EdAc.Text = n.Ac.ToString();
        EdTags.Text = n.Tags;
        EdFaction.Text = n.Faction;
        EdCity.Text = n.City;
        EdOrg.Text = n.Organization;
        EdCampaign.Text = n.Campaign;
        EdNotes.Text = n.Notes;
        MusicLabel.Text = string.IsNullOrEmpty(n.Music) ? "нет музыки" : Path.GetFileName(n.Music);
        _sync = false;
    }

    private void OnSelect(object? sender, SelectionChangedEventArgs e) { if (Cur is Npc n) LoadInto(n); }

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        var n = new Npc { Name = "Новый НПС" };
        _npcs.Add(n);
        NpcStore.Save(_npcs);
        Refresh();
        NpcList.SelectedItem = n;
    }

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (Cur is Npc n) { _npcs.Remove(n); NpcStore.Save(_npcs); Refresh(); }
    }

    private void Save() => NpcStore.Save(_npcs);

    private void OnName(object? s, TextChangedEventArgs e) { if (!_sync && Cur is Npc n) { n.Name = EdName.Text ?? ""; Save(); Refresh(); NpcList.SelectedItem = n; } }
    private void OnRace(object? s, TextChangedEventArgs e) { if (!_sync && Cur is Npc n) { n.Race = EdRace.Text ?? ""; Save(); } }
    private void OnClass(object? s, TextChangedEventArgs e) { if (!_sync && Cur is Npc n) { n.Class = EdClass.Text ?? ""; Save(); } }
    private void OnTags(object? s, TextChangedEventArgs e) { if (!_sync && Cur is Npc n) { n.Tags = EdTags.Text ?? ""; Save(); } }
    private void OnNotes(object? s, TextChangedEventArgs e) { if (!_sync && Cur is Npc n) { n.Notes = EdNotes.Text ?? ""; Save(); } }
    private void OnHp(object? s, TextChangedEventArgs e) { if (!_sync && Cur is Npc n) { n.Hp = int.TryParse(EdHp.Text, out var v) ? v : n.Hp; Save(); } }
    private void OnAc(object? s, TextChangedEventArgs e) { if (!_sync && Cur is Npc n) { n.Ac = int.TryParse(EdAc.Text, out var v) ? v : n.Ac; Save(); } }
    private void OnFaction(object? s, TextChangedEventArgs e) { if (!_sync && Cur is Npc n) { n.Faction = EdFaction.Text ?? ""; Save(); RefreshFilterOptions(); } }
    private void OnCity(object? s, TextChangedEventArgs e) { if (!_sync && Cur is Npc n) { n.City = EdCity.Text ?? ""; Save(); RefreshFilterOptions(); } }
    private void OnOrg(object? s, TextChangedEventArgs e) { if (!_sync && Cur is Npc n) { n.Organization = EdOrg.Text ?? ""; Save(); RefreshFilterOptions(); } }
    private void OnCampaign(object? s, TextChangedEventArgs e) { if (!_sync && Cur is Npc n) { n.Campaign = EdCampaign.Text ?? ""; Save(); RefreshFilterOptions(); } }

    private void OnSearch(object? s, TextChangedEventArgs e) { _q = SearchBox.Text ?? ""; ApplyFilter(); }

    private async void OnPickMusic(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null || Cur is not Npc n) return;
        var path = await FilePickerDialog.Pick(top as Window, "Выбрать музыку", "*.mp3;*.wav;*.wma;*.ogg;*.flac");
        if (string.IsNullOrEmpty(path)) return;
        n.Music = path;
        Save();
        LoadInto(n);
    }

    private void OnPlayMusic(object? sender, RoutedEventArgs e)
    {
        if (Cur is not Npc n || string.IsNullOrEmpty(n.Music) || !File.Exists(n.Music)) return;
        if (Audio.Play(n.Music)) MusicLabel.Text = "Играет: " + Path.GetFileName(n.Music);
        else MusicLabel.Text = "не удалось воспроизвести";
    }

    private void OnStopMusic(object? sender, RoutedEventArgs e)
    {
        Audio.Stop();
        MusicLabel.Text = "остановлено";
    }

    private void OnToHero(object? sender, RoutedEventArgs e)
    {
        if (Cur is Npc n) ToHero?.Invoke(n);
    }
}
