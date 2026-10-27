using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BongoChat
{
    public sealed partial class ChatWindow
    {
        private TextBox SelectableMessageText(ChatEntry entry)
        {
            // Native read-only selection supports dragging and Ctrl+C without
            // changing message content or adding a separate copy workflow.
            var text = new TextBox {
                Text = entry.text ?? "", IsReadOnly = true, IsReadOnlyCaretVisible = false,
                IsUndoEnabled = false, IsTabStop = false, AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0),
                Padding = new Thickness(0), Margin = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = entry.mine ? RawBrush("#293B46") : Brush("#293B46"),
                FontSize = 13, MinWidth = 0,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                FocusVisualStyle = null, Cursor = Cursors.IBeam
            };
            TextBlock.SetLineHeight(text,21);
            return text;
        }
    }
}