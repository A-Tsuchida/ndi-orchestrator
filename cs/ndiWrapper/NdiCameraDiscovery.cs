using System.Runtime.InteropServices;
using System.Text;
using ndiWrapper.Interop;

namespace ndiWrapper;

/// <summary>
/// Discovers NDI sources available on the network.
/// </summary>
public static class NdiCameraDiscovery
{
    /// <summary>
    /// Discovers NDI sources on the network, waiting up to <paramref name="timeout"/> for sources to appear.
    /// This call blocks the calling thread for the duration of the timeout.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for sources to appear.</param>
    /// <param name="showLocalSources">Whether to include sources running on the local machine.</param>
    /// <param name="groups">Comma-separated NDI group names to search in, or null for all groups.</param>
    /// <param name="extraIps">Comma-separated extra IP addresses to query (for sources not on the local subnet), or null to use the system registry.</param>
    public static IReadOnlyList<NdiSource> Discover(
        TimeSpan timeout,
        bool showLocalSources = true,
        string? groups = null,
        string? extraIps = null)
    {
        var groupsPtr = IntPtr.Zero;
        var extraIpsPtr = IntPtr.Zero;

        try
        {
            if (groups is not null)
                groupsPtr = StringToUtf8(groups);
            if (extraIps is not null)
                extraIpsPtr = StringToUtf8(extraIps);

            var createSettings = new NdiFindCreateNative
            {
                ShowLocalSources = showLocalSources,
                Groups = groupsPtr,
                ExtraIps = extraIpsPtr
            };

            var finder = NdiLib.FindCreate(ref createSettings);
            if (finder == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create NDI finder. Ensure the NDI 6 runtime is installed.");

            try
            {
                var timeoutMs = (uint)Math.Min(Math.Max(0L, (long)timeout.TotalMilliseconds), uint.MaxValue);
                NdiLib.FindWaitForSources(finder, timeoutMs);
                return ReadSources(finder);
            }
            finally
            {
                NdiLib.FindDestroy(finder);
            }
        }
        finally
        {
            if (groupsPtr != IntPtr.Zero) Marshal.FreeHGlobal(groupsPtr);
            if (extraIpsPtr != IntPtr.Zero) Marshal.FreeHGlobal(extraIpsPtr);
        }
    }

    /// <summary>
    /// Asynchronously discovers NDI sources on the network.
    /// </summary>
    public static Task<IReadOnlyList<NdiSource>> DiscoverAsync(
        TimeSpan timeout,
        bool showLocalSources = true,
        string? groups = null,
        string? extraIps = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Discover(timeout, showLocalSources, groups, extraIps), cancellationToken);
    }

    private static List<NdiSource> ReadSources(IntPtr finder)
    {
        var sourcesPtr = NdiLib.FindGetCurrentSources(finder, out uint count);
        var sourceSize = Marshal.SizeOf<NdiSourceNative>();
        var sources = new List<NdiSource>((int)count);

        for (int i = 0; i < (int)count; i++)
        {
            var raw = Marshal.PtrToStructure<NdiSourceNative>(sourcesPtr + i * sourceSize);
            var name = Utf8ToString(raw.NdiName) ?? string.Empty;
            var url = raw.UrlAddress != IntPtr.Zero ? Utf8ToString(raw.UrlAddress) : null;
            sources.Add(new NdiSource(name, url));
        }

        return sources;
    }

    private static IntPtr StringToUtf8(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var ptr = Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        Marshal.WriteByte(ptr, bytes.Length, 0);
        return ptr;
    }

    private static string? Utf8ToString(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return null;
        int len = 0;
        while (Marshal.ReadByte(ptr, len) != 0) len++;
        if (len == 0) return string.Empty;
        var buffer = new byte[len];
        Marshal.Copy(ptr, buffer, 0, len);
        return Encoding.UTF8.GetString(buffer);
    }
}
