#!/usr/bin/env python3
from __future__ import annotations

from collections import defaultdict
from pathlib import Path
import hashlib
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
errors: list[str] = []
warnings: list[str] = []

FIRST_PARTY_ROOTS = ("assets", "data", "scenes", "scripts")
DECLARATIVE_SUFFIXES = {".tscn", ".tres", ".import", ".cfg"}
CODE_SUFFIXES = {".cs", ".gd"}
QUOTED_RES_PATH = re.compile(r'["\'](res://[^"\']+)["\']')


def add_error(message: str) -> None:
    errors.append(message)


def target_exists(ref: str) -> bool:
    if not ref.startswith("res://"):
        return True
    if ref.startswith("res://.godot/"):
        return True
    return (ROOT / ref.removeprefix("res://")).exists()


for forbidden in ("src/UI.zip", "scripts/UI.zip", "scripts/UI/HUD/j.json"):
    if (ROOT / forbidden).exists():
        add_error(f"forbidden runtime artifact: {forbidden}")

# Naming applies to first-party resources, not C# namespaces or vendor code.
for base in ("assets", "data", "scenes"):
    start = ROOT / base
    if not start.exists():
        continue
    for path in start.rglob("*"):
        rel = path.relative_to(ROOT).as_posix()
        for part in path.relative_to(ROOT).parts:
            core = part.removesuffix(".import").removesuffix(".uid")
            stem = Path(core).stem if Path(core).suffix else core
            if " " in part or "#" in part or any(ch.isupper() for ch in stem):
                add_error(f"non-normalized path: {rel}")
                break

# Hard references in Godot resource files. These are not optional: one missing ext_resource
# can invalidate the entire .tres, which is exactly what previously disabled Hyou's mana/skills.
for base in FIRST_PARTY_ROOTS:
    start = ROOT / base
    if not start.exists():
        continue
    for path in start.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in DECLARATIVE_SUFFIXES:
            continue
        rel = path.relative_to(ROOT).as_posix()
        try:
            text = path.read_text("utf-8")
        except UnicodeDecodeError:
            continue
        for line_number, line in enumerate(text.splitlines(), 1):
            for ref in QUOTED_RES_PATH.findall(line):
                if not target_exists(ref):
                    add_error(f"missing hard reference in {rel}:{line_number}: {ref}")

# Runtime code literals. Migration map keys and explicitly named Legacy constants are historical
# inputs, not files that should still exist.
for path in (ROOT / "scripts").rglob("*") if (ROOT / "scripts").exists() else ():
    if not path.is_file() or path.suffix.lower() not in CODE_SUFFIXES:
        continue
    rel = path.relative_to(ROOT).as_posix()
    try:
        text = path.read_text("utf-8")
    except UnicodeDecodeError:
        continue
    for line_number, line in enumerate(text.splitlines(), 1):
        if "=>" in line or "Legacy" in line:
            continue
        for ref in QUOTED_RES_PATH.findall(line):
            if not target_exists(ref):
                add_error(f"missing runtime path in {rel}:{line_number}: {ref}")

# Imported source files must exist. Imported cache destinations under .godot are generated.
for path in (ROOT / "assets").rglob("*.import") if (ROOT / "assets").exists() else ():
    rel = path.relative_to(ROOT).as_posix()
    text = path.read_text("utf-8", errors="ignore")
    source = re.search(r'^source_file="(res://[^"]+)"$', text, re.MULTILINE)
    if source and not target_exists(source.group(1)):
        add_error(f"missing import source in {rel}: {source.group(1)}")

# Duplicate active graphics are usually stale copies disguised as organization.
hashes: dict[str, list[str]] = defaultdict(list)
graphics_root = ROOT / "assets/graphics"
if graphics_root.exists():
    for path in graphics_root.rglob("*"):
        if path.is_file() and path.suffix.lower() in {".png", ".jpg", ".jpeg", ".webp"}:
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            hashes[digest].append(path.relative_to(ROOT).as_posix())
for group in hashes.values():
    if len(group) > 1:
        add_error("duplicate active graphics: " + ", ".join(sorted(group)))

# Targeted Hyou runtime chain. This catches the failure that a generic folder audit can miss.
hyou_config = ROOT / "data/characters/hyou.tres"
hyou_scene = ROOT / "scenes/characters/companions/hyou.tscn"
required_hyou_paths = {
    "character config": hyou_config,
    "companion scene": hyou_scene,
    "background": ROOT / "assets/graphics/backgrounds/hyou_ice_cavern.png",
    "skill": ROOT / "data/combat/skills/hyou_ice_bolt.tres",
    "action": ROOT / "data/combat/actions/hyou_ice_bolt.tres",
    "projectile": ROOT / "data/combat/projectiles/hyou_ice_bolt.tres",
    "projectile visual": ROOT / "data/combat/projectiles/visuals/hyou_ice_bolt_visual.tres",
}
for label, path in required_hyou_paths.items():
    if not path.exists():
        add_error(f"Hyou chain missing {label}: {path.relative_to(ROOT).as_posix()}")

if hyou_config.exists():
    text = hyou_config.read_text("utf-8")
    expected = (
        'path="res://assets/graphics/backgrounds/hyou_ice_cavern.png"',
        'path="res://data/combat/skills/hyou_ice_bolt.tres"',
        'ActiveSkills = Array[Resource]([ExtResource("4_ice_bolt")])',
    )
    for token in expected:
        if token not in text:
            add_error(f"Hyou config missing required binding: {token}")

if hyou_scene.exists():
    text = hyou_scene.read_text("utf-8")
    for token in (
        'path="res://data/characters/hyou.tres"',
        'ConfigData = ExtResource("4_config")',
        'UseDecisionCore = true',
        'ClassProfile = ExtResource("8_class")',
    ):
        if token not in text:
            add_error(f"Hyou scene missing required binding: {token}")

if not (ROOT / "project.godot").exists():
    warnings.append("project.godot is absent from the supplied archive; engine boot validation is unavailable")
if not list(ROOT.glob("*.csproj")):
    warnings.append("no .csproj is present in the supplied archive; C# compilation validation is unavailable")

print(f"Checked: {ROOT}")
for item in warnings:
    print(f"WARNING: {item}")
unique_errors = sorted(set(errors))
if unique_errors:
    for item in unique_errors:
        print(f"ERROR: {item}")
    print(f"FAILED: {len(unique_errors)} unique issue(s)")
    sys.exit(1)
print("PASS: structure, hard references, runtime paths, naming, duplicates, and Hyou resource chain")
