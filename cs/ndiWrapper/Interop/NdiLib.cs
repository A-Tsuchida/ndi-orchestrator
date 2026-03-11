using System.Reflection;
using System.Runtime.InteropServices;

namespace ndiWrapper.Interop;

internal static class NdiLib
{
    private const string LibraryName = "NDIPTZLib";

    static NdiLib()
    {
        NativeLibrary.SetDllImportResolver(typeof(NdiLib).Assembly, ResolveLibrary);
    }

    private static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibraryName)
            return IntPtr.Zero;

        var dllName = IntPtr.Size == 8 ? "Processing.NDI.Lib.x64.dll" : "Processing.NDI.Lib.x86.dll";

        var runtimeDir = Environment.GetEnvironmentVariable("NDI_RUNTIME_DIR_V6");
        if (runtimeDir is not null)
        {
            var fullPath = Path.Combine(runtimeDir, dllName);
            if (NativeLibrary.TryLoad(fullPath, out var handle))
                return handle;
        }

        if (NativeLibrary.TryLoad(dllName, assembly, searchPath, out var fallback))
            return fallback;

        return IntPtr.Zero;
    }

    // Library lifecycle
    [DllImport(LibraryName, EntryPoint = "NDIlib_initialize", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool Initialize();

    [DllImport(LibraryName, EntryPoint = "NDIlib_destroy", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Destroy();

    // Find API
    [DllImport(LibraryName, EntryPoint = "NDIlib_find_create_v2", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr FindCreate(ref NdiFindCreateNative createSettings);

    [DllImport(LibraryName, EntryPoint = "NDIlib_find_destroy", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void FindDestroy(IntPtr instance);

    [DllImport(LibraryName, EntryPoint = "NDIlib_find_get_current_sources", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr FindGetCurrentSources(IntPtr instance, out uint noSources);

    [DllImport(LibraryName, EntryPoint = "NDIlib_find_wait_for_sources", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool FindWaitForSources(IntPtr instance, uint timeoutMs);

    // Recv API
    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_create_v3", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr RecvCreate(ref NdiRecvCreateNative createSettings);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_destroy", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void RecvDestroy(IntPtr instance);

    // video/audio passed as IntPtr.Zero (NULL) since we use metadata-only bandwidth
    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_capture_v2", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int RecvCaptureMetadata(IntPtr instance, IntPtr videoData, IntPtr audioData, ref NdiMetadataFrameNative metadata, uint timeoutMs);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_free_metadata", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void RecvFreeMetadata(IntPtr instance, ref NdiMetadataFrameNative metadata);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_send_metadata", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool RecvSendMetadata(IntPtr instance, ref NdiMetadataFrameNative metadata);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_get_no_connections", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int RecvGetNoConnections(IntPtr instance);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_free_string", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void RecvFreeString(IntPtr instance, IntPtr str);

    // PTZ
    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_is_supported", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzIsSupported(IntPtr instance);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_zoom", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzZoom(IntPtr instance, float zoomValue);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_zoom_speed", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzZoomSpeed(IntPtr instance, float zoomSpeed);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_pan_tilt", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzPanTilt(IntPtr instance, float panValue, float tiltValue);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_pan_tilt_speed", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzPanTiltSpeed(IntPtr instance, float panSpeed, float tiltSpeed);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_store_preset", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzStorePreset(IntPtr instance, int presetNo);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_recall_preset", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzRecallPreset(IntPtr instance, int presetNo, float speed);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_auto_focus", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzAutoFocus(IntPtr instance);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_focus", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzFocus(IntPtr instance, float focusValue);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_focus_speed", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzFocusSpeed(IntPtr instance, float focusSpeed);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_white_balance_auto", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzWhiteBalanceAuto(IntPtr instance);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_white_balance_indoor", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzWhiteBalanceIndoor(IntPtr instance);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_white_balance_outdoor", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzWhiteBalanceOutdoor(IntPtr instance);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_white_balance_oneshot", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzWhiteBalanceOneShot(IntPtr instance);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_white_balance_manual", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzWhiteBalanceManual(IntPtr instance, float red, float blue);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_exposure_auto", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzExposureAuto(IntPtr instance);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_exposure_manual", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzExposureManual(IntPtr instance, float exposureLevel);

    [DllImport(LibraryName, EntryPoint = "NDIlib_recv_ptz_exposure_manual_v2", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool PtzExposureManualV2(IntPtr instance, float iris, float gain, float shutterSpeed);
}
