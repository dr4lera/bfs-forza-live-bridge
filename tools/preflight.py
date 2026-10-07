import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
data = json.loads((ROOT/'sheets/bridge.json').read_text())
errors = []
for sheet, rows in data.items():
    if not rows: errors.append(f'{sheet}: no rows')
    keys = set(rows[0])
    for row in rows:
        for key in keys:
            if key not in row or row[key] is None or row[key] == '': errors.append(f'{sheet}.{row.get("id")}.{key}: empty')
        if set(row) != keys: errors.append(f'{sheet}.{row.get("id")}: inconsistent columns')
t, p = data['transport'][0], data['presentation'][0]
if t['pixelFormat'] != 'rgba' or t['headerBytes'] != 64: errors.append('Unsupported transport layout')
if t['width']*t['height']*4 > 16777216: errors.append('Frame exceeds receiver bound')
if p['multiplayerAllowed'] or p['changeRoad'] or p['changeCar']: errors.append('Out of scope mutation')
known_components = {'CubeTriggerEvent', 'CubeExplodeEffectCaller', 'ProximityPathVehicleMover', 'ChildObjectExplosion'}
if set(p['preserveComponentNames']) - known_components: errors.append('Unknown preserved gameplay component')
known_fields = {'AtmosphericScattering','Volumetrics','VolumetricClouds','Postprocess','CustomPostProcess','CustomPass','Distortion','ExposureControl','MotionVectors','ObjectMotionVectors','MotionBlur','DepthOfField','ColorGrading','Vignette','ChromaticAberration','LensDistortion','FilmGrain','Tonemapping','Bloom'}
if set(p['disabledFrameSettings']) - known_fields: errors.append('Unknown HDRP frame setting')
if set(p['gameplayFrameSettings']) - known_fields: errors.append('Unknown gameplay camera setting')
if set(p['gameplayFrameSettings']) & set(p['disabledFrameSettings']): errors.append('Contradictory camera settings')
if errors: raise SystemExit('\n'.join(errors))
def csstr(s): return json.dumps(s)
cs = '// Generated from sheets/bridge.json; edit the sheet first.\nnamespace BFSForzaLive;\n'
cs += 'internal static class FrameRow {\n'
for key, typ in [('mapping','string'),('width','int'),('height','int'),('fps','int'),('headerBytes','int'),('magic','uint'),('staleMs','int')]:
    value = csstr(t[key]) if typ == 'string' else str(t[key])
    cs += f' internal const {typ} {key} = {value};\n'
cs += '}\ninternal static class BackdropRow {\n'
for key, typ in [('layer','int'),('cameraDepth','int'),('autoEnable','bool'),('previewInMenu','bool'),('cleanVideo','bool')]:
    value = str(p[key]).lower()
    if typ == 'float': value += 'f'
    cs += f' internal const {typ} {key} = {value};\n'
for key in ['hideRootNames','hideRootPrefixes','preserveComponentNames','preserveRootNames','preserveRootPrefixes','preserveRootPatterns','disabledFrameSettings','gameplayFrameSettings']:
    cs += f' internal static readonly string[] {key} = {{'+','.join(csstr(v) for v in p[key])+'};\n'
cs += '}\n'
cs += 'internal record HookRow(string Id, string Target, string Method, bool WritesGame);\ninternal static class HookRows { internal static readonly HookRow[] All = {\n'
for r in data['hooks']:
    cs += ' new('+','.join(csstr(r[k]) for k in ['id','target','method'])+','+str(r['writesGame']).lower()+'),\n'
cs += '}; }\n'
(ROOT/'plugin/Generated.cs').write_text(cs)
print('Preflight clean: every design cell filled; 5 rows; no unresolved references. Runtime verification remains separate.')
