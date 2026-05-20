using System.Diagnostics;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace MagnifierAutomation.Tests;

public sealed class MagnifierTests
{
    private const int WaitTimeoutSeconds = 10;
    private const string MagnifierRegistryPath = @"Software\Microsoft\ScreenMagnifier";

    private Process? _magnifierProcess;

    [SetUp]
    public void OpenMagnifier()
    {
        _magnifierProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "magnify.exe",
            UseShellExecute = true
        });

        WaitUntil(
            () => Process.GetProcessesByName("Magnify").Length > 0 && ReadMagnificationPercent() > 0,
            "Лупа не запустилась или не записала текущий масштаб в системные настройки.");
    }

    [TearDown]
    public void CloseMagnifier()
    {
        PressWinEscape();

        foreach (var process in Process.GetProcessesByName("Magnify"))
        {
            process.CloseMainWindow();

            if (!process.WaitForExit(2_000))
            {
                process.Kill();
            }

            process.Dispose();
        }

        _magnifierProcess?.Dispose();
    }

    [Test]
    public void ZoomInShortcut_IncreasesMagnifierZoomLevel()
    {
        var initialZoom = EnsureZoomCanBeIncreased();

        PressWinPlus();

        var increasedZoom = WaitForMagnification(zoom => zoom > initialZoom);

        Assert.That(increasedZoom, Is.GreaterThan(initialZoom));
    }

    private static int EnsureZoomCanBeIncreased()
    {
        var currentZoom = ReadMagnificationPercent();
        if (currentZoom < 1_600)
        {
            return currentZoom;
        }

        PressWinMinus();
        return WaitForMagnification(zoom => zoom < currentZoom);
    }

    private static int ReadMagnificationPercent()
    {
        using var key = Registry.CurrentUser.OpenSubKey(MagnifierRegistryPath);
        var value = key?.GetValue("Magnification");

        return value is int magnification ? magnification : 0;
    }

    private static int WaitForMagnification(Func<int, bool> condition)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(WaitTimeoutSeconds);

        while (DateTime.UtcNow < timeoutAt)
        {
            var zoom = ReadMagnificationPercent();
            if (condition(zoom))
            {
                return zoom;
            }

            Thread.Sleep(250);
        }

        Assert.Fail($"Масштаб Лупы не изменился за {WaitTimeoutSeconds} секунд. Текущее значение: {ReadMagnificationPercent()}%.");
        return 0;
    }

    private static void WaitUntil(Func<bool> condition, string failureMessage)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(WaitTimeoutSeconds);

        while (DateTime.UtcNow < timeoutAt)
        {
            if (condition())
            {
                return;
            }

            Thread.Sleep(250);
        }

        Assert.Fail(failureMessage);
    }

    private static void PressWinPlus()
    {
        KeyDown(VirtualKey.LeftWindows);
        KeyDown(VirtualKey.Plus);
        KeyUp(VirtualKey.Plus);
        KeyUp(VirtualKey.LeftWindows);
    }

    private static void PressWinMinus()
    {
        KeyDown(VirtualKey.LeftWindows);
        KeyDown(VirtualKey.Minus);
        KeyUp(VirtualKey.Minus);
        KeyUp(VirtualKey.LeftWindows);
    }

    private static void PressWinEscape()
    {
        KeyDown(VirtualKey.LeftWindows);
        KeyDown(VirtualKey.Escape);
        KeyUp(VirtualKey.Escape);
        KeyUp(VirtualKey.LeftWindows);
    }

    private static void KeyDown(byte keyCode) =>
        SendKeyboardInput(keyCode, 0);

    private static void KeyUp(byte keyCode) =>
        SendKeyboardInput(keyCode, KeyEvent.KeyUp);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    private static void SendKeyboardInput(byte keyCode, uint flags)
    {
        var input = new Input
        {
            Type = InputType.Keyboard,
            Data = new InputUnion
            {
                KeyboardInput = new KeyboardInput
                {
                    VirtualKey = keyCode,
                    Flags = flags
                }
            }
        };

        var sent = SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Input)));
        Assert.That(sent, Is.EqualTo(1), $"Не удалось отправить клавишу 0x{keyCode:X2} через SendInput. Win32Error={Marshal.GetLastWin32Error()}");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput MouseInput;

        [FieldOffset(0)]
        public KeyboardInput KeyboardInput;

        [FieldOffset(0)]
        public HardwareInput HardwareInput;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParamLow;
        public ushort ParamHigh;
    }
    private static class InputType
    {
        public const uint Keyboard = 1;
    }

    private static class VirtualKey
    {
        public const byte LeftWindows = 0x5B;
        public const byte Escape = 0x1B;
        public const byte Plus = 0x6B;
        public const byte Minus = 0xBD;
    }

    private static class KeyEvent
    {
        public const uint KeyUp = 0x0002;
    }
}
