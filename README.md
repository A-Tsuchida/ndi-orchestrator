# NDI Orchestrator

**NDI Orchestrator** is a tool for controlling PTZ cameras over the NDI (Network Device Interface) video protocol and orchestrating complex scene sequences. It provides an interface to pan/tilt/zoom NDI-capable cameras, define scene presets, record sequences of camera positions and actions, and playback those sequences for automated operation.

> *NDI (Network Device Interface) is a protocol that enables high-quality, low-latency video, audio, and control data over local networks.* ([Wikipedia][1])

---

## 🚀 Features

* **PTZ Control via NDI** — sends pan/tilt/zoom commands to NDI-enabled cameras.
* **Scene Sequencing** — create sequences of camera states (position, zoom, etc.).
* **Record & Playback** — record operator actions and replay them automatically.
* **User Interface** — intuitive UI (CLI / GUI) to manage cameras and sequence steps.
* **Recording Modes** — supports timed transitions or instant scene jumps.

---

## 📦 Quick Start

### Requirements

* A local network with **NDI-capable PTZ cameras**.
* The **NDI runtime / SDK** installed (from NewTek’s developer downloads).
* Python / Node.js / other stack deps depending on implementation.
* Optional: joystick/game controller for manual PTZ control (if supported).

### Installation

```bash
git clone https://github.com/A-Tsuchida/ndi-orchestrator.git
cd ndi-orchestrator
# install dependencies
```

*(Fill in details depending on your implementation stack, e.g., `pip install -r requirements.txt` or `npm install`, etc.)*

### Running

```bash
# run the orchestrator
# e.g., python main.py  (example)
```

---

## 📘 Usage

### PTZ Control

1. Discover NDI sources on your network.
2. Select a PTZ camera source.
3. Use controller / UI to move camera.

> Note: NDI devices typically advertise themselves via mDNS and are discoverable on the local subnet. ([Wikipedia][1])

### Create Scenes

* Move your camera to a desired position.
* Save the position as a “scene preset”.
* Repeat to build a library of presets.

### Sequence Mode

* Create an ordered list of scenes.
* Assign timing or transition rules.
* Run the sequence to automatically navigate through presets.

---

## 🧠 Example

```yaml
scenes:
  - name: “Entrance Wide”
    pan: 0
    tilt: -5
    zoom: 0.5

  - name: “Speaker Close”
    pan: 15
    tilt: -2
    zoom: 0.8

sequence:
  - Entrance Wide
  - Speaker Close
```

---

## 🛠 Development

### Contributing

1. Fork the repo
2. Create a new branch
3. Add features or fixes
4. Submit a pull request

---

## 📄 License

This project is licensed under the **MIT License**.

---

## 📞 Contact

For questions or support, open an Issue on GitHub or contact the maintainer.

[1]: https://en.wikipedia.org/wiki/Network_Device_Interface?utm_source=chatgpt.com "Network Device Interface"
