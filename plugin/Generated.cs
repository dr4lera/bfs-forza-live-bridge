// Generated from sheets/bridge.json; edit the sheet first.
namespace BFSForzaLive;
internal static class FrameRow {
 internal const string mapping = "Local\\BFSForzaLiveFramesV1";
 internal const int width = 1280;
 internal const int height = 720;
 internal const int fps = 30;
 internal const int headerBytes = 64;
 internal const uint magic = 827805250;
 internal const int staleMs = 750;
}
internal static class BackdropRow {
 internal const int layer = 31;
 internal const int cameraDepth = -100;
 internal const bool autoEnable = true;
 internal const bool previewInMenu = false;
 internal static readonly string[] hideRootNames = {"BUILDING","Japanese countryside template"};
 internal static readonly string[] hideRootPrefixes = {"Japanese countryside "};
}
internal record HookRow(string Id, string Target, string Method, bool WritesGame);
internal static class HookRows { internal static readonly HookRow[] All = {
 new("capture","forzahorizon6.exe","Windows.Graphics.Capture via FFmpeg gfxcapture; continuous until Stop-Bridge or game capture closes",false),
 new("gameplay","PathFollower","Instance and CurrentWorldSpeed read only; keep backdrop during song pause",false),
 new("render","Camera and HDAdditionalCameraData","temporary background camera, pinned-pointer texture upload, and restore original clear settings",true),
}; }
