# NVAPI — NvApiDriverSettings.h (third-party reference)

`NvApiDriverSettings.h` in this folder is a vendored copy of NVIDIA's public
NVAPI SDK header, used as the **authoritative source for driver-setting IDs and
value constants** that the app's NVIDIA settings are built from.

- **Source:** https://github.com/NVIDIA/nvapi — `NvApiDriverSettings.h`
- **Raw URL:** https://raw.githubusercontent.com/NVIDIA/nvapi/main/NvApiDriverSettings.h
- **Fetched:** 2026-09-17
- **Copyright:** © NVIDIA Corporation. Part of the public NVAPI SDK.

This header is redistributed here for reference/attribution so the exact
`*_ID` constants (e.g. `PREFERRED_PSTATE_ID`, `VSYNCMODE_ID`) and their value
enums (e.g. `VSYNCMODE_FORCEOFF`) can be cited when maintaining the app's NVIDIA
settings catalog. The app does not compile this header; it reads the numeric
values from it into its own C# catalog. NVAPI itself is invoked at runtime via
the managed [`NvAPIWrapper.Net`](https://github.com/falahati/NvAPIWrapper)
library (MIT), against the `nvapi64.dll` supplied by the installed NVIDIA driver.
