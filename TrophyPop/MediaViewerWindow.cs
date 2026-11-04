using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;

namespace TrophyPop;

/// <summary>
/// Janela para visualizar o print (.jpg) de um troféu ou assistir ao clipe (.mp4) da Platina.
/// </summary>
public sealed class MediaViewerWindow : Window
{
    readonly string filePath;
    readonly MediaElement? player;

    public MediaViewerWindow(string title, TrophyGrade grade, string filePath)
    {
        this.filePath = filePath;
        bool isVideo = filePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);

        Title = (isVideo ? "Clipe da Platina · " : "Print do Troféu · ") + title;
        Width = 920;
        Height = 600;
        MinWidth = 640;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ThemeMode = ThemeMode.Dark;
        Background = new SolidColorBrush(Color.FromRgb(0x08, 0x0B, 0x16));
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        Icon = TrophyArt.Render(64, grade);

        var root = new Grid { Margin = new Thickness(22, 18, 22, 18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Cabeçalho
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        header.Children.Add(new Shapes.Path
        {
            Data = TrophyArt.Geometry,
            Fill = TrophyArt.Brush(grade),
            Width = 24,
            Height = 24,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.Children.Add(new TextBlock
        {
            Text = isVideo ? " · Clipe de Platina (.mp4)" : $" · Print ({Grades.Label(grade)})",
            FontSize = 14,
            Opacity = 0.6,
            VerticalAlignment = VerticalAlignment.Center,
        });
        root.Children.Add(header);

        // Área da mídia
        var frame = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(0x66, 0x00, 0x00, 0x00)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
        };
        Grid.SetRow(frame, 1);

        if (isVideo)
        {
            player = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Close,
                Stretch = Stretch.Uniform,
                Source = new Uri(filePath),
            };
            player.MediaEnded += (_, _) =>
            {
                player.Position = TimeSpan.Zero;
                player.Play();
            };
            frame.Child = player;
            Loaded += (_, _) => player.Play();
            Closed += (_, _) => { try { player.Stop(); player.Close(); } catch { } };
        }
        else
        {
            frame.Child = new Image
            {
                Source = TrophyArt.FromFile(filePath),
                Stretch = Stretch.Uniform,
            };
        }
        root.Children.Add(frame);

        // Barra inferior
        var bar = new DockPanel { Margin = new Thickness(0, 14, 0, 0), LastChildFill = false };
        Grid.SetRow(bar, 2);

        var openFolderBtn = new Button
        {
            Content = "Mostrar arquivo na pasta",
            Padding = new Thickness(14, 6, 14, 6),
        };
        openFolderBtn.Click += (_, _) => MediaCapture.RevealFile(filePath);
        DockPanel.SetDock(openFolderBtn, Dock.Left);
        bar.Children.Add(openFolderBtn);

        if (isVideo && player != null)
        {
            var replayBtn = new Button
            {
                Content = "Reiniciar vídeo",
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(14, 6, 14, 6),
            };
            replayBtn.Click += (_, _) =>
            {
                player.Position = TimeSpan.Zero;
                player.Play();
            };
            DockPanel.SetDock(replayBtn, Dock.Left);
            bar.Children.Add(replayBtn);
        }

        if (File.Exists(filePath))
        {
            long kb = new FileInfo(filePath).Length / 1024;
            var sizeInfo = new TextBlock
            {
                Text = kb >= 1024 ? $"{kb / 1024.0:0.1} MB" : $"{Math.Max(1, kb)} KB",
                Opacity = 0.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0),
            };
            DockPanel.SetDock(sizeInfo, Dock.Left);
            bar.Children.Add(sizeInfo);
        }

        var closeBtn = new Button
        {
            Content = "Fechar",
            Width = 100,
            IsCancel = true,
            IsDefault = true,
        };
        closeBtn.Click += (_, _) => Close();
        DockPanel.SetDock(closeBtn, Dock.Right);
        bar.Children.Add(closeBtn);

        Content = root;
    }
}
