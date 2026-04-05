using System.Runtime.InteropServices;
using System.Text;
using ndiWrapper.Interop;

namespace ndiWrapper;

/// <summary>
/// Controls an NDI PTZ camera. Connect to a source, then issue pan/tilt/zoom/focus commands and manage presets.
/// Dispose when done to release the NDI receiver.
/// </summary>
public sealed class NdiPtzCamera : IDisposable
{
    // NDIlib_recv_bandwidth_e: metadata only (-10) — no video/audio data, lowest network load
    private const int BandwidthMetadataOnly = -10;
    private const int ColorFormatUyvyBgra = 1;
    private const int FrameTypeMetadata = 3;

    private IntPtr _recv;
    private bool _disposed;

    /// <summary>The name of the NDI source this camera is connected to.</summary>
    public string SourceName { get; }

    /// <param name="source">The NDI source to connect to.</param>
    /// <param name="receiverName">Optional name for this receiver as it appears on the network.</param>
    public NdiPtzCamera(NdiSource source, string? receiverName = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        SourceName = source.Name;

        var namePtr = IntPtr.Zero;
        var urlPtr = IntPtr.Zero;
        var recvNamePtr = IntPtr.Zero;

        try
        {
            namePtr = StringToUtf8(source.Name);
            if (source.UrlAddress is not null)
                urlPtr = StringToUtf8(source.UrlAddress);
            if (receiverName is not null)
                recvNamePtr = StringToUtf8(receiverName);

            var settings = new NdiRecvCreateNative
            {
                Source = new NdiSourceNative
                {
                    NdiName = namePtr,
                    UrlAddress = urlPtr
                },
                ColorFormat = ColorFormatUyvyBgra,
                Bandwidth = BandwidthMetadataOnly,
                AllowVideoFields = false,
                RecvName = recvNamePtr
            };

            _recv = NdiLib.RecvCreate(ref settings);
        }
        finally
        {
            if (namePtr != IntPtr.Zero) Marshal.FreeHGlobal(namePtr);
            if (urlPtr != IntPtr.Zero) Marshal.FreeHGlobal(urlPtr);
            if (recvNamePtr != IntPtr.Zero) Marshal.FreeHGlobal(recvNamePtr);
        }

        if (_recv == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to create NDI receiver for source '{source.Name}'.");
    }

    /// <summary>
    /// Whether the connected camera supports PTZ control.
    /// Note: this may return false for a few seconds after connecting while the camera capability is detected.
    /// Use <see cref="WaitForPtzSupport"/> to wait for the capability to be confirmed.
    /// </summary>
    public bool IsPtzSupported
    {
        get { ThrowIfDisposed(); return NdiLib.PtzIsSupported(_recv); }
    }

    /// <summary>Number of active connections to the source (normally 0 or 1).</summary>
    public int ConnectionCount
    {
        get { ThrowIfDisposed(); return NdiLib.RecvGetNoConnections(_recv); }
    }

    /// <summary>
    /// Blocks until PTZ support is confirmed or the timeout expires.
    /// Returns true if PTZ is supported, false if the timeout elapsed first.
    /// </summary>
    public bool WaitForPtzSupport(TimeSpan timeout)
    {
        ThrowIfDisposed();
        var deadline = DateTime.UtcNow + timeout;
        var frame = new NdiMetadataFrameNative();

        while (DateTime.UtcNow < deadline)
        {
            if (NdiLib.PtzIsSupported(_recv))
                return true;

            var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
            if (remaining <= 0) break;

            var frameType = NdiLib.RecvCaptureMetadata(_recv, IntPtr.Zero, IntPtr.Zero, ref frame, (uint)Math.Min(remaining, 200));
            if (frameType == FrameTypeMetadata && frame.Data != IntPtr.Zero)
                NdiLib.RecvFreeMetadata(_recv, ref frame);
        }

        return NdiLib.PtzIsSupported(_recv);
    }

    // -------------------------------------------------------------------------
    // Pan / Tilt
    // -------------------------------------------------------------------------

    /// <summary>Moves to an absolute pan/tilt position.</summary>
    /// <param name="pan">-1.0 (full left) … 0.0 (centre) … +1.0 (full right)</param>
    /// <param name="tilt">-1.0 (full down) … 0.0 (centre) … +1.0 (full up)</param>
    public bool PanTilt(float pan, float tilt)
    {
        ThrowIfDisposed();
        return NdiLib.PtzPanTilt(_recv, pan, tilt);
    }

    /// <summary>Moves the camera at the specified pan/tilt speed. Send 0,0 to stop.</summary>
    /// <param name="panSpeed">-1.0 (moving right) … 0.0 (stopped) … +1.0 (moving left)</param>
    /// <param name="tiltSpeed">-1.0 (moving down) … 0.0 (stopped) … +1.0 (moving up)</param>
    public bool PanTiltSpeed(float panSpeed, float tiltSpeed)
    {
        ThrowIfDisposed();
        return NdiLib.PtzPanTiltSpeed(_recv, panSpeed, tiltSpeed);
    }

    // -------------------------------------------------------------------------
    // Zoom
    // -------------------------------------------------------------------------

    /// <summary>Sets zoom to an absolute value.</summary>
    /// <param name="zoom">0.0 (zoomed in / telephoto) … 1.0 (zoomed out / wide)</param>
    public bool Zoom(float zoom)
    {
        ThrowIfDisposed();
        return NdiLib.PtzZoom(_recv, zoom);
    }

    /// <summary>Zooms at the specified continuous speed. Send 0.0 to stop.</summary>
    /// <param name="speed">-1.0 (zoom out) … 0.0 (stopped) … +1.0 (zoom in)</param>
    public bool ZoomSpeed(float speed)
    {
        ThrowIfDisposed();
        return NdiLib.PtzZoomSpeed(_recv, speed);
    }

    // -------------------------------------------------------------------------
    // Focus
    // -------------------------------------------------------------------------

    /// <summary>Sets focus to an absolute value.</summary>
    /// <param name="focus">0.0 (focused to infinity) … 1.0 (focused as close as possible)</param>
    public bool Focus(float focus)
    {
        ThrowIfDisposed();
        return NdiLib.PtzFocus(_recv, focus);
    }

    /// <summary>Adjusts focus at the specified continuous speed. Send 0.0 to stop.</summary>
    /// <param name="speed">-1.0 (focus out / far) … 0.0 (stopped) … +1.0 (focus in / near)</param>
    public bool FocusSpeed(float speed)
    {
        ThrowIfDisposed();
        return NdiLib.PtzFocusSpeed(_recv, speed);
    }

    /// <summary>Switches the camera to auto-focus mode.</summary>
    public bool AutoFocus()
    {
        ThrowIfDisposed();
        return NdiLib.PtzAutoFocus(_recv);
    }

    // -------------------------------------------------------------------------
    // Presets
    // -------------------------------------------------------------------------

    /// <summary>Saves the current pan, tilt, zoom and focus into a preset slot.</summary>
    /// <param name="presetNo">Preset slot number 0–99.</param>
    public bool StorePreset(int presetNo)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(presetNo);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(presetNo, 99);
        return NdiLib.PtzStorePreset(_recv, presetNo);
    }

    /// <summary>Moves the camera to a previously stored preset.</summary>
    /// <param name="presetNo">Preset slot number 0–99.</param>
    /// <param name="speed">Movement speed: 0.0 (slowest) … 1.0 (fastest).</param>
    public bool RecallPreset(int presetNo, float speed = 1.0f)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(presetNo);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(presetNo, 99);
        return NdiLib.PtzRecallPreset(_recv, presetNo, speed);
    }

    // -------------------------------------------------------------------------
    // White Balance
    // -------------------------------------------------------------------------

    /// <summary>Switches to automatic white balance.</summary>
    public bool WhiteBalanceAuto() { ThrowIfDisposed(); return NdiLib.PtzWhiteBalanceAuto(_recv); }

    /// <summary>Switches to indoor (tungsten) white balance preset.</summary>
    public bool WhiteBalanceIndoor() { ThrowIfDisposed(); return NdiLib.PtzWhiteBalanceIndoor(_recv); }

    /// <summary>Switches to outdoor (daylight) white balance preset.</summary>
    public bool WhiteBalanceOutdoor() { ThrowIfDisposed(); return NdiLib.PtzWhiteBalanceOutdoor(_recv); }

    /// <summary>Captures a one-shot white balance using the current scene brightness.</summary>
    public bool WhiteBalanceOneShot() { ThrowIfDisposed(); return NdiLib.PtzWhiteBalanceOneShot(_recv); }

    /// <summary>Sets manual white balance using red/blue channel values.</summary>
    /// <param name="red">0.0 (no red) … 1.0 (maximum red)</param>
    /// <param name="blue">0.0 (no blue) … 1.0 (maximum blue)</param>
    public bool WhiteBalanceManual(float red, float blue)
    {
        ThrowIfDisposed();
        return NdiLib.PtzWhiteBalanceManual(_recv, red, blue);
    }

    // -------------------------------------------------------------------------
    // Exposure
    // -------------------------------------------------------------------------

    /// <summary>Switches to automatic exposure.</summary>
    public bool ExposureAuto() { ThrowIfDisposed(); return NdiLib.PtzExposureAuto(_recv); }

    /// <summary>Sets a single manual exposure level.</summary>
    /// <param name="level">0.0 (darkest) … 1.0 (brightest)</param>
    public bool ExposureManual(float level)
    {
        ThrowIfDisposed();
        return NdiLib.PtzExposureManual(_recv, level);
    }

    /// <summary>Sets full manual exposure with independent iris, gain and shutter speed.</summary>
    /// <param name="iris">0.0 (closed / dark) … 1.0 (open / bright)</param>
    /// <param name="gain">0.0 (minimum gain) … 1.0 (maximum gain)</param>
    /// <param name="shutterSpeed">0.0 (slow shutter) … 1.0 (fast shutter)</param>
    public bool ExposureManual(float iris, float gain, float shutterSpeed)
    {
        ThrowIfDisposed();
        return NdiLib.PtzExposureManualV2(_recv, iris, gain, shutterSpeed);
    }

    // -------------------------------------------------------------------------
    // Camera state / metadata
    // -------------------------------------------------------------------------

    /// <summary>
    /// Attempts to receive a metadata XML frame from the camera within the given timeout.
    /// Some NDI PTZ cameras report their current state (pan, tilt, zoom, focus) through metadata.
    /// Returns null if no metadata frame was received within the timeout.
    /// </summary>
    /// <param name="timeoutMs">Maximum milliseconds to wait for a frame.</param>
    public string? TryReceiveMetadata(int timeoutMs = 100)
    {
        ThrowIfDisposed();
        var frame = new NdiMetadataFrameNative();
        var frameType = NdiLib.RecvCaptureMetadata(_recv, IntPtr.Zero, IntPtr.Zero, ref frame, (uint)timeoutMs);

        if (frameType != FrameTypeMetadata || frame.Data == IntPtr.Zero)
            return null;

        try
        {
            return Utf8ToString(frame.Data);
        }
        finally
        {
            NdiLib.RecvFreeMetadata(_recv, ref frame);
        }
    }

    /// <summary>
    /// Sends an XML metadata string to the camera.
    /// Can be used to request the current camera state from cameras that support such queries.
    /// </summary>
    public bool SendMetadata(string xml)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(xml);

        var dataPtr = StringToUtf8(xml);
        try
        {
            var frame = new NdiMetadataFrameNative { Data = dataPtr };
            return NdiLib.RecvSendMetadata(_recv, ref frame);
        }
        finally
        {
            Marshal.FreeHGlobal(dataPtr);
        }
    }

    // -------------------------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_recv != IntPtr.Zero)
        {
            NdiLib.RecvDestroy(_recv);
            _recv = IntPtr.Zero;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

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
