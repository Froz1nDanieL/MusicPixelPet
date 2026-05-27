using System.Runtime.InteropServices;

namespace MusicPixelPet.Helper;

// 只负责读取系统主音量，避免和媒体键控制逻辑耦合。
internal static class SystemVolumeReader
{
    // Core Audio COM 接口 ID，使用默认输出设备读取主音量。
    private static readonly Guid AudioEndpointVolumeInterfaceId = new("5cdf2c82-841e-4546-9722-0cf74078229a");
    private static readonly Guid DeviceEnumeratorClassId = new("bcde0395-e52f-467c-8e3d-c4579291692e");
    private const uint ClsctxAll = 0x17;

    public static double GetMasterVolumeLevel()
    {
        // Multimedia 是常用播放角色；Console 作为兼容兜底。
        if (TryGetMasterVolumeLevel(ERole.Multimedia, out var volumeLevel)
            || TryGetMasterVolumeLevel(ERole.Console, out volumeLevel))
        {
            return volumeLevel;
        }

        return 0;
    }

    private static bool TryGetMasterVolumeLevel(ERole role, out double level)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioEndpointVolume? endpointVolume = null;
        level = 0;

        try
        {
            // 通过 MMDeviceEnumerator 获取当前默认渲染设备。
            var enumeratorType = Type.GetTypeFromCLSID(DeviceEnumeratorClassId);

            if (enumeratorType is null)
            {
                return false;
            }

            enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType)!;
            ThrowIfFailed(enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, role, out device));
            ThrowIfFailed(device.Activate(AudioEndpointVolumeInterfaceId, ClsctxAll, IntPtr.Zero, out var endpointVolumeObject));
            endpointVolume = (IAudioEndpointVolume)endpointVolumeObject;
            ThrowIfFailed(endpointVolume.GetMasterVolumeLevelScalar(out var volumeLevel));
            level = Math.Clamp(volumeLevel, 0, 1);

            return true;
        }
        catch
        {
            // 音量读取失败不应影响媒体信息展示。
            return false;
        }
        finally
        {
            // COM 对象需要显式释放，避免 Helper 长驻时泄漏。
            ReleaseComObject(endpointVolume);
            ReleaseComObject(device);
            ReleaseComObject(enumerator);
        }
    }

    private static void ReleaseComObject(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }

    private static void ThrowIfFailed(int result)
    {
        // Core Audio 以 HRESULT 表示错误，负值代表失败。
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    // 以下接口只声明当前读取音量所需的最小 COM 成员。
    private enum EDataFlow
    {
        Render,
        Capture,
        All
    }

    private enum ERole
    {
        Console,
        Multimedia,
        Communications
    }

    [ComImport]
    [Guid("a95664d2-9614-4f35-a746-de8db63617e6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out object devices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    }

    [ComImport]
    [Guid("d666063f-1587-4e43-81f1-b948e807363f")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(
            [MarshalAs(UnmanagedType.LPStruct)] Guid interfaceId,
            uint classContext,
            IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    }

    [ComImport]
    [Guid("5cdf2c82-841e-4546-9722-0cf74078229a")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out uint channelCount);
        int SetMasterVolumeLevel(float level, Guid eventContext);
        int SetMasterVolumeLevelScalar(float level, Guid eventContext);
        int GetMasterVolumeLevel(out float level);
        int GetMasterVolumeLevelScalar(out float level);
    }
}
