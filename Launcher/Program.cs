using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace GameTranslatorLensLauncher
{
    internal static class Program
    {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONERROR = 0x00000010;

    [STAThread]
    private static int Main(string[] args)
    {
        string rootDirectory = AppContext.BaseDirectory;
        string appDirectory = Path.Combine(rootDirectory, "date", "app");
        string appPath = Path.Combine(appDirectory, "GameTranslatorLens.exe");

        if (!File.Exists(appPath))
        {
            MessageBoxW(IntPtr.Zero,
                "找不到 date\\app\\GameTranslatorLens.exe，请确认发布包已完整解压。",
                "Game Translator Lens",
                MB_OK | MB_ICONERROR);
            return 1;
        }

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = appPath,
                WorkingDirectory = appDirectory,
                UseShellExecute = false,
                Arguments = BuildArgumentString(args)
            };
            Process.Start(startInfo);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBoxW(IntPtr.Zero,
                "启动 Game Translator Lens 失败：" + ex.Message,
                "Game Translator Lens",
                MB_OK | MB_ICONERROR);
            return 2;
        }
    }

    private static string BuildArgumentString(string[] args)
    {
        if (args == null || args.Length == 0)
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) builder.Append(' ');
            builder.Append(QuoteArgument(args[i]));
        }
        return builder.ToString();
    }

    private static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        bool needsQuotes = value.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0;
        if (!needsQuotes)
            return value;

        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
}
