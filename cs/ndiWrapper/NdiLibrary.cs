using ndiWrapper.Interop;

namespace ndiWrapper;

/// <summary>
/// Manages the NDI library lifecycle. Calling <see cref="Initialize"/> is optional but recommended
/// for slightly better performance. If not called, the NDI runtime will still be loaded on first use.
/// </summary>
public static class NdiLibrary
{
    private static bool _initialized;
    private static readonly Lock _lock = new();

    /// <summary>
    /// Initializes the NDI runtime. Returns false if the CPU does not support NDI (requires SSE4.2).
    /// Safe to call multiple times — subsequent calls are no-ops.
    /// </summary>
    public static bool Initialize()
    {
        lock (_lock)
        {
            if (_initialized) return true;
            _initialized = NdiLib.Initialize();
            return _initialized;
        }
    }

    /// <summary>
    /// Shuts down the NDI runtime. Call this once when your application exits.
    /// All <see cref="NdiPtzCamera"/> instances should be disposed before calling this.
    /// </summary>
    public static void Shutdown()
    {
        lock (_lock)
        {
            if (!_initialized) return;
            NdiLib.Destroy();
            _initialized = false;
        }
    }
}
