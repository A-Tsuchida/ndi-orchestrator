using System.Runtime.InteropServices;

namespace ndiWrapper.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NdiMetadataFrameNative
{
    public int Length;
    public long Timecode;
    public IntPtr Data;
}
