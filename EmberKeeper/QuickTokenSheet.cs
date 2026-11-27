using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

/// <summary>Быстрый лист токена: правки ХП/КД/заметок поверх карты.</summary>
public sealed class QuickTokenSheet : Window
{
    private readonly Token _t;
    private readonly Action _changed;
    private bool _sync;

    public QuickTokenSheet(Token t, Hero? hero, Action changed)
    {
        _t = t;
        _changed = changed;
        _t.Hp = Math.Min(_t.Hp, Math.Max(1, _t.MaxHp));

        Title = "Быстрый лист — " + t.Name;
        Width = 400;
        Height = 520;
        Background = Pal.B("#100d0b");
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SystemDecorations = SystemDecorations.Full;

        var root = new StackPanel { Margin = new Thickness(16), Spacing = 8 };

        root.Children.Add(new TextBlock
        {
            Text = "Быстрый лист",
            FontFamily = new FontFamily("Georgia"),
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = Pal.B("#f6e7cf"),
        });

        TextBox Field(string label, string value, bool multiline = false, double height = 0)
        {
            var sp = new StackPanel { Spacing = 2 };
            sp.Children.Add(new TextBlock { Text = label, FontSize = 10.5, Foreground = Pal.B("#9c8b78") });
            var tb = new TextBox
            {
                Text = value,
                Background = Pal.B("#15110e"),
                Foreground = Pal.B("#efe3d2"),
                BorderBrush = Pal.B("#352a21"),
                BorderThickness = new Thickness(1),
                FontSize = 13,
                AcceptsReturn = multiline,
                TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                Height = multiline ? Math.Max(70, height) : double.NaN,
            };
            sp.Children.Add(tb);
            root.Children.Add(sp);
            return tb;
        }

        var name = Field("ИМЯ", t.Name);
        var hp = Field("ХИТЫ", t.Hp.ToString());
        var maxHp = Field("МАКС. ХИТЫ", t.MaxHp.ToString());
        var ac = Field("КД", t.Ac.ToString());
        var init = Field("ИНИЦИАТИВА", t.Init.ToString());
        var light = Field("СВЕТ, ФТ (0 — выкл)", ((int)Math.Round(t.LightOn ? t.LightFt : 0)).ToString());
        var vision = Field("НОЧНОЕ ЗРЕНИЕ, ФТ", ((int)Math.Round(t.VisionFt)).ToString());
        var notes = Field("ЗАМЕТКИ", t.Notes, true, 90);

        var hidden = new CheckBox
        {
            Content = "Скрыт от игроков",
            IsChecked = t.Hidden,
            Foreground = Pal.B("#efe3d2"),
            FontSize = 12,
        };
        root.Children.Add(hidden);

        void Apply()
        {
            if (_sync) return;
            _sync = true;
            try
            {
                _t.Name = string.IsNullOrWhiteSpace(name.Text) ? _t.Name : name.Text!.Trim();
                _t.Hp = Math.Max(0, int.TryParse(hp.Text, out var h) ? h : _t.Hp);
                _t.MaxHp = Math.Max(1, int.TryParse(maxHp.Text, out var mh) ? mh : _t.MaxHp);
                _t.Ac = int.TryParse(ac.Text, out var a) ? a : _t.Ac;
                _t.Init = int.TryParse(init.Text, out var i) ? i : _t.Init;
                double lf = double.TryParse(light.Text, out var lv) ? lv : 0;
                _t.LightOn = lf > 0;
                _t.LightFt = lf > 0 ? lf : _t.LightFt;
                _t.VisionFt = double.TryParse(vision.Text, out var vv) ? Math.Max(0, vv) : _t.VisionFt;
                _t.Notes = notes.Text ?? "";
                _t.Hidden = hidden.IsChecked == true;

                if (hero != null)
                {
                    hero.Name = _t.Name;
                    hero.Hp = _t.Hp;
                    hero.MaxHp = _t.MaxHp;
                    hero.Ac = _t.Ac;
                    hero.Init = _t.Init;
                    hero.Darkvision = (int)Math.Round(_t.VisionFt);
                    var heroes = HeroStore.Load();
                    var hh = heroes.FirstOrDefault(x => x.Id == hero.Id) ?? hero;
                    hh.Name = hero.Name; hh.Hp = hero.Hp; hh.MaxHp = hero.MaxHp;
                    hh.Ac = hero.Ac; hh.Init = hero.Init; hh.Darkvision = hero.Darkvision;
                    if (!heroes.Contains(hh)) heroes.Add(hh);
                    HeroStore.Save(heroes);
                }

                _changed();
                Title = "Быстрый лист — " + _t.Name;
            }
            finally { _sync = false; }
        }

        name.TextChanged += (_, _) => Apply();
        hp.TextChanged += (_, _) => Apply();
        maxHp.TextChanged += (_, _) => Apply();
        ac.TextChanged += (_, _) => Apply();
        init.TextChanged += (_, _) => Apply();
        light.TextChanged += (_, _) => Apply();
        vision.TextChanged += (_, _) => Apply();
        notes.TextChanged += (_, _) => Apply();
        hidden.IsCheckedChanged += (_, _) => Apply();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button
        {
            Content = "Готово",
            Padding = new Thickness(16, 8),
            CornerRadius = new CornerRadius(9),
            Background = Pal.B("#cf9245"),
            Foreground = Pal.B("#241505"),
            BorderBrush = Pal.B("#e0b072"),
            BorderThickness = new Thickness(1),
            FontWeight = FontWeight.Bold,
        };
        ok.Click += (_, _) => Close();
        buttons.Children.Add(ok);
        root.Children.Add(buttons);

        Content = new ScrollViewer { Content = root };
    }
}
