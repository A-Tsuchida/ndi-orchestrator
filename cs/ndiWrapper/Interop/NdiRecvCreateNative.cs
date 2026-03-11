using System.Runtime.InteropServices;

namespace ndiWrapper.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NdiRecvCreateNative
{
    public NdiSourceNative Source;
    public int ColorFormat;
    public int Bandwidth;
    [MarshalAs(UnmanagedType.U1)]
    public bool AllowVideoFields;
    public IntPtr RecvName;
}
