"""Copy the source dependency closure without changing the reference checkout."""
from pathlib import Path
import re, shutil, json

SOURCE = Path(r'C:\Projects\Bubblepics_Puzzle_And_reference\Bubblepics')
TARGET = Path(r'C:\Projects\pingguoshu\BizzaWZ')
GUID = re.compile(r'guid: ([0-9a-f]{32})')

def index(root):
    result = {}
    for meta in root.rglob('*.meta'):
        match = GUID.search(meta.read_text(encoding='utf-8-sig', errors='replace'))
        if match:
            result[match[1]] = Path(str(meta)[:-5])
    return result

src_index = index(SOURCE / 'Assets')
src_index.update(index(SOURCE / 'Packages'))
dst_index = index(TARGET / 'Assets')
queue = []
for root in ('Scripts', 'Resources', 'StreamingAssets/BubblePicsContent'):
    for path in (SOURCE / 'Assets' / root).rglob('*'):
        if path.is_file() and path.suffix != '.meta' and '/Obfuz/' not in path.as_posix():
            if path.name not in ('ObfuzRuntimeBootstrap.cs', 'AutoPlay.cs', 'SROptions.BubblePicsGm.cs'):
                queue.append(path)
queue.append(SOURCE / 'Assets/Scenes/Main.unity')
copied = set()
reused = set()
missing = set()
while queue:
    path = queue.pop()
    if path in copied or not path.is_file():
        continue
    meta = Path(str(path) + '.meta')
    own = GUID.search(meta.read_text(encoding='utf-8-sig', errors='replace')) if meta.exists() else None
    if own and own[1] in dst_index:
        reused.add(str(path.relative_to(SOURCE)))
        continue
    copied.add(path)
    rel = path.relative_to(SOURCE)
    if rel.parts[0] == 'Packages':
        # Existing Spine runtime supplies the same GUID-backed components.
        # Only the remote-content package is added as a package dependency.
        if 'spine' in rel.parts[1]:
            missing.add(str(rel))
            continue
        dst = TARGET / rel
    elif 'StreamingAssets' in rel.parts:
        dst = TARGET / rel
    else:
        dst = TARGET / 'Assets/BubblePics' / path.relative_to(SOURCE / 'Assets')
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(path, dst)
    if meta.exists():
        shutil.copy2(meta, Path(str(dst) + '.meta'))
    if path.suffix.lower() in ('.prefab', '.asset', '.mat', '.unity', '.controller', '.overridecontroller', '.anim', '.shader', '.json'):
        data = path.read_text(encoding='utf-8-sig', errors='replace')
        for guid in GUID.findall(data):
            if guid in src_index:
                queue.append(src_index[guid])
            elif guid not in dst_index and not guid.startswith('0000000'):
                missing.add(guid)

package = 'com.bubblepics.remote-image-delivery'
shutil.copytree(SOURCE / 'Packages' / package, TARGET / 'Packages' / package, dirs_exist_ok=True)
manifest_path = TARGET / 'Packages/manifest.json'
manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
manifest['dependencies'][package] = 'file:' + package
manifest_path.write_text(json.dumps(manifest, indent=4) + '\n', encoding='utf-8')
report = {'copied': len(copied), 'reused': sorted(reused), 'unresolved': sorted(missing)}
Path('Tools/migration-dependencies.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
