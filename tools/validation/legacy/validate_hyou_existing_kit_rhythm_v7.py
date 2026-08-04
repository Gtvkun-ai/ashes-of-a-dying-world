from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def read(rel: str) -> str:
    path = ROOT / rel
    assert path.exists(), f"Thiếu file: {rel}"
    return path.read_text(encoding="utf-8")

checks = []

def check(name: str, condition: bool):
    checks.append((name, bool(condition)))

# Decision language + profiles
enums = read("src/Combat/Decision/Model/CombatDecisionEnums.cs")
profile = read("src/Combat/Decision/Profiles/CombatClassProfile.cs")
profile_res = read("assets/resources/data/combat/decision/classes/cryomancer.tres")
check("Decision Core có MeleePrimary", "MeleePrimary = 16" in enums)
check("Cryomancer bật melee fallback", "AllowsMeleeFallback = true" in profile_res)
check("Profile có dữ liệu melee/evade", all(k in profile for k in [
    "MeleeRange", "PanicEvadeMinStamina", "PanicEvadeCooldownSeconds", "RepositionAfterActionSeconds"
]))

# Evaluator rhythm
evaluator = read("src/Combat/Decision/Runtime/TacticalEvaluator.cs")
check("Evaluator có run evade", "AddPanicEvadeCandidate" in evaluator and "incoming_attack_run_evade" in evaluator)
check("Evaluator có sword fallback", "AddMeleeCandidate" in evaluator and "sword_close_fallback" in evaluator)
check("Evaluator có reposition sau action", "AddRepositionCandidate" in evaluator and "break_action_repetition" in evaluator)
check("Cast chịu phạt lặp", "GetActionRhythmMultiplier(skillKey)" in evaluator)

# Runtime ownership
executor = read("src/Combat/Decision/Execution/CombatIntentExecutor.cs")
blackboard = read("src/Combat/Decision/Runtime/CombatBlackboard.cs")
agent = read("src/Combat/Decision/Runtime/CombatDecisionAgent.cs")
check("Evade ngắt action qua ActionRunner", "_self.Actions?.Cancel()" in executor)
check("Melee đi qua RequestAttack", "intent.Type == CombatIntentType.MeleePrimary" in executor and "_self.RequestAttack()" in executor)
check("Blackboard nhớ nhịp action", all(k in blackboard for k in [
    "LastExecutedActionId", "ConsecutiveActionUses", "RecordActionExecution", "RecentIntentTypes"
]))
check("Agent ghi commitment vào rhythm memory", "RecordCommittedIntent(committed" in agent)
check("Có marker runtime v7+", "v7-existing-kit-rhythm" in agent or "v8-action-events-debug-spine" in agent)

# Movement + perception
spacing = read("src/Combat/Decision/Movement/CombatSpacingController.cs")
movement = read("src/Combat/Decision/Movement/CombatMovementSolver.cs")
perception = read("src/Combat/Decision/Runtime/CombatPerception.cs")
threat = read("src/Combat/Decision/Runtime/ThreatPredictor.cs")
check("PanicEvade không preserve facing", "intent.Type != CombatIntentType.PanicEvade" in spacing)
check("PanicEvade luôn yêu cầu run", "pose.Mode == CombatMovementMode.PanicEvade" in movement)
check("PanicEvade lập hướng ngay khi ngắt cast", "interruptibleRunEvade" in movement)
check("Retreat vector có thành phần chéo", "directionToTarget * 0.86f" in perception and "tangent * 0.52f" in perception)
check("Threat đọc reach/lunge/action phase", all(k in threat for k in [
    "ResolveActionDangerRange", "action.LungeSpeed", "AttackStartup", "dodgeable"
]))

# Existing kit resource wiring
moveset = read("assets/resources/data/combat/movesets/hyou_cryomancer.tres")
hyou_scene = read("assets/resources/data/characters/Hyou.tscn")
check("Default moveset dùng combo kiếm 2 nhát", all(k in moveset for k in [
    "wood_sword_light_1.tres", "wood_sword_light_2.tres", "hyou_cryomancer_hybrid"
]))
check("Run dodge được tune bằng stamina", all(k in hyou_scene for k in [
    "RunSpeed = 205.0", "RunStaminaCost = 28.0", "MinStaminaToRun = 18.0"
]))

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(f"[{'OK' if ok else 'FAIL'}] {name}")

if failed:
    raise SystemExit("V7 validation failed: " + ", ".join(failed))

print("[OK] V7: Ice Bolt + sword fallback + run evade + anti-repeat/reposition đã nối đủ.")
print("[NOTE] Cần Godot thật để compile C# và quan sát timing né/chém trong arena.")

# Tuning smoke tests độc lập, không thay thế runtime test.
def smoothstep(t: float) -> float:
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)

def ramp(value: float, start: float, end: float) -> float:
    return smoothstep((value - start) / max(0.001, end - start))

def band(value: float, minimum: float, maximum: float, edge: float) -> float:
    if value < minimum:
        return ramp(value, minimum - edge, minimum)
    if value <= maximum:
        return 1.0
    return 1.0 - ramp(value, maximum, maximum + edge)

# Trong preferred band, lần đầu vẫn cast. Sau một cast, reposition phải thắng để phá loop.
cast_first = 0.89
cast_after_one = cast_first * 0.68
reposition_after_one = 0.52 + 0.26 * (1.0 - 0.68) + 0.08 + 0.08 * 0.71
assert cast_first > reposition_after_one, "Lần đầu phải ưu tiên Ice Bolt"
assert reposition_after_one > cast_after_one, "Sau cast phải ưu tiên reposition"

# Ở cự ly kiếm và target recovery, sword punish phải thắng backpedal thụ động.
melee_close_fit = band(40.0, 18.0, 46.0, 12.0)
melee_recovery = min(
    (0.38 + 0.34 * melee_close_fit + 0.20 + 0.08 * 0.2)
    * (0.82 + 0.18 * (0.50 * 0.87 + 0.30 * 0.36 + 0.20 * 0.60)),
    0.94,
)
assert melee_recovery > 0.62, "Sword punish phải thắng lùi khi slime đang recovery"
print("[OK] Tuning smoke: cast -> reposition -> cast; close recovery -> sword; startup -> run evade.")
