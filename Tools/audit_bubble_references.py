from pathlib import Path
import re, json

project=Path('BizzaWZ')
guid_pattern=re.compile(r'guid:\s*([a-fA-F0-9]{32})')
index={}
for root in [project/'Assets',project/'Packages',project/'Library/PackageCache']:
    for meta in root.rglob('*.meta'):
        match=guid_pattern.search(meta.read_text(encoding='utf-8-sig',errors='replace'))
        if match: index.setdefault(match[1].lower(),[]).append(str(meta.with_suffix('')))
missing=[]
checked=0
for path in (project/'Assets/BubblePics').rglob('*'):
    if path.suffix not in {'.prefab','.unity','.mat','.asset','.controller','.overrideController'}: continue
    checked+=1
    for guid in set(guid_pattern.findall(path.read_text(encoding='utf-8-sig',errors='replace'))):
        if guid.startswith('0000000000000000') or guid in index: continue
        missing.append({'file':str(path),'guid':guid})
duplicates={guid:paths for guid,paths in index.items() if len(paths)>1 and any('Assets\\BubblePics\\' in p for p in paths)}
report={'checkedMigratedSerializedAssets':checked,'missingReferences':missing,'duplicateMigratedGuids':duplicates}
Path('Validation/bubble-reference-audit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({'checked':checked,'missing':len(missing),'duplicateGuids':len(duplicates)}))
