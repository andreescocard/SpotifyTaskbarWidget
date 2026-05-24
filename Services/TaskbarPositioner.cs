using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace SpotifyTaskbarWidget.Services;

public static class TaskbarPositioner
{
    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    public static void PositionWindow(Window window)
    {
        try
        {
            var trayWnd = FindWindow("Shell_TrayWnd", null);
            if (trayWnd == IntPtr.Zero) { PositionFallback(window); return; }

            var trayNotify = FindWindowEx(trayWnd, IntPtr.Zero, "TrayNotifyWnd", null);

            GetWindowRect(trayWnd, out var taskbarRect);

            double dpi = GetDpiScale(window);
            double taskbarLeft = taskbarRect.Left / dpi;
            double taskbarTop = taskbarRect.Top / dpi;
            double taskbarBottom = taskbarRect.Bottom / dpi;
            double taskbarHeight = taskbarBottom - taskbarTop;

            double trayLeft;
            if (trayNotify != IntPtr.Zero && GetWindowRect(trayNotify, out var trayRect))
                trayLeft = trayRect.Left / dpi;
            else
                trayLeft = taskbarRect.Right / dpi;

            double widgetWidth = window.Width;
            double widgetHeight = window.Height;

            double left = trayLeft - widgetWidth - 8;
            double top = taskbarTop + (taskbarHeight - widgetHeight) / 2.0;

            if (left < taskbarLeft) left = taskbarLeft;

            window.Left = left;
            window.Top = top;
        }
        catch
        {
            PositionFallback(window);
        }
    }

    private static void PositionFallback(Window window)
    {
        var area = SystemParameters.WorkArea;
        window.Left = area.Right - window.Width - 16;
        window.Top = area.Bottom - window.Height - 8;
    }

    private static double GetDpiScale(Window window)
    {
        try
        {
            var source = PresentationSource.FromVisual(window);
            if (source?.CompositionTarget != null)
                return source.CompositionTarget.TransformToDevice.M11;
        }
        catch { }
        return 1.0;
    }
}
