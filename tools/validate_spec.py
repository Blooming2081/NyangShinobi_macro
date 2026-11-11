#!/usr/bin/env python3
"""Validate v0.1 Codex design material. Does not require .NET or run a macro."""
from __future__ import annotations
import hashlib
import json
from pathlib import Path
from collections import Counter

ROOT = Path(__file__).resolve().parents[1]

def main() -> int:
    problems = []
    def load(name):
        try:
            return json.loads((ROOT / name).read_text('utf-8'))
        except (OSError, ValueError) as e:
            problems.append(f'{name}: {e}')
            return {}
    manifest = load('data/asset_manifest.v0.1.json')
    catalog = load('data/screen_catalog.v0.1.json')
    settings = load('data/settings.default.v0.1.json')
    workflows = load('data/workflows.v0.1.json')
    arch = load('data/architecture.v0.1.json')
    modules = load('data/modules.v0.1.json')
    event_schema = load('data/events/event.v0.1.schema.json')
    events = sorted((ROOT/'data/events').glob('*.json'))
    known_assets = {e.get('path') for e in manifest.get('assets',[])}
    cat_assets = {e.get('path') for e in catalog.get('screenElements',[])}
    for e in manifest.get('assets',[]):
        path = ROOT/e['path']
        if e.get('included'):
            if not path.is_file(): problems.append(f'missing asset: {e["path"]}');continue
            if hashlib.sha256(path.read_bytes()).hexdigest()!=e.get('sha256'):
                problems.append(f'asset hash mismatch: {e["path"]}')
        if e.get('status')=='required' and not e.get('included'):
            problems.append(f'required but missing: {e["path"]}')
    if not cat_assets.issubset(known_assets):
        problems.append('screen_catalog contains assets missing from manifest')
    if manifest.get('counts') != dict(Counter(e['status'] for e in manifest.get('assets',[]))):
        problems.append('manifest count inconsistent')
    for key in ['date_changed.png','date_change_followup_close.png']:
        asset = f'assets/images_nav/{key}'
        if not (ROOT/asset).is_file(): problems.append(f'date asset missing: {asset}')
        if not any(e.get('path')==asset and e.get('status')=='required' for e in manifest.get('assets',[])):
            problems.append(f'date asset not marked required: {asset}')
    if settings.get('appVersion') not in ('0.1.0','v0.1'):
        problems.append('settings version is not v0.1')
    if not settings.get('dateChange',{}).get('enabled'):
        problems.append('dateChange must be enabled by default')
    if not workflows.get('globalInterrupts',{}).get('dateChange',{}).get('steps'):
        problems.append('date change workflow missing')
    projects = arch.get('projects',[])
    names = [e['name'] for e in projects]
    if len(set(names)) != len(names): problems.append('duplicate project name')
    for proj in projects:
        if any(ref not in names for ref in proj.get('references',[])):
            problems.append(f'project has unknown references: {proj["name"]}')
    # no cycles
    dependency_map = {x['name']:x.get('references',[]) for x in projects}
    visiting,visited=set(),set()
    def visit(k):
        if k in visiting: problems.append(f'project cycle: {k}');return
        if k in visited:return
        visiting.add(k)
        for v in dependency_map.get(k,[]):visit(v)
        visiting.remove(k);visited.add(k)
    for k in names:visit(k)
    modids=[m['id'] for m in modules.get('modules',[])]
    if len(set(modids))!=len(modids):problems.append('duplicate module id')
    if not {'mission','mirror','neko_ninja','double_reward','date_change','events'}.issubset(modids):
        problems.append('missing required game module')
    for p in events:
        if p.name == 'event.v0.1.schema.json':continue
        data=load(str(p.relative_to(ROOT)))
        if p.name.endswith('.disabled.json') and data.get('enabled'):
            problems.append(f'disabled template marked enabled: {p.name}')
        if data.get('schemaVersion') != 1:
            problems.append(f'bad event schemaVersion: {p.name}')
        if not (400<=data.get('priority',0)<=899):
            problems.append(f'invalid event priority: {p.name}')
        for step in data.get('steps',[]):
            if step.get('action') not in ('tap_detected','wait_for_screen'):
                problems.append(f'forbidden event action: {p.name} / {step.get("action")}')
        if data.get('enabled'):
            for match in data.get('trigger',{}).get('allOf',[]):
                if not (ROOT / match.get('asset','__missing__')).is_file():
                    problems.append(f'enabled event missing trigger asset: {p.name}')
            for step in data.get('steps',[]):
                if step.get('action')=='tap_detected' and not (ROOT/step.get('targetAsset','__missing__')).is_file():
                    problems.append(f'enabled event missing action asset: {p.name}')
    if '$schema' not in event_schema:problems.append('event schema missing $schema')
    if problems:
        print('FAILED:',*problems,sep='\n - ')
        return 1
    print(f'PASS: {len(manifest.get("assets",[]))} catalog assets, {len(projects)} project specs, {len(modids)} modules, {len(events)-1} event definitions')
    return 0

if __name__=='__main__':
    raise SystemExit(main())
