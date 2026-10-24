using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace BongoChat
{
    public sealed partial class ChatWindow
    {
        private double windowAlpha = 1;
        private void InitializeTransparentFrame(Grid root)
        {
            // Per-pixel WPF transparency requires a borderless native window.
            // Supply the title bar actions explicitly rather than dimming an opaque HWND.
            foreach (UIElement child in root.Children) Grid.SetRow(child, Grid.GetRow(child) + 1);
            root.RowDefinitions.Insert(0, new RowDefinition { Height = GridLength.Auto });
            var bar = new Grid { Height = 30 };
            bar.SetResourceReference(Panel.BackgroundProperty, "ColorFCFCF8");
            bar.ColumnDefinitions.Add(new ColumnDefinition());
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = new TextBlock { Text = Title, ToolTip = Title, FontSize = 10, Margin = new Thickness(10,0,8,0),
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            title.SetResourceReference(TextBlock.ForegroundProperty, "Color718795");
            title.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                else try { DragMove(); } catch (InvalidOperationException) { }
            };
            bar.Children.Add(title);
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetColumn(actions, 1);
            string[] names = { "WindowMinimize", "WindowMaximize", "WindowClose" };
            string[] labels = { "−", "□", "×" };
            for (int i = 0; i < labels.Length; i++)
            {
                int command = i;
                var button = new Button { Content = labels[i], Width = 32, Height = 28, FontSize = 14,
                    Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Focusable = false };
                button.Click += delegate
                {
                    if (command == 0) WindowState = WindowState.Minimized;
                    else if (command == 1) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                    else Close();
                };
                actions.Children.Add(button); root.RegisterName(names[i], button);
            }
            bar.Children.Add(actions); root.Children.Add(bar);
            var grip = new ResizeGrip { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Width = 16, Height = 16 };
            Grid.SetRow(grip, root.RowDefinitions.Count - 1); root.Children.Add(grip);
        }
        private void RefreshFrameLabels()
        {
            if (rootGrid == null) return;
            string[] names = { "WindowMinimize", "WindowMaximize", "WindowClose" };
            string[] labels = { "최소화", "최대화 / 복원", "닫기" };
            for (int i = 0; i < names.Length; i++)
            {
                var button = rootGrid.FindName(names[i]) as Button;
                if (button == null) continue;
                button.ToolTip = L(labels[i]);
                System.Windows.Automation.AutomationProperties.SetName(button, L(labels[i]));
            }
        }
        private void SetWindowOpacity(double value)
        {
            windowAlpha = Math.Max(0.4, Math.Min(1, value));
            Opacity = windowAlpha;
        }
    }
}