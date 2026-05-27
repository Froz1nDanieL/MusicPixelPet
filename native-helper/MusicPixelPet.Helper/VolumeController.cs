using System.Runtime.InteropServices;

namespace MusicPixelPet.Helper;

// 负责发送系统媒体键和音量键，不直接依赖具体播放器。
internal static class VolumeController
{
    // Windows 虚拟键码：音量控制和媒体播放控制。
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
        // delta 代表按键次数，保持和前端滚轮步进一致。
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
        // keybd_event 需要先按下再抬起，播放器才会收到完整按键。
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, KeyUpFlag, UIntPtr.Zero);
    }

    // 旧 API 但兼容性好，适合发送全局媒体键。
    [DllImport("user32.dll", SetLastError = false)]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
}
