using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TrophyPop;

// Paleta de cores prontas + campo para digitar a cor (#RRGGBB ou #AARRGGBB).
public sealed class ColorPick : UserControl
{
    static readonly string[] Palette =
    [
        "#222327", "#111318", "#2B2D42", "#1E3A8A", "#3B82F6", "#0EA5E9", "#14B8A6", "#16A34A",
        "#84CC16", "#EAB308", "#F97316", "#DC2626", "#DB2777", "#7C3AED", "#F2F3F5", "#FFFFFF",
    ];

    readonly TextBox hex = new() { Width = 130, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    readonly Border current = new()
    {
        Width = 30,
        Height = 30,
        CornerRadius = new CornerRadius(6),
        BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)),
        BorderThickness = new Thickness(1),
    };
    readonly TextBlock emptyText = new() { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
    readonly string? emptyLabel;
    bool updating;

    public event Action<string>? Changed;
    public string Value { get; private set; } = "";

    /// <param name="emptyLabel">Se não for null, oferece a opção "sem cor" com esse nome (ex.: "Automática").</param>
    public ColorPick(string value, string? emptyLabel = null)
    {
        this.emptyLabel = emptyLabel;
        var swatches = new WrapPanel();
        if (emptyLabel != null)
        {
            var none = new Button { Content = emptyLabel, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 2, 10, 2) };
            none.Click += (_, _) => Set("", updateText: true);
            swatches.Children.Add(none);
        }
        foreach (var c in Palette)
        {
            var swatch = new Border
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(0, 0, 6, 6),
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = c,
            };
            swatch.MouseLeftButtonUp += (_, _) => Set(c, updateText: true);
            swatches.Children.Add(swatch);
        }

        hex.TextChanged += (_, _) =>
        {
            if (updating) return;
            var text = hex.Text.Trim();
            if (text.Length == 0 && emptyLabel != null) Set("", updateText: false);
            else if (TrophyCard.ParseColor(text) != null) Set(text, updateText: false);
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        row.Children.Add(current);
        row.Children.Add(hex);
        row.Children.Add(emptyText);

        var root = new StackPanel();
        root.Children.Add(swatches);
        root.Children.Add(row);
        Content = root;

        Set(value, updateText: true, notify: false);
    }

    void Set(string value, bool updateText, bool notify = true)
    {
        Value = value;
        var color = TrophyCard.ParseColor(value);
        current.Background = color is { } c ? new SolidColorBrush(c) : Brushes.Transparent;
        emptyText.Text = color == null && emptyLabel != null ? emptyLabel : "";
        if (updateText)
        {
            updating = true;
            hex.Text = value;
            updating = false;
        }
        if (notify) Changed?.Invoke(value);
    }
}
