using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace EmberKeeper;

/// <summary>Окно активации: показывает код компьютера и принимает лицензионный ключ.</summary>
public sealed class ActivationWindow : Window
{
    private readonly TextBox _key;
    private readonly TextBlock _error;
    private readonly TextBlock _status;
    public bool Activated { get; private set; }

    public ActivationWindow()
    {
        Pal.Set(Settings.LoadDark(), Settings.LoadStyle());

        Title = "EmberKeeper — активация";
        Width = 620;
        Height = 520;
        CanResize = false;
        Background = Pal.B("#100d0b");
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new StackPanel { Margin = new Thickness(28), Spacing = 12 };

        root.Children.Add(new TextBlock
        {
            Text = "EMBERKEEPER",
            FontFamily = new FontFamily("Georgia"),
            FontSize = 30,
            FontWeight = FontWeight.Bold,
            Foreground = Pal.B("#f0c27b"),
        });
        root.Children.Add(new TextBlock
        {
            Text = "Для работы программы нужна лицензия.",
            FontSize = 13,
            Foreground = Pal.B("#9c8b78"),
            Margin = new Thickness(0, 0, 0, 8),
        });

        root.Children.Add(new TextBlock { Text = "КОД ЭТОГО КОМПЬЮТЕРА", FontSize = 9.5, LetterSpacing = 0.8, Foreground = Pal.B("#8d7a66") });

        var code = License.MachineId();
        var codeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var codeText = new SelectableTextBlock
        {
            Text = code,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 20,
            Foreground = Pal.B("#efe3d2"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        codeRow.Children.Add(codeText);
        var copy = Btn("Копировать", async () =>
        {
            var clip = Clipboard;
            if (clip != null) await clip.SetTextAsync(code);
            _status.Text = "Код скопирован — отправьте его владельцу программы.";
        }, false);
        codeRow.Children.Add(copy);
        root.Children.Add(codeRow);

        root.Children.Add(new TextBlock
        {
            Text = "Отправьте этот код владельцу программы и вставьте полученный ключ ниже.",
            FontSize = 11.5,
            Foreground = Pal.B("#9c8b78"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6),
        });

        root.Children.Add(new TextBlock { Text = "ЛИЦЕНЗИОННЫЙ КЛЮЧ", FontSize = 9.5, LetterSpacing = 0.8, Foreground = Pal.B("#8d7a66") });
        _key = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 96,
            Background = Pal.B("#1c1714"),
            Foreground = Pal.B("#efe3d2"),
            BorderBrush = Pal.B("#352a21"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 8),
            FontSize = 12,
            Watermark = "Вставьте ключ, который вам прислали",
        };
        root.Children.Add(_key);

        _error = new TextBlock { FontSize = 12, Foreground = Pal.B("#c96a5a"), TextWrapping = TextWrapping.Wrap, MinHeight = 18 };
        root.Children.Add(_error);

        _status = new TextBlock { FontSize = 11, Foreground = Pal.B("#9c8b78"), TextWrapping = TextWrapping.Wrap };
        root.Children.Add(_status);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };
        buttons.Children.Add(Btn("Активировать", Activate, true));
        buttons.Children.Add(Btn("Выйти", Close, false));
        root.Children.Add(buttons);

        Content = new ScrollViewer
        {
            Content = root,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    private void Activate()
    {
        if (!License.Activate(_key.Text ?? "", out var error))
        {
            _error.Text = error;
            return;
        }

        Activated = true;
        _error.Text = "";
        Close();
    }

    private static Button Btn(string text, Action onClick, bool accent)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(14, 8),
            CornerRadius = new CornerRadius(9),
            FontSize = 12.5,
            Background = accent ? Pal.B("#cf9245") : Pal.B("#221b16"),
            Foreground = accent ? Pal.B("#241505") : Pal.B("#efe3d2"),
            BorderBrush = accent ? Pal.B("#e0b072") : Pal.B("#352a21"),
            BorderThickness = new Thickness(1),
            FontWeight = accent ? FontWeight.Bold : FontWeight.Normal,
        };
        b.Click += (_, _) => onClick();
        return b;
    }
}
