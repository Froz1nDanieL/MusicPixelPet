using System.Runtime.InteropServices;

namespace MusicPixelPet.Wpf.Services;

public static class SystemVolumeReader
{
    private static readonly Guid AudioEndpointVolumeInterfaceId = new("5cdf2c82-841e-4546-9722-0cf74078229a");
    private static readonly Guid DeviceEnumeratorClassId = new("bcde0395-e52f-467c-8e3d-c4579291692e");
    private const uint ClsctxAll = 0x17;

    public static double GetMasterVolumeLevel()
    {
        if (TryGetMasterVolumeLevel(ERole.Multimedia, out var level)
            || TryGetMasterVolumeLevel(ERole.Console, out level))
        {
            return level;
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
            return false;
        }
        finally
        {
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
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

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
