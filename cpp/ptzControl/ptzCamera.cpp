#include "pch.h"
#include "ptzCamera.h"

#include <Processing.NDI.Lib.h>

#ifdef _WIN64
#pragma comment(lib, "Processing.NDI.Lib.x64.lib")
#else
#pragma comment(lib, "Processing.NDI.Lib.x86.lib")
#endif

#include <algorithm>
#include <cctype>
#include <chrono>
#include <cstdlib>

namespace PtzControl {

// ---- NDI library lifetime ---------------------------------------------------

static void ensureNdiInitialized()
{
    struct Guard {
        Guard()  { NDIlib_initialize(); }
        ~Guard() { NDIlib_destroy(); }
    };
    static Guard g;
}

// ---- XML attribute helpers --------------------------------------------------

static float xmlAttrFloat(const std::string& xml,
                          const char*        attr,
                          float              default_val = 0.0f)
{
    const std::string needle = std::string(attr) + "=\"";
    const size_t pos = xml.find(needle);
    if (pos == std::string::npos)
        return default_val;

    const size_t val_start = pos + needle.size();
    const size_t val_end   = xml.find('"', val_start);
    if (val_end == std::string::npos)
        return default_val;

    return static_cast<float>(std::atof(xml.c_str() + val_start));
}

static bool xmlHasTagCI(const std::string& xml, const char* tag)
{
    auto to_lower = [](unsigned char c) {
        return static_cast<char>(std::tolower(c));
    };
    std::string xml_lower(xml);
    std::string tag_lower(tag);
    std::transform(xml_lower.begin(), xml_lower.end(), xml_lower.begin(), to_lower);
    std::transform(tag_lower.begin(), tag_lower.end(), tag_lower.begin(), to_lower);
    return xml_lower.find(tag_lower) != std::string::npos;
}

static bool tryParsePtzState(const char* xml_data, PtzState& out)
{
    if (!xml_data)
        return false;

    const std::string xml(xml_data);

    // Accept the common tag names used by NDI PTZ cameras.
    if (!xmlHasTagCI(xml, "ndi_ptz_response") &&
        !xmlHasTagCI(xml, "ntk_ptz_response") &&
        !xmlHasTagCI(xml, "ptz_state"))
    {
        return false;
    }

    // Some cameras report focus as "focus_value"; fall back to "focus".
    const float focus_value = xmlAttrFloat(xml, "focus_value", -1.0f);

    out.pan   = xmlAttrFloat(xml, "pan");
    out.tilt  = xmlAttrFloat(xml, "tilt");
    out.zoom  = xmlAttrFloat(xml, "zoom");
    out.focus = (focus_value >= 0.0f) ? focus_value : xmlAttrFloat(xml, "focus");

    return true;
}

// ---- CameraDiscovery --------------------------------------------------------

struct CameraDiscovery::Impl {
    NDIlib_find_instance_t finder = nullptr;

    Impl(const char* extra_ips, const char* groups)
    {
        ensureNdiInitialized();

        NDIlib_find_create_t desc = {};
        desc.show_local_sources  = true;
        desc.p_groups            = groups;
        desc.p_extra_ips         = extra_ips;

        finder = NDIlib_find_create_v2(&desc);
    }

    ~Impl()
    {
        if (finder)
            NDIlib_find_destroy(finder);
    }
};

CameraDiscovery::CameraDiscovery(const char* extra_ips, const char* groups)
    : m_impl(std::make_unique<Impl>(extra_ips, groups))
{
}

CameraDiscovery::~CameraDiscovery() = default;

std::vector<CameraSource> CameraDiscovery::discover(uint32_t timeout_ms)
{
    std::vector<CameraSource> result;
    if (!m_impl->finder)
        return result;

    NDIlib_find_wait_for_sources(m_impl->finder, timeout_ms);

    uint32_t count = 0;
    const NDIlib_source_t* sources =
        NDIlib_find_get_current_sources(m_impl->finder, &count);

    for (uint32_t i = 0; i < count; ++i) {
        CameraSource src;
        if (sources[i].p_ndi_name)    src.name = sources[i].p_ndi_name;
        if (sources[i].p_url_address) src.url  = sources[i].p_url_address;
        result.push_back(std::move(src));
    }

    return result;
}

// ---- PtzCamera --------------------------------------------------------------

struct PtzCamera::Impl {
    NDIlib_recv_instance_t recv = nullptr;

    explicit Impl(const CameraSource& source)
    {
        ensureNdiInitialized();

        NDIlib_source_t ndi_src = {};
        ndi_src.p_ndi_name    = source.name.empty() ? nullptr : source.name.c_str();
        ndi_src.p_url_address = source.url.empty()  ? nullptr : source.url.c_str();

        NDIlib_recv_create_v3_t desc = {};
        desc.source_to_connect_to = ndi_src;
        desc.color_format         = NDIlib_recv_color_format_BGRX_BGRA;
        desc.bandwidth            = NDIlib_recv_bandwidth_metadata_only;
        desc.allow_video_fields   = false;
        desc.p_ndi_recv_name      = "PTZ Controller";

        recv = NDIlib_recv_create_v3(&desc);
    }

    ~Impl()
    {
        if (recv)
            NDIlib_recv_destroy(recv);
    }

    bool sendMetadata(const char* xml) const
    {
        if (!recv)
            return false;
        NDIlib_metadata_frame_t meta = {};
        meta.p_data = const_cast<char*>(xml);
        return NDIlib_recv_send_metadata(recv, &meta);
    }
};

PtzCamera::PtzCamera(const CameraSource& source)
    : m_impl(std::make_unique<Impl>(source))
{
}

PtzCamera::~PtzCamera() = default;

bool PtzCamera::isPtzSupported() const
{
    return m_impl->recv && NDIlib_recv_ptz_is_supported(m_impl->recv);
}

bool PtzCamera::getState(PtzState& out_state, uint32_t timeout_ms)
{
    if (!m_impl->recv)
        return false;

    // Ask the camera to report its current PTZ state.
    m_impl->sendMetadata("<ndi_ptz_query/>");

    const auto deadline =
        std::chrono::steady_clock::now() + std::chrono::milliseconds(timeout_ms);

    while (std::chrono::steady_clock::now() < deadline) {
        const auto remaining =
            std::chrono::duration_cast<std::chrono::milliseconds>(
                deadline - std::chrono::steady_clock::now()).count();
        if (remaining <= 0)
            break;

        NDIlib_metadata_frame_t meta = {};
        const NDIlib_frame_type_e type = NDIlib_recv_capture_v2(
            m_impl->recv, nullptr, nullptr, &meta,
            static_cast<uint32_t>(remaining));

        switch (type) {
        case NDIlib_frame_type_metadata:
            if (meta.p_data) {
                const bool ok = tryParsePtzState(meta.p_data, out_state);
                NDIlib_recv_free_metadata(m_impl->recv, &meta);
                if (ok)
                    return true;
            }
            break;

        case NDIlib_frame_type_status_change:
            // Camera capabilities updated; re-send the query.
            m_impl->sendMetadata("<ndi_ptz_query/>");
            break;

        case NDIlib_frame_type_error:
            return false;

        default:
            break;
        }
    }

    return false;
}

bool PtzCamera::setPanTilt(float pan, float tilt)
{
    return m_impl->recv && NDIlib_recv_ptz_pan_tilt(m_impl->recv, pan, tilt);
}

bool PtzCamera::setZoom(float zoom)
{
    return m_impl->recv && NDIlib_recv_ptz_zoom(m_impl->recv, zoom);
}

bool PtzCamera::setFocus(float focus)
{
    return m_impl->recv && NDIlib_recv_ptz_focus(m_impl->recv, focus);
}

bool PtzCamera::setAutoFocus()
{
    return m_impl->recv && NDIlib_recv_ptz_auto_focus(m_impl->recv);
}

bool PtzCamera::setPanTiltSpeed(float pan_speed, float tilt_speed)
{
    return m_impl->recv &&
           NDIlib_recv_ptz_pan_tilt_speed(m_impl->recv, pan_speed, tilt_speed);
}

bool PtzCamera::setZoomSpeed(float zoom_speed)
{
    return m_impl->recv && NDIlib_recv_ptz_zoom_speed(m_impl->recv, zoom_speed);
}

bool PtzCamera::setFocusSpeed(float focus_speed)
{
    return m_impl->recv && NDIlib_recv_ptz_focus_speed(m_impl->recv, focus_speed);
}

bool PtzCamera::storePreset(int preset_no)
{
    return m_impl->recv && NDIlib_recv_ptz_store_preset(m_impl->recv, preset_no);
}

bool PtzCamera::recallPreset(int preset_no, float speed)
{
    return m_impl->recv &&
           NDIlib_recv_ptz_recall_preset(m_impl->recv, preset_no, speed);
}

bool PtzCamera::setAutoWhiteBalance()
{
    return m_impl->recv && NDIlib_recv_ptz_white_balance_auto(m_impl->recv);
}

bool PtzCamera::setIndoorWhiteBalance()
{
    return m_impl->recv && NDIlib_recv_ptz_white_balance_indoor(m_impl->recv);
}

bool PtzCamera::setOutdoorWhiteBalance()
{
    return m_impl->recv && NDIlib_recv_ptz_white_balance_outdoor(m_impl->recv);
}

bool PtzCamera::setOneshotWhiteBalance()
{
    return m_impl->recv && NDIlib_recv_ptz_white_balance_oneshot(m_impl->recv);
}

bool PtzCamera::setManualWhiteBalance(float red, float blue)
{
    return m_impl->recv &&
           NDIlib_recv_ptz_white_balance_manual(m_impl->recv, red, blue);
}

bool PtzCamera::setAutoExposure()
{
    return m_impl->recv && NDIlib_recv_ptz_exposure_auto(m_impl->recv);
}

bool PtzCamera::setManualExposure(float level)
{
    return m_impl->recv && NDIlib_recv_ptz_exposure_manual(m_impl->recv, level);
}

bool PtzCamera::setManualExposureV2(float iris, float gain, float shutter_speed)
{
    return m_impl->recv &&
           NDIlib_recv_ptz_exposure_manual_v2(m_impl->recv, iris, gain, shutter_speed);
}

} // namespace PtzControl
