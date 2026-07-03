using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GameTranslatorLens;

public partial class ReleaseNotesWindow : Window
{
    private const double WheelScrollPixelsPerNotch = 180;

    public ReleaseNotesWindow(string version, string notes)
    {
        InitializeComponent();
        VersionText.Text = $"Game Translator Lens 已更新到 {version}";
        string displayNotes = ExtractUpdateHighlights(notes);
        NotesText.Text = string.IsNullOrWhiteSpace(displayNotes)
            ? "此版本没有随包附带更新说明。"
            : displayNotes.Trim();
    }

    public static string LoadBundledNotes()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Resources", "ReleaseNotes", "current.md");
        if (!File.Exists(path))
        {
            return "";
        }

        return File.ReadAllText(path, System.Text.Encoding.UTF8);
    }

    private static string ExtractUpdateHighlights(string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return "";
        }

        string normalized = notes.Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (string heading in new[] { "## 更新要点", "## 本次更新", "## 更新内容" })
        {
            string section = ExtractSection(normalized, heading);
            if (!string.IsNullOrWhiteSpace(section))
            {
                return section;
            }
        }

        return normalized.Trim();
    }

    private static string ExtractSection(string notes, string heading)
    {
        int start = notes.IndexOf(heading, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return "";
        }

        int contentStart = start + heading.Length;
        while (contentStart < notes.Length && notes[contentStart] is '\n' or ' ')
        {
            contentStart++;
        }

        int nextHeading = notes.IndexOf("\n## ", contentStart, StringComparison.OrdinalIgnoreCase);
        string section = nextHeading >= 0
            ? notes[contentStart..nextHeading]
            : notes[contentStart..];
        return section.Trim();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // Window drag is convenience-only.
        }
    }

    private void NotesScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer)
        {
            return;
        }

        double deltaNotches = e.Delta / 120.0;
        double target = Math.Clamp(
            viewer.VerticalOffset - deltaNotches * WheelScrollPixelsPerNotch,
            0,
            viewer.ScrollableHeight);
        viewer.ScrollToVerticalOffset(target);
        e.Handled = true;
    }
}
