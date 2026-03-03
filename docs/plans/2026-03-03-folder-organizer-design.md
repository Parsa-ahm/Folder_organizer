# FolderOrganizer — Design Document
**Date:** 2026-03-03
**Status:** Approved

---

## Overview

FolderOrganizer is a Windows 11 shell extension + companion app that lets users right-click any file or folder to color-code it, assign a custom icon, and tag it — all without touching folder properties. It is designed to be completely non-invasive: it never crashes Explorer, never runs at startup, and uninstalls cleanly.

---

## Architecture

Four projects in one solution:

```
FolderOrganizer/
├── FolderOrganizer.Shell/      # COM DLL — context menu shell extension
├── FolderOrganizer.App/        # WinUI 3 — preset manager & settings GUI
├── FolderOrganizer.Core/       # Shared library — icon gen, ADS I/O, metadata
└── FolderOrganizer.Installer/  # WiX installer — COM registration, app install
```

- **Core** is the single source of truth for all business logic. Both Shell and App depend on it.
- **Shell DLL** is lean — reads metadata from ADS, draws the menu, swallows all exceptions.
- **App** is opened on demand. No system tray. No startup entry. No background services.
- **Installer** handles COM registration (requires one-time admin) and creates an uninstaller that removes everything.

---

## Metadata Storage

Metadata is stored in **NTFS Alternate Data Streams (ADS)** attached to each file/folder, so it travels with the item when moved within the same NTFS volume.

Schema (stored as JSON in the ADS stream named `FolderOrganizer`):
```json
{
  "color": "#EA4335",
  "icon": "C:\\Users\\...\\AppData\\Roaming\\FolderOrganizer\\icons\\my-icon.png",
  "tags": [
    { "name": "Work", "color": "#1A73E8" },
    { "name": "Urgent", "color": "#EA4335" }
  ]
}
```

- Writes are **atomic** — written to a temp stream then renamed, never leaving corrupt state.
- "Clear All" strips the ADS stream entirely, leaving the file untouched.
- A write log in `%AppData%\FolderOrganizer\touched.log` tracks every file ever modified, enabling global "Clear All Customizations" from the app.

---

## Context Menu (Shell Extension)

Registered as a classic COM `IContextMenu` / `IShellExtInit` shell extension. Uses **owner-drawn menu items** to render rich UI inside the native Windows shell menu.

### Menu Structure

Right-click any file or folder → **"Organize"** (with small colored folder icon)

Hovering opens submenu with three collapsible sections:

**1. Color**
- Horizontal row of 9 filled circles (Chrome tab group palette)
- Active color shows a checkmark
- `+` circle at the end opens Windows native color picker
- Clicking any circle applies immediately

**2. Custom Icons** *(collapsible)*
- Grid of saved icon preset thumbnails
- `+ Add` opens file picker (JPEG/PNG)
- Clicking applies immediately

**3. Tags** *(collapsible)*
- All created tags shown as colored pill chips
- Tags applied to current item are checked
- Clicking toggles on/off (multiple tags supported)
- `+ New Tag` opens inline popup: name field + color circle row

**Bottom row — Clear**
`Clear Color` · `Clear Icon` · `Clear Tags` · `Clear All`

### Safety Contract
- Every method in the DLL is wrapped in a top-level try/catch
- On any exception: silently return S_OK or the appropriate COM error — never propagate
- No blocking I/O on the UI thread — ADS reads are fast but done on a background thread with a timeout

---

## Preset Colors

9 built-in colors matching Chrome tab groups (non-deletable):

| Name   | Hex       |
|--------|-----------|
| Grey   | `#5F6368` |
| Blue   | `#1A73E8` |
| Red    | `#EA4335` |
| Yellow | `#FBBC04` |
| Green  | `#34A853` |
| Pink   | `#F06292` |
| Purple | `#9334E6` |
| Cyan   | `#24C1E0` |
| Orange | `#FA903E` |

These are pre-generated as tinted folder icons at install time. Custom colors beyond these use runtime icon generation (tint applied over the base Windows folder icon via GDI+/SkiaSharp).

---

## Tags & Explorer Column

Tags are stored per-file in ADS. A **central tag registry** at `%AppData%\FolderOrganizer\tags.json` keeps the global list of all created tags available across every context menu.

A **Windows Shell Property Handler** registers a `FolderOrganizer.Tags` property that Explorer reads from ADS. This adds a real, sortable, filterable "Tags" column to Explorer detail view. No admin required for property handler registration (per-user HKCU).

---

## Standalone App (WinUI 3)

Windows 11 Fluent Design — NavigationView sidebar with three pages.

### Page 1 — Presets
- **Colors section**: 9 Chrome defaults (non-deletable) + user custom colors. `+ Add Color` → color picker.
- **Icons section**: Grid of saved custom icon presets. Drag to reorder, rename, delete. `+ Add Icon` → file picker.
- **Tags section**: List of all tags with color pill. Edit name/color inline. Delete. `+ New Tag`.

### Page 2 — Themes
- **Get Themes** button — opens VSCode Marketplace in browser (icon theme packs, `.vsix` files import directly)
- **Import Theme** button — file picker for `.vsix` or `.zip`. Auto-detects format.
- Import guide info card explaining the 2-step process.
- **Active Theme** card — shows applied theme name + `Remove Theme` button that cleanly reverts all overrides.
- Themes apply system-wide by writing `desktop.ini` into folders and registering file type icon overrides in `HKCU` registry (no admin needed).

### Page 3 — Settings
- `Clear All Customizations` — strips ADS from every file in the touch log
- `Reset Tag Registry` — clears `tags.json`
- `Unregister Context Menu` — removes COM registration without full uninstall
- Toggle: Explorer Tags column on/off
- About + version info

---

## Installer

Built with **WiX Toolset**. Handles:
- COM DLL registration (`regsvr32` equivalent, requires admin — one-time)
- App installation to `%ProgramFiles%\FolderOrganizer`
- AppData directory setup
- Pre-generated icon assets
- Uninstaller that removes: COM registration, registry keys, app files, optionally user data (prompted)

---

## Non-Invasive Guarantees

- Shell DLL: all exceptions swallowed, Explorer can never crash because of this app
- No startup entries, no background services, no system tray
- Minimal registry footprint: COM registration + HKCU property handler only
- All file modifications (ADS) are atomic and tracked
- Uninstall is complete and clean
- Theme icon overrides are scoped to HKCU — no system-wide registry writes
