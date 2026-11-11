# QuickText 📝💎

[![Framework](https://img.shields.io/badge/.NET-10.0--windows-purple.svg?style=flat-square)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-blue.svg?style=flat-square)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg?style=flat-square)](LICENSE)
[![Style](https://img.shields.io/badge/Design-Glassmorphism-988BF0.svg?style=flat-square)](https://github.com/BlazeR-28/QuickText)

[![Download QuickText](https://img.shields.io/badge/Download-Latest%20Release-blueviolet.svg?style=for-the-badge&logo=github)](https://github.com/BlazeR-28/QuickText/releases/latest)

QuickText is a lightweight, ultra-low latency Windows scratchpad application designed for developers and power users. Built on **.NET 10** with a hybrid architecture of a native **WPF C# shell** and an embedded **WebView2 (Chromium)** instance, it combines the styling flexibility of web technology with the raw performance and control of Windows Win32 APIs.

The user interface implements a premium, desaturated slate-purple dark theme with frosted glass (glassmorphism) surfaces, subtle hover micro-interactions, and a clean, icon-free layout structure inspired by modern Apple and Obsidian design philosophies.

---

## 📖 Table of Contents

- [Core Features](#-core-features)
- [Keyboard Shortcuts Reference](#-keyboard-shortcuts-reference)
- [Technical Architecture](#-technical-architecture)
- [Local Storage & Configuration](#-local-storage--configuration)
- [Prerequisites & Build Instructions](#-prerequisites--build-instructions)
- [License](#-license)

---

## ✨ Core Features

### 🎨 Premium UI/UX (Glassmorphism)

- **Frosted Glassmorphism:** Fully transparent/translucent window styling utilizing Windows DWM composition.
- **Slate-Purple Caret & Selection:** Custom-tailored text input styling matching the active theme color tokens.
- **Flexbox Layout:** A fixed title bar and footer that remain visible, restricting scrollbars exclusively to the notepad text area.
- **Watermark Branding:** A centered, low-opacity "made by BlazeR" footer that dynamically pushes upward when links are detected to avoid visual overlapping.

### ⚡ Native Performance & Windows Interop

- **Win32 Layered Window Opacity:** Window transparency is handled natively via Win32 `SetLayeredWindowAttributes` by bypassing WPF's restrictive `AllowsTransparency="True"` mode (which conflicts with WebView2 input capture).
- **Always-On-Top:** Native WPF topmost configuration keeps the scratchpad visible over other applications.
- **Borderless Dragging:** Smooth window repositioning by clicking and dragging anywhere on the frosted background title bar.
- **Collapsible Scratchpad:** Double-click the title bar background to collapse the window to just its toolbar; double-click again to restore the editor without losing text. Clear, Copy, and Close remain available while collapsed; Settings expands the window to show its panel. The collapsed bar stays draggable and temporarily bypasses the minimum editor height.
- **Customizable Window Size Limits:** Automatically grows and shrinks the window height to match note length, bounded by user-defined minimum/maximum height and fixed width limits configured in the Settings panel via a clean 2x2 grid layout.
- **System-wide Visibility Toggle:** Register a global hotkey to show or hide the scratchpad instantly from anywhere in Windows.

### 📝 Editor Utilities

- **Autosave & Auto-Restore:** Notepad content is written to disk in real-time and automatically restored on next startup.
- **Tab Insertion:** Intercepts the `Tab` key to insert literal `\t` characters instead of shifting focus to other UI controls.
- **Copy Feedback:** Single-click Copy button with a temporary green success glow animation.
- **Clear Animation:** Amber-styled Clear button with a success confirmation state that wipes content and shrinks the window back to its configured minimum height.
- **Reactive Link Pills:** Automatically scans your notes for URLs in real-time, displaying them as clickable pills at the bottom. Clicking a pill opens the URL in your default system browser.

---

## ⌨ Keyboard Shortcuts Reference

The application supports both **local shortcuts** (active when the window is focused) and **global hotkeys** (active system-wide). All shortcuts can be customized or disabled (`not set`) directly in the Settings panel.

| Action | Type | Default Shortcut | Description |
| :--- | :--- | :--- | :--- |
| **Copy All Text** | Local | `CTRL+SHIFT+C` | Copies the entire content of the scratchpad to the clipboard. |
| **Close / Hide** | Local | `ESCAPE` | Closes the settings panel if open, otherwise hides the app to the tray (if global hotkey is set) or exits. |
| **Exit Application** | Local | `CTRL+Q` | Completely terminates and shuts down the application. |
| **Global Show/Hide** | Global | `not set` (Click to record) | System-wide shortcut to instantly toggle the window's visibility. |
| **Insert Tab Char** | Local | `TAB` | Inserts a literal `\t` tab space at the current cursor position. |

---

## ⚙ Technical Architecture

The WPF host targets `net10.0-windows` (.NET 10 for Windows). The default publish configuration is framework-dependent: .NET is not bundled with the executable.

To bypass the classic WPF "Airspace Conflict" (where hosting native controls like `WebView2` inside a transparent WPF window causes keyboard inputs to freeze or renders the control completely invisible), QuickText implements a custom **Win32 Hooking Layer**:

```text
[User Interaction (Slider)]
           │
           ▼
   [WebView2 frontend] ────(PostMessage JSON)────► [WPF C# Host]
                                                          │
                                                (Interceptors & P/Invoke)
                                                          │
                                                          ▼
                                            [Windows API (user32.dll)]
                                            • Add WS_EX_LAYERED style
                                            • SetLayeredWindowAttributes (Opacity)
                                            • Intercept WM_STYLECHANGING (Prevent WPF strip)
```

1. **AllowsTransparency="False"**: The window is initialized as an opaque window, which lets `WebView2` receive keyboard and mouse focus natively.
2. **WS_EX_LAYERED Injection**: The window style is modified to a layered window (`WS_EX_LAYERED`) on runtime.
3. **WM_STYLECHANGING Hook**: Since WPF detects the layered style and attempts to strip it back off during layout passes, a native `HwndSource` hook intercepts `WM_STYLECHANGING` messages for `GWL_EXSTYLE`, forces `WS_EX_LAYERED` back on, and marks the message as `handled = true` to bypass WPF's internal window procedure.
4. **SetLayeredWindowAttributes**: Opacity is updated smoothly via `user32.dll` attributes, giving the window DWM-blended translucency.

---

## 📂 Local Storage & Configuration

QuickText operates completely offline and saves all settings and notes locally in the user's AppData directory:

- **Settings File:** `%LOCALAPPDATA%\QuickText\settings.json` (stores hotkeys, autosave configuration, and opacity).
- **Notes File:** `%LOCALAPPDATA%\QuickText\notes.txt` (stores notepad text if Autosave is enabled).
- **WebView2 Cache:** `%LOCALAPPDATA%\QuickText\WebView2_Data\` (stores browser profile and storage cache).

---

## 🛠 Prerequisites & Build Instructions

### Prerequisites

- Windows 10 or 11 (64-bit)
- **To build:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- **To run a framework-dependent release:** [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) for Windows x64 (included with the SDK; the base .NET Runtime alone is not sufficient for WPF)
- Microsoft Edge WebView2 Runtime (Pre-installed on modern Windows)

### Build and Run from Source

Open PowerShell in the repository directory with the .NET 10 SDK installed:

```powershell
dotnet build -c Release
dotnet run -c Release
```

### Publish a Single-File Executable

Run the following command to publish a framework-dependent Windows x64 release:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The published executable will be located at:
`bin\Release\net10.0-windows\win-x64\publish\QuickText.exe`

The destination machine must have the .NET 10 Desktop Runtime and WebView2 Runtime installed.

### Install or Update for the Current User

From the repository directory, run:

```powershell
.\scripts\Install-QuickText.ps1
```

The script publishes a Windows x64 release to `bin\publish`, then installs it at
`%LOCALAPPDATA%\Programs\QuickText`. It force-stops any instance running from that
installation before copying the published files, creates or updates the Windows
Startup shortcut, and launches QuickText. Other copies running from different
locations are left alone. Publishing must succeed before the installed app is
stopped or changed.

Use `.\scripts\Install-QuickText.ps1 -NoStart` to install without launching immediately.
The same command works for first-time installation and updates; no administrator
rights are required. Existing notes and settings are preserved. The build and
runtime prerequisites above still apply.

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
