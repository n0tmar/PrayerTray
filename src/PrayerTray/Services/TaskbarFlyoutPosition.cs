using System.Drawing;
using System.Windows;
using PrayerTray.Native;

namespace PrayerTray.Services;

public static class TaskbarFlyoutPosition
{
    private const int SlotWidth = 60;
    private const int SlotHeight = 40;
    private const int GapPixels = 4;

    public static bool TryGetSlotAnchor(out Rectangle anchor)
    {
        anchor = Rectangle.Empty;

        if (!TaskbarInterop.TryGetWidgetScreenPosition(SlotWidth, SlotHeight, GapPixels, out var x, out var y))
        {
            return false;
        }

        anchor = new Rectangle(x, y, SlotWidth, SlotHeight);
        return true;
    }

    public static void PlaceAboveSlot(Window window, Rectangle anchor)
    {
        window.UpdateLayout();

        var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
        var height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;

        var left = anchor.Right - width;
        var top = anchor.Top - height - 8;

        var screen = System.Windows.Forms.Screen.FromRectangle(anchor);
        var work = screen.WorkingArea;

        if (width > work.Width - 16)
        {
            width = work.Width - 16;
        }

        if (height > work.Height - 16)
        {
            height = work.Height - 16;
        }

        if (left + width > work.Right - 8)
        {
            left = work.Right - width - 8;
        }

        if (left < work.Left + 8)
        {
            left = work.Left + 8;
        }

        if (top < work.Top + 8)
        {
            top = work.Top + 8;
        }

        if (top + height > work.Bottom - 8)
        {
            top = work.Bottom - height - 8;
        }

        if (top < work.Top + 8)
        {
            top = work.Top + 8;
        }

        window.Left = left;
        window.Top = top;
        FlyoutWindowHelper.SetScreenPosition(window, left, top);
    }
}
