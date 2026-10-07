# Changelog

## 0.2.4 — 2026-10-07

- Regular installation release with a full bridge ZIP and a plugin-only ZIP.
- BepInEx 6 Unity IL2CPP x64 is required; manual plugin installation is documented.
- Optional Windows installer finds Steam libraries, verifies the receiver, backs up an older bridge and offers the pinned official loader download for fresh installations.
- Installer tests passed for fresh installation, upgrades/backups, invalid-folder rejection and checksum rejection in disposable fixtures.
- Includes the unchanged, live-tested gameplay receiver 0.2.3.

## 0.2.3 — 2026-10-07

- Remove the 0.2.2 material adjustment that made targets too dark.
- Retain gameplay exposure, tonemapping and bloom for native colored targets and break effects; keep fog and obstructive camera effects disabled.
- Preserve all chart-generated `_Beat..._Key...` render groups, rather than relying only on selected components.
- Preserve detached fracture and hit-explosion renderers as well as the explosion pool.
- The instanced grass removal from 0.2.2 remains active.
- 0.2.2 was a local test build and was not published.

## 0.2.2 — 2026-10-07

- Remove BFS's instanced grass while clean presentation is active; restore its previous enabled state on fallback.
- Preserve the native block explosion pool, rhythm targets, traffic hazards and local ramp visuals.
- Local emissive adjustment failed the gameplay visual check; replaced in 0.2.3.
- Disable BFS fog, atmospheric scattering, distortion and post-processing over the live source, with full camera-state restoration.
- F9 switches clean presentation; F8 still switches the bridge. Menus retain their original presentation by default.
- Design sheets now control preserved targets and disabled camera effects; reject unsupported settings during preflight.
- This remains a flat video bridge. Car/camera synchronization and Forza focus-notification suppression are not implemented.

## 0.1.1 — 2026-10-06

- Continuous start/stop scripts; removed the timed test option.
- Latest-frame capture queue so Stop works even if frames stop arriving.
- Duplicate-process prevention in the launcher.
- Pause retention and removal of repeated scene-wide follower scans.
- Installation, removal, diagnostics, dependency and limitation documentation.

## 0.1.0 — local experiment

- Window-only capture, shared memory and BFS background camera.
- Pinned-memory upload repair, stale-feed restoration and multiplayer guard.
