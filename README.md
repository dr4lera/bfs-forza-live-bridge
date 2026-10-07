# BFS × Forza Live Bridge

An experimental Windows bridge that captures the running Forza Horizon 6 window and displays its live video behind Beat For Speed Demo gameplay. Capture runs continuously until you stop it or the source closes. **There is no time limit.**

This is a **flat live-video backdrop prototype**, not a full BFS port into Forza. It does not synchronize Forza's car, camera, physics or road with BFS. A car visible in the feed remains part of that image; BFS's playable car still uses BFS's renderer. Depth-correct scenery integration is not implemented.

## Requirements

- Windows 10/11 and your own installed copies of both games.
- Tested source: Xbox Forza Horizon 6 package 3.461.691.0.
- Tested host: Steam Beat For Speed Demo 0.7.33 with an existing BepInEx 6 IL2CPP loader, build 6.0.0-be.788. Other versions need revalidation.
- Python 3. Capture uses only its standard library.
- FFmpeg with `gfxcapture`. Start-Bridge finds an existing copy or downloads one from [BtbN's FFmpeg builds](https://github.com/BtbN/FFmpeg-Builds). This third-party build has its own license and source links there. FFmpeg is not bundled.

## Install and play

1. Close BFS, back up its plugins, and copy `BepInEx/plugins/BFS.ForzaLive.dll` from the release ZIP into the same folder in your BFS installation. This release does not install a loader.
2. Extract the remaining ZIP files into their own folder. Keep `tools`, `sheets` and the PowerShell scripts together.
3. Open your Xbox Forza copy. Its window must be visible, non-minimized and titled `Forza Horizon 6`. The creator reports that Drone Mode keeps the scenery source usable when switching to BFS; this has not been independently verified across versions.
4. Run `Start-Bridge.ps1` from PowerShell. You can pass `-Python "path/to/python.exe" -FFmpeg "path/to/ffmpeg.exe"`. Capture runs hidden, continuously. It does not launch Forza, send driving input, inject code into Forza or modify its files.
5. Start BFS and a solo song. **F8** toggles the backdrop. Version 0.1.1 retains the backdrop while the song is paused; menus without an active path follower use BFS's normal view.
6. Run `Stop-Bridge.ps1` when finished. It stops the helper without closing either game. BFS restores the original camera settings and decorative renderers once frames become stale.

Capture is 1280×720 at up to 30 FPS. To change dimensions, edit the transport sheet, run preflight and rebuild the receiver so both processes agree. The whole source image includes Forza's car, road and HUD; these are not separated automatically.

## Compatibility

Solo BFS only. Presentation turns off when BFS reports an active multiplayer client or server. No matchmaking is provided. Forza remains an independently running source; the bridge itself does not connect to its online services.

BFS's camera clear settings change temporarily, and selected decorative renderer roots are hidden. BFS roads, lanes, scoring, inputs, vehicles and collisions are not rewritten. Camera/scenery plugins may conflict. The separate countryside project's files and imported cars are not included.

The bridge does not bypass anti-cheat, DRM, ownership or focus protection. It does not suppress Forza's focus notification. Use the game's available modes/settings to keep the source running.

## Troubleshooting

- **It disappeared when recording began:** inspect `BepInEx/plugins/ForzaLiveData/status.json`. `requested=false` means F8 or the error circuit breaker disabled it. `receiveAgeMs` over 750 means a stale source. `network=true` disables presentation. Version 0.1.0 also hid it when BFS paused; update to 0.1.1 for pause retention.
- **Waiting for capture:** run Start-Bridge and check `evidence/capture-continuous.err.log` and `evidence/capture.log` in the bridge folder.
- **Source missing/minimized:** restore Forza. Do not run a second Forza instance. If window capture closes, restore the game and start the bridge again.
- **FFmpeg filter missing:** check `ffmpeg -h filter=gfxcapture` and pass a compatible build.
- **Header mismatch:** rebuild after changing dimensions. Do not run two bridge folders at once; the launcher prevents duplicate processes from the same folder.
- **Colors/perspective do not match:** the image has its own camera and tonemapping. HDR, lighting, depth and perspective matching are unfinished.

## Remove

Run Stop-Bridge, close BFS, and remove only `BFS.ForzaLive.dll` and its `ForzaLiveData` folder from BepInEx/plugins. Other plugins and songs remain. Nothing is installed into Forza.

## Build

Install the .NET SDK and reference your own BFS BepInEx/interop assemblies. They are never distributed:

```powershell
python tools/preflight.py
dotnet build plugin/BFS.ForzaLive.csproj -c Release -p:GameDir="C:/path/to/Beat For Speed Demo"
```

JSON sheets are the source of truth. Preflight checks their design cells and generates `plugin/Generated.cs`. Transport uses named shared memory, a sequence guard, latest-frame delivery and a 750 ms stale timeout. Texture uploads use pinned managed memory; Unity operations stay on its main thread. Capture reads run on a worker with a one-frame queue, allowing Stop to work even if source frames stop arriving.

## Validation and limitations

The repaired 0.1.0 receiver applied 1,966 frames with zero bridge errors in observed runtime status. Capture ran at about 29–30 FPS. Timestamp samples were roughly 9–62 ms old: **transport freshness, not end-to-end display latency**. When the timed test source stopped, status showed the original view restored with zero modified cameras/renderers.

Version 0.1.1 builds without warnings or errors. Its pause-retention change has not yet been retested in a fresh BFS process. Complete visual composition/alignment, sustained performance, recording compatibility and multiplayer behavior have not been fully verified. The release is **prerelease**. It is not claimed to be a finished photorealistic fusion.

## Credits and rights

Original code and docs developed with OpenAI Codex. Uses [BepInEx](https://github.com/BepInEx/BepInEx), [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop) and [FFmpeg](https://ffmpeg.org/). Local investigation used [universal-modder](https://github.com/rehan-remade/universal-modder). Unity, Forza and Beat For Speed names identify interoperability targets; this is unofficial.

No game content, extracted assets, songs, decompiled game source, credentials or personal recordings are included. Dependencies and games retain their respective rights. No open-source license has been selected for the original bridge code; public visibility does not grant a redistribution/remix license.
