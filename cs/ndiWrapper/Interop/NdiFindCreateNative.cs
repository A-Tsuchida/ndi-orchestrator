using System.Runtime.InteropServices;

namespace ndiWrapper.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NdiFindCreateNative
{
    [MarshalAs(UnmanagedType.U1)]
    public bool ShowLocalSources;
    public IntPtr Groups;
    public IntPtr ExtraIps;
}
