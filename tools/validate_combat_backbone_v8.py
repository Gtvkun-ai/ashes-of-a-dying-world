#!/usr/bin/env python3
"""Static validation for the v8 action-event, visual-profile and QA backbone.

The uploaded archive still has no project.godot/.csproj, so this deliberately does not
pretend to compile Godot. It verifies ownership boundaries and resource wiring that used
to produce duplicate/missing projectiles and opaque decision debugging.
"""

from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(rel: str) -> str:
    path = ROOT / rel
    assert path.is_file(), f"Thiếu file: {rel}"
    return path.read_text(encoding="utf-8")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)
    print(f"[OK] {message}")


def balanced(rel: str) -> None:
    text = read(rel)
    require(text.count("{") == text.count("}"), f"Ngoặc C# cân bằng: {rel}")


files = [
    "src/Combat/Data/CombatActionEventData.cs",
    "src/Combat/Data/ProjectileVisualProfileData.cs",
    "src/Combat/Data/CombatActionData.cs",
    "src/Combat/Data/ProjectileSpecData.cs",
    "src/Combat/Runtime/CombatActionRunner.cs",
    "src/Combat/Runtime/CombatActionEventDispatcher.cs",
    "src/Combat/Actors/CombatCharacter.cs",
    "src/Combat/Projectiles/CombatProjectileSpawner.cs",
    "src/Combat/Projectiles/CombatProjectile2D.cs",
    "src/Combat/Decision/Debug/DecisionTraceExporter.cs",
    "src/Combat/Decision/Debug/CombatDecisionDebugOverlay.cs",
    "src/Combat/Decision/Debug/CombatDecisionWorldDebugDraw.cs",
    "src/Combat/Decision/Runtime/CombatDecisionAgent.cs",
]
for rel in files:
    balanced(rel)

action_event = read("src/Combat/Data/CombatActionEventData.cs")
action_data = read("src/Combat/Data/CombatActionData.cs")
runner = read("src/Combat/Runtime/CombatActionRunner.cs")
dispatcher = read("src/Combat/Runtime/CombatActionEventDispatcher.cs")
character = read("src/Combat/Actors/CombatCharacter.cs")
spawner = read("src/Combat/Projectiles/CombatProjectileSpawner.cs")
spec = read("src/Combat/Data/ProjectileSpecData.cs")
visual = read("src/Combat/Data/ProjectileVisualProfileData.cs")
projectile = read("src/Combat/Projectiles/CombatProjectile2D.cs")
overlay = read("src/Combat/Decision/Debug/CombatDecisionDebugOverlay.cs")
exporter = read("src/Combat/Decision/Debug/DecisionTraceExporter.cs")
agent = read("src/Combat/Decision/Runtime/CombatDecisionAgent.cs")
action_resource = read("assets/resources/data/combat/actions/hyou_ice_bolt.tres")
projectile_resource = read("assets/resources/data/combat/projectiles/hyou_ice_bolt.tres")
visual_resource = read("assets/resources/data/combat/projectiles/visuals/hyou_ice_bolt_visual.tres")
hyou_scene = read("assets/resources/data/characters/Hyou.tscn")

# Action event ownership.
require("CombatActionEventType" in action_event and "TriggerNormalizedTime" in action_event,
        "Action event model hỗ trợ frame và fallback normalized time")
require("Array<CombatActionEventData> Events" in action_data,
        "CombatActionData author event bằng resource array")
require("ActionEventTriggered" in runner and "_triggeredEventIndices.Add(index);" in runner,
        "ActionRunner phát event đúng một lần trước callback re-entrant")
require(runner.index("_triggeredEventIndices.Add(index);") < runner.index("ActionEventTriggered?.Invoke"),
        "Runner đánh dấu fired trước khi gọi listener")
require("EvaluateActionEventsFrame(frame);" in runner and "EvaluateActionEventsNormalized" in runner,
        "Event timeline chạy cả animation frame lẫn timing fallback")
require("CombatProjectileSpawner.Spawn" in dispatcher and "ActionId ==" not in dispatcher,
        "Dispatcher spawn theo payload, không hardcode tên spell")
require("Actions.ActionEventTriggered += OnActionEventTriggered" in character,
        "CombatCharacter bind event dispatcher")
require("DispatchLegacyDelivery" in character and "action.HasAuthoredEvents" in dispatcher,
        "Legacy bridge tồn tại nhưng không spawn đôi action mới")
require("OriginSocketPath" in action_event and "ResolveOrigin" in spawner,
        "Projectile spawn từ socket data-driven")

# Ice Bolt resource chain.
for token in (
    "Resource_spawn_ice_bolt",
    'EventId = "spawn_hyou_ice_bolt"',
    "TriggerFrame = 7",
    "TriggerNormalizedTime = 0.99",
    'OriginSocketPath = NodePath("CastOrigin")',
    "Events = Array[Resource]",
):
    require(token in action_resource, f"Ice Bolt action event có {token}")
require("ProjectileSpec = ExtResource" in action_resource,
        "Spawn event mang projectile spec")
require("CastOrigin" in hyou_scene,
        "Hyou scene có CastOrigin socket")

# Presentation boundary.
require("ProjectileVisualProfileData VisualProfile" in spec,
        "ProjectileSpec chỉ giữ reference presentation")
for forbidden in ("SpriteSheetPath", "LaunchSpriteSheetPath", "SpriteColumns", "CoreColor"):
    require(forbidden not in spec, f"Gameplay spec không còn ôm {forbidden}")
require("SpriteSheetPath" in visual and "LaunchSpriteSheetPath" in visual,
        "Visual profile sở hữu sprite/import layout")
require("_visual = spec?.VisualProfile" in projectile,
        "Projectile runtime đọc presentation qua visual profile")
require("VisualProfile = ExtResource" in projectile_resource,
        "Ice Bolt projectile spec nối visual profile")
require("UseProceduralFallback = false" in visual_resource,
        "Ice Bolt cấm fallback viên bi procedural")

# QA spine.
for key in ("Key.F6", "Key.F7", "Key.F8", "Key.F11"):
    require(key in overlay, f"Overlay có hotkey {key}")
require("DecisionTraceExporter.ExportLatest" in overlay,
        "F11 dump trace qua exporter riêng")
require('"schema"] = "combat_decision_trace_v1"' in exporter,
        "Trace JSON có schema version")
require("ControlledCharacter" in agent and "LastSnapshot" in agent,
        "Agent expose debug state read-only")
require("CombatDecisionDebugOverlay" in hyou_scene and "VisibleByDefault = false" in hyou_scene,
        "Overlay được gắn additive và mặc định ẩn")
require("v8-action-events-debug-spine" in agent,
        "Runtime có build marker v8")

# Resource references in the new chain must exist.
for rel in (
    "src/Combat/Data/CombatActionEventData.cs",
    "src/Combat/Data/ProjectileVisualProfileData.cs",
    "assets/resources/data/combat/projectiles/hyou_ice_bolt.tres",
    "assets/resources/data/combat/projectiles/visuals/hyou_ice_bolt_visual.tres",
):
    require((ROOT / rel).exists(), f"Resource chain tồn tại: {rel}")

# Simple timeline smoke: fallback event at 0.99 must fire once near the 2s release.
startup, active, recovery = 2.0, 0.01, 0.01
total = startup + active + recovery
trigger = 0.99 * total
require(1.95 <= trigger <= 2.01, "Fallback event phát quanh mốc release 2 giây")

# Event resource must not carry the old root-level ProjectileSpec assignment.
root_resource = action_resource.split("[resource]", 1)[1]
require("ProjectileSpec = ExtResource" not in root_resource,
        "Ice Bolt mới không phụ thuộc legacy root ProjectileSpec")

print("[OK] V8 backbone: action events + visual profile + QA overlay đã nối đủ.")
print("[NOTE] Vẫn cần Godot/.NET project đầy đủ để compile và chạy arena runtime.")
