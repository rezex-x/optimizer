# Tweaker

A Windows 10/11 optimization app for gaming PCs. Tweaks, NVIDIA settings, app installs, live resource
monitoring and hardware tuning in one window, with a backup of everything it changes.

> Use at your own risk. Create a restore point first (there's a button for it).

## How it works

1. Pick a tab at the top (Tweaks, NVIDIA, Config, Install, Resources, Hardware, Inspector, Updates), then a
   category in the side panel on the left.
2. Flip switches. Changes queue in the bar at the bottom: nothing happens until you press **Apply**.
   **Revert** throws the queue away.
3. Every change saves the old value first, so switching it back off restores it.

The title bar always shows live CPU, memory, disk, network and GPU usage.

## Download

Get `tweaker.exe` from [Releases](../../releases/latest). One file, no install, asks for admin rights.
It isn't code-signed, so SmartScreen may warn: **More info → Run anyway**.

## Build from source (.NET 8 SDK)

```powershell
# run the tests
dotnet test src/Tweaker.Tests

# run the app (from an admin terminal: it needs admin rights)
dotnet run --project src/Tweaker

# build the single-file exe into publish\tweaker.exe
powershell -ExecutionPolicy Bypass -File build.ps1
```
