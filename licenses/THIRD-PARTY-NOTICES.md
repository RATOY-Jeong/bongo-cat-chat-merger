# Third-party components

Runtime source: https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5

Archive: `BepInEx_win_x64_5.4.23.5.zip`

SHA-256: `82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4`

The original upstream BepInEx runtime binaries are included without modification. Doorstop and BepInEx configuration are adapted for this game. The bridge and companion source are in this repository. Runtime libraries are shipped in the release ZIP.

- BepInEx: https://github.com/BepInEx/BepInEx/tree/v5-lts — license in `BepInEx-LICENSE.txt`
- HarmonyX: https://github.com/BepInEx/HarmonyX — license in `HarmonyX-LICENSE.txt`
- MonoMod: https://github.com/MonoMod/MonoMod — license in `MonoMod-LICENSE.txt`
- Mono.Cecil: https://github.com/jbevain/cecil — license in `Mono.Cecil-LICENSE.txt`
- UnityDoorstop: https://github.com/NeighTools/UnityDoorstop — license in `Doorstop-LICENSE.txt`
- Windows Mono class libraries, Unity 2022.3.30 profile: published with the BepInEx UnityDataMiner platform-profile issue at https://github.com/BepInEx/UnityDataMiner/issues/3 . Archive: https://github.com/user-attachments/files/17195456/2022.3.30_Win.zip . These unmodified managed libraries are in `runtime/BepInEx/unstripped`. Mono license notices are in `Mono-LICENSE.txt` and `Mono-ClassLibraries-LICENSE.txt`.

The game itself and its original assemblies are not redistributed. Build scripts reference the user's installed game.


Json.NET compatibility runtime (0.4.12): official Newtonsoft.Json 13.0.2 NuGet package, https://www.nuget.org/packages/Newtonsoft.Json/13.0.2 . Unmodified lib/net45/Newtonsoft.Json.dll is included in runtime/BepInEx/unstripped to match full Mono interface contracts. Package SHA256: 112CA3B7F47BCBD743BEFCF949FB68CE4F9EFF73AD9F7F1B39C2C61A1EBD3ADD. DLL SHA256: C5C83BBC1741BE6FF4C490C0AEE34C162945423EC577C646538B2D21CE13199E. License: Newtonsoft.Json-LICENSE.txt (MIT). The game's original Managed files are preserved.
