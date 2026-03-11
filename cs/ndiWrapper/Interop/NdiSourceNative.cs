using System.Runtime.InteropServices;

namespace ndiWrapper.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NdiSourceNative
{
    public IntPtr NdiName;
    public IntPtr UrlAddress;
}
