using System.Runtime.InteropServices;

namespace MusicPixelPet.Wpf.Services;

public static class VolumeController
{
    private const byte VolumeUpKey = 0xAF;
    private const byte VolumeDownKey = 0xAE;
    private const byte MediaNextTrackKey = 0xB0;
    private const byte MediaPreviousTrackKey = 0xB1;
    private const byte MediaPlayPauseKey = 0xB3;
    private const uint KeyUpFlag = 0x0002;

    public static void AdjustMasterVolume(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        var key = delta > 0 ? VolumeUpKey : VolumeDownKey;
        var steps = Math.Max(1, Math.Abs(delta));

        for (var index = 0; index < steps; index += 1)
        {
            PressKey(key);
        }
    }

    public static void TogglePlayPause()
    {
        PressKey(MediaPlayPauseKey);
    }

    public static void SkipNext()
    {
        PressKey(MediaNextTrackKey);
    }

    public static void SkipPrevious()
    {
        PressKey(MediaPreviousTrackKey);
    }

    private static void PressKey(byte key)
    {
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, KeyUpFlag, UIntPtr.Zero);
    }

    [DllImport("user32.dll", SetLastError = false)]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
}
