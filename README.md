# BFS × Forza Live Bridge

An experimental Windows bridge that captures the running Forza Horizon 6 window and displays its live video behind Beat For Speed Demo gameplay. Capture runs continuously until you stop it or the source closes. **There is no time limit.**

This is a **flat live-video backdrop prototype**, not a full BFS port into Forza. It does not synchronize Forza's car, camera, physics or road with BFS. A car visible in the feed remains part of that image. Clean mode hides BFS's player/world visuals but keeps rhythm targets and hazards; BFS still supplies gameplay and collisions. Depth-correct scenery integration is not implemented.

## Requirements

- Windows 10/11 and your own installed copies of both games.
- Tested source: Xbox Forza Horizon 6 package 3.461.691.0.
- Tested host: Steam Beat For Speed Demo 0.7.33 and BepInEx 6 IL2CPP loader, build 6.0.0-be.788. The installer can download this exact official loader for a fresh installation. Other game/loader versions need revalidation.
- Python 3. Capture uses only its standard library.
- FFmpeg with `gfxcapture`. Start-Bridge finds an existing copy or downloads one from [BtbN's FFmpeg builds](https://github.com/BtbN/FFmpeg-Builds). This third-party build has its own license and source links there. FFmpeg is not bundled.

## Install and play

**Manual plugin installation:** with BepInEx 6 Unity IL2CPP x64 already installed, close BFS and copy `BFS.ForzaLive.dll` into that copy's `BepInEx/plugins` folder. Back up an older copy of this DLL before replacing it. The plugin-only ZIP is for users who already have the capture helper; first-time users should extract the full bridge ZIP for its `tools`, `sheets`, Start and Stop scripts. The optional installer below performs these checks/copies for you.

1. Download **BFS-Forza-Live-Bridge-0.2.4.zip** from [Releases](https://github.com/dr4lera/bfs-forza-live-bridge/releases/tag/v0.2.4), and extract the whole ZIP into a permanent folder. Use this release asset; GitHub's automatic source archives do not contain the compiled installer or receiver.
2. Close BFS and double-click **Install-Bridge.exe**. It finds Steam library installations or asks for the game folder, verifies the DLL, backs up an existing bridge, and installs it. On a fresh game installation it offers the pinned official BepInEx 6 Unity IL2CPP x64 build 788 download, with checksum verification. It refuses to overwrite an existing/incomplete loader. [Official BepInEx instructions](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html) cover manual loader installation. The installer does not require a .NET SDK. See `INSTALL.txt` for custom paths and removal.
3. Open your Xbox Forza copy. Its window must be visible, non-minimized and titled `Forza Horizon 6`. Enter Drone Mode and position the camera behind your car facing down the road. Press **Backspace** in Forza to hide the Drone Mode UI. The creator reports that Drone Mode keeps the scenery source usable when switching to BFS; this is not guaranteed across game versions. Its camera and car remain independent of BFS.
4. Run `Start-Bridge.ps1` from PowerShell. You can pass `-Python "path/to/python.exe" -FFmpeg "path/to/ffmpeg.exe"`. Capture runs hidden, continuously. It does not launch Forza, send driving input, inject code into Forza or modify its files.
5. Start BFS and a solo song. **F8** toggles the backdrop. **F9** toggles clean presentation. Clean mode hides BFS scenery and its instanced grass, preserves block colors, native break effects, targets, traffic hazards and local ramp visuals, and suppresses BFS fog/post-processing over the video. The backdrop stays active during song pause. Menus without an active path follower use BFS's normal view.
6. Run `Stop-Bridge.ps1` when finished. It stops the helper without closing either game. BFS restores the original camera settings and decorative renderers once frames become stale.

Capture is 1280×720 at up to 30 FPS. To change dimensions, edit the transport sheet, run preflight and rebuild the receiver so both processes agree. The whole source image includes Forza's car, road and HUD; these are not separated automatically.

## Compatibility

Solo BFS only. Presentation turns off when BFS reports an active multiplayer client or server. No matchmaking is provided. Forza remains an independently running source; the bridge itself does not connect to its online services.

For another installation of the same BFS Demo 0.7.33 build, install BepInEx and the bridge DLL in that copy and keep one capture helper running on the same Windows PC. Python/FFmpeg and the helper folder can be shared across your installations. A different BFS version may have incompatible game classes and require a receiver rebuild; it has not been validated.

BFS camera settings change temporarily. Clean mode hides its world/player renderers and disables its instanced grass component; targets and hazards remain visible. Original states restore on toggle-off, stale frames or multiplayer activation. Scoring, controls and collisions are not rewritten. Camera/scenery plugins may conflict. The separate countryside project's files and imported cars are not included.

The bridge does not bypass anti-cheat, DRM, ownership or focus protection. It does not suppress Forza's focus notification. Use the game's available modes/settings to keep the source running.

## Troubleshooting

- **It disappeared when recording began:** inspect `BepInEx/plugins/ForzaLiveData/status.json`. `requested=false` means F8 or the error circuit breaker disabled it. `receiveAgeMs` over 750 means a stale source. `network=true` disables presentation. Version 0.1.0 also hid it when BFS paused; update to 0.1.1 for pause retention.
- **Waiting for capture:** run Start-Bridge and check `evidence/capture-continuous.err.log` and `evidence/capture.log` in the bridge folder.
- **Source missing/minimized:** restore Forza. Do not run a second Forza instance. If window capture closes, restore the game and start the bridge again.
- **FFmpeg filter missing:** check `ffmpeg -h filter=gfxcapture` and pass a compatible build.
- **Header mismatch:** rebuild after changing dimensions. Do not run two bridge folders at once; the launcher prevents duplicate processes from the same folder.
- **Colors/perspective do not match:** F9 enables/disables clean mode. Clean mode keeps gameplay exposure, bloom and tonemapping for colored targets; materials are left unchanged. The image has its own camera; depth, road perspective and lane matching remain unfinished.
- **Forza HUD or focus notification appears:** it is part of the captured window. Hide the Drone Mode UI with Backspace in Forza. The bridge cannot remove a source focus warning or resume a paused Forza simulation.

## Remove

Run Stop-Bridge, close BFS, and remove only `BFS.ForzaLive.dll` and its `ForzaLiveData` folder from BepInEx/plugins. Other plugins and songs remain. Nothing is installed into Forza.

## Build

Package 0.2.4 adds the Windows installer and includes the unchanged, live-tested receiver 0.2.3. Build the installer with `Build-Installer.ps1`; it uses the Windows .NET Framework compiler. The installer source is included. Its `--check` option validates a selected game and the release DLL without changing files.

Install the .NET SDK and reference your own BFS BepInEx/interop assemblies. They are never distributed:

```powershell
python tools/preflight.py
dotnet build plugin/BFS.ForzaLive.csproj -c Release -p:GameDir="C:/path/to/Beat For Speed Demo"
```

JSON sheets are the source of truth. Preflight checks their design cells and generates `plugin/Generated.cs`. Transport uses named shared memory, a sequence guard, latest-frame delivery and a 750 ms stale timeout. Texture uploads use pinned managed memory; Unity operations stay on its main thread. Capture reads run on a worker with a one-frame queue, allowing Stop to work even if source frames stop arriving.

## Validation and limitations

The repaired 0.1.0 receiver applied 1,966 frames with zero bridge errors in observed runtime status. Capture ran at about 29–30 FPS. Timestamp samples were roughly 9–62 ms old: **transport freshness, not end-to-end display latency**. When the timed test source stopped, status showed the original view restored with zero modified cameras/renderers.

Version 0.2.3 built without warnings/errors. Observed gameplay status reported zero bridge errors, one disabled instanced grass system and hundreds of preserved target/effect renderers. Inspected gameplay captures showed colored targets and break fragments, with no BFS grass or dark lower-screen cover. An off/on check on 0.2.2 restored camera, renderer and grass states; 0.2.3 removes that local build's failed material adjustment. Complete alignment, sustained performance, long recordings and multiplayer behavior have not been fully verified. The public package is a regular release; the bridge remains experimental. It is not claimed to be a finished photorealistic fusion.

## Credits and rights

Original code and docs developed with OpenAI Codex. Uses [BepInEx](https://github.com/BepInEx/BepInEx), [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop) and [FFmpeg](https://ffmpeg.org/). Local investigation used [universal-modder](https://github.com/rehan-remade/universal-modder). Unity, Forza and Beat For Speed names identify interoperability targets; this is unofficial.

No game content, extracted assets, songs, decompiled game source, credentials or personal recordings are included. Dependencies and games retain their respective rights. No open-source license has been selected for the original bridge code; public visibility does not grant a redistribution/remix license.
