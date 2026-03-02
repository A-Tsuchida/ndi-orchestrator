#pragma once

#include <cstdint>
#include <memory>
#include <string>
#include <vector>

namespace PtzControl {

// Describes an NDI source found on the network.
struct CameraSource {
    std::string name;  // Human-readable name, e.g. "MACHINE (Camera 1)"
    std::string url;   // Network address returned by the NDI finder
};

// Current PTZ state reported by a camera.
struct PtzState {
    float pan   = 0.0f;  // -1.0 (left)   ... 0.0 (centered) ... +1.0 (right)
    float tilt  = 0.0f;  // -1.0 (bottom) ... 0.0 (centered) ... +1.0 (top)
    float zoom  = 0.0f;  // 0.0 (zoomed in)       ... 1.0 (zoomed out)
    float focus = 0.0f;  // 0.0 (focused to infinity) ... 1.0 (focused close)
};

// Discovers NDI sources visible on the local network (and optionally on
// extra subnets).  Create one instance and call discover() as often as needed.
class CameraDiscovery {
public:
    // extra_ips : optional comma-separated list of IP addresses outside the
    //             local subnet to include in the search (e.g. "192.168.2.10").
    // groups    : optional NDI group filter string; NULL means all groups.
    explicit CameraDiscovery(const char* extra_ips = nullptr,
                             const char* groups    = nullptr);
    ~CameraDiscovery();

    // Blocks for up to timeout_ms waiting for sources to appear, then returns
    // all currently visible NDI sources.
    std::vector<CameraSource> discover(uint32_t timeout_ms = 2000);

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

// Controls a single NDI PTZ camera.
//
// The constructor connects a metadata-only receiver to the given source.
// Use CameraDiscovery::discover() to obtain a valid CameraSource.
//
// Requires the NDI 6 runtime DLL (Processing.NDI.Lib.x64.dll) to be
// installed on the host machine (available from https://ndi.video/).
// The consumer project must also add the NDI SDK Lib directory to its
// Additional Library Directories:
//   C:\Program Files\NDI\NDI 6 SDK\Lib\x64   (x64 builds)
//   C:\Program Files\NDI\NDI 6 SDK\Lib\x86   (Win32 builds)
class PtzCamera {
public:
    explicit PtzCamera(const CameraSource& source);
    ~PtzCamera();

    // Returns true once the camera has announced PTZ capability.
    // This may return false for 1-2 seconds right after construction while
    // the NDI session is being established.
    bool isPtzSupported() const;

    // Queries the current PTZ state.  Blocks until the camera responds or
    // timeout_ms elapses.  Returns false when the camera does not reply or
    // does not support state reporting.
    bool getState(PtzState& out_state, uint32_t timeout_ms = 2000);

    // ---- Absolute positioning ------------------------------------------------
    // pan:  -1.0 (left)   ... 0.0 (center) ... +1.0 (right)
    // tilt: -1.0 (bottom) ... 0.0 (center) ... +1.0 (top)
    bool setPanTilt(float pan, float tilt);

    // zoom: 0.0 (zoomed in) ... 1.0 (zoomed out)
    bool setZoom(float zoom);

    // focus: 0.0 (focused to infinity) ... 1.0 (focused as close as possible)
    bool setFocus(float focus);

    // Engage auto-focus.
    bool setAutoFocus();

    // ---- Speed-based / continuous movement ----------------------------------
    // Pass 0.0 to stop.
    // pan_speed:   -1.0 (moving right) ... 0.0 (stopped) ... +1.0 (moving left)
    // tilt_speed:  -1.0 (moving down)  ... 0.0 (stopped) ... +1.0 (moving up)
    bool setPanTiltSpeed(float pan_speed, float tilt_speed);

    // zoom_speed:  -1.0 (zoom out) ... 0.0 (stopped) ... +1.0 (zoom in)
    bool setZoomSpeed(float zoom_speed);

    // focus_speed: -1.0 (towards infinity) ... 0.0 (stopped) ... +1.0 (towards close)
    bool setFocusSpeed(float focus_speed);

    // ---- Presets (index 0 .. 99) --------------------------------------------
    bool storePreset(int preset_no);
    // speed: 0.0 (as slow as possible) ... 1.0 (as fast as possible)
    bool recallPreset(int preset_no, float speed = 1.0f);

    // ---- White balance -------------------------------------------------------
    bool setAutoWhiteBalance();
    bool setIndoorWhiteBalance();
    bool setOutdoorWhiteBalance();
    bool setOneshotWhiteBalance();
    // red:  0.0 (no red)  ... 1.0 (very red)
    // blue: 0.0 (no blue) ... 1.0 (very blue)
    bool setManualWhiteBalance(float red, float blue);

    // ---- Exposure -----------------------------------------------------------
    bool setAutoExposure();
    // level: 0.0 (dark) ... 1.0 (light)
    bool setManualExposure(float level);
    // iris / gain / shutter_speed: all 0.0 (dark/slow) ... 1.0 (light/fast)
    bool setManualExposureV2(float iris, float gain, float shutter_speed);

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace PtzControl
