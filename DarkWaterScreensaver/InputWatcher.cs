using System.Runtime.InteropServices;

namespace DarkWaterScreensaver;

/// <summary>
/// Globale Low-Level-Hooks für Tastatur und Maus (nur im /s-Modus aktiv).
/// Der Exit wird damit auf OS-Ebene abgefangen, unabhängig davon, ob das
/// WebView2-Child-Fenster den Input schluckt. Mausbewegungen unterhalb der
/// Toleranzschwelle (Sensor-Rauschen) beenden den Screensaver nicht.
/// </summary>
internal static class InputWatcher
{
    private const int MouseMoveTolerancePx = 10;

    private static IntPtr _keyboardHook;
    private static IntPtr _mouseHook;
    private static Win32.HookProc? _keyboardProc;
    private static Win32.HookProc? _mouseProc;
    private static Win32.POINT? _origin;
    private static Action? _onInput;

    public static void Start(Action onInput)
    {
        _onInput = onInput;
        _keyboardProc = KeyboardHook;
        _mouseProc = MouseHook;
        _keyboardHook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _keyboardProc, IntPtr.Zero, 0);
        _mouseHook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _mouseProc, IntPtr.Zero, 0);
    }

    public static void Stop()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
        _onInput = null;
    }

    private static IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt64();
            if (msg is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN)
                Trigger();
        }
        return Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private static IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = (int)wParam.ToInt64();
            if (msg == Win32.WM_MOUSEMOVE)
            {
                var data = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
                if (_origin is null)
                {
                    _origin = data.pt;
                }
                else if (Math.Abs(data.pt.X - _origin.Value.X) > MouseMoveTolerancePx ||
                         Math.Abs(data.pt.Y - _origin.Value.Y) > MouseMoveTolerancePx)
                {
                    Trigger();
                }
            }
            else if (msg is Win32.WM_LBUTTONDOWN or Win32.WM_RBUTTONDOWN or Win32.WM_MBUTTONDOWN
                     or Win32.WM_XBUTTONDOWN or Win32.WM_MOUSEWHEEL or Win32.WM_MOUSEHWHEEL)
            {
                Trigger();
            }
        }
        return Win32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private static void Trigger()
    {
        var handler = _onInput;
        _onInput = null;
        handler?.Invoke();
    }
}
