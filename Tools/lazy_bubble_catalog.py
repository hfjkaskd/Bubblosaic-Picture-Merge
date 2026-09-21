from pathlib import Path
import re, json

root=Path('BizzaWZ/Assets/BubblePics').resolve()
catalog=root/'RuntimeAssets/RuntimeAssetCatalog.asset'
text=catalog.read_text(encoding='utf-8-sig')
index={}
for meta in root.rglob('*.meta'):
    match=re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text(encoding='utf-8-sig'),re.M)
    if match: index[match[1]]=meta.with_suffix('')
moved=[]
resolved={}
for guid in set(re.findall(r'Asset: \{fileID: -?\d+, guid: ([a-f0-9]{32}), type: \d+\}',text)):
    source=index.get(guid)
    if source is None: continue  # Shared framework assets retain their existing reference.
    if not source.is_file(): raise RuntimeError(f'Missing asset {source}')
    if 'Resources' in source.relative_to(root).parts:
        parts=source.parts
        path=Path(*parts[parts.index('Resources')+1:]).with_suffix('').as_posix()
    else:
        dest=root/'Resources/BubblePicsDynamic'/guid/source.name
        if not source.resolve().is_relative_to(root) or not dest.resolve().is_relative_to(root):
            raise RuntimeError('Asset move escaped migration directory')
        dest.parent.mkdir(parents=True,exist_ok=True)
        source.rename(dest)
        Path(str(source)+'.meta').rename(Path(str(dest)+'.meta'))
        path=dest.relative_to(root/'Resources').with_suffix('').as_posix()
        moved.append({'from':str(source),'to':str(dest),'guid':guid})
    resolved[guid]=path

def replace(match):
    guid=match.group(1)
    if guid not in resolved: return match.group(0)
    return 'ResourcePath: '+resolved[guid]+'\n    Asset: {fileID: 0}'
text=re.sub(r'Asset: \{fileID: -?\d+, guid: ([a-f0-9]{32}), type: \d+\}',replace,text)
catalog.write_text(text,encoding='utf-8')
Path('Validation/lazy-resource-moves.json').write_text(json.dumps(moved,indent=2),encoding='utf-8')
print(json.dumps({'movedAssets':len(moved),'lazyAssets':len(resolved)}))
