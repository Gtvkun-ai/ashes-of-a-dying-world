using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Runtime;

namespace AshesofaDyingWorld.Combat.Fields
{
    /// <summary>
    /// Persistent area effect. Frost Ward dùng trigger OnEnter: target phải ra ngoài rồi
    /// bước vào lại mới nhận lần kế tiếp, tránh spam Chill theo physics tick.
    /// </summary>
    public partial class CombatField2D : Node2D
    {
        private const string RuntimeBuild = "v1-frost-ward-field";

        private CombatCharacter _owner;
        private CombatActionData _action;
        private CombatFieldSpecData _spec;
        private float _damageMultiplier = 1f;
        private float _remaining;
        private readonly HashSet<ulong> _insideTargets = new();
        private readonly Dictionary<ulong, float> _targetCooldowns = new();

        private Sprite2D _circleSprite;
        private Sprite2D _crystalSprite;
        private float _armingRemaining;
        private float _pulseRemaining;
        private Vector2 _baseCircleScale = Vector2.One;
        private Vector2 _baseCrystalScale = Vector2.One;
        private Vector2 _baseCrystalPosition = Vector2.Zero;
        private float _crystalGroundBaseY = 2f;

        public void Initialize(
            CombatCharacter owner,
            CombatActionData action,
            CombatFieldSpecData spec,
            float damageMultiplier = 1f)
        {
            _owner = owner;
            _action = action;
            _spec = spec;
            _damageMultiplier = Mathf.Max(0f, damageMultiplier);
            _remaining = Mathf.Max(0.05f, spec?.DurationSeconds ?? 0f);
        }

        public override void _Ready()
        {
            if (_owner == null || _spec == null)
            {
                QueueFree();
                return;
            }

            BuildVisuals();
            _armingRemaining = 0.24f;
            SetPhysicsProcess(true);
            SetProcess(true);
            GD.Print($"[CombatField] ARMED build={RuntimeBuild} id={_spec.FieldId} owner={_owner.CombatantId} radius={_spec.Radius:0.0} duration={_remaining:0.0}s");
        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = Mathf.Max(0f, (float)delta);
            _remaining -= dt;
            TickCooldowns(dt);

            if (_remaining <= 0f
                || !IsInstanceValid(_owner)
                || _owner.IsQueuedForDeletion()
                || !_owner.IsAlive)
            {
                QueueFree();
                return;
            }

            // Crystal rise is the final completion cue; field becomes dangerous only
            // after the crystal is fully armed.
            if (_armingRemaining > 0f)
            {
                return;
            }

            SceneTree tree = GetTree();
            if (tree == null)
            {
                return;
            }

            float radius = Mathf.Max(1f, _spec.Radius);
            var seenInside = new HashSet<ulong>();
            foreach (Node node in tree.GetNodesInGroup("Combatant"))
            {
                if (node is not CombatCharacter target
                    || target == _owner
                    || !IsInstanceValid(target)
                    || target.IsQueuedForDeletion()
                    || !target.IsAlive
                    || !FactionRules.CanDamage(_owner.Faction, target.Faction))
                {
                    continue;
                }

                ulong id = target.GetInstanceId();
                bool inside = target.GlobalPosition.DistanceTo(GlobalPosition) <= radius;
                if (!inside)
                {
                    _insideTargets.Remove(id);
                    continue;
                }

                seenInside.Add(id);
                bool wasInside = _insideTargets.Contains(id);
                if (!wasInside)
                {
                    _insideTargets.Add(id);
                }

                bool cooldownReady = !_targetCooldowns.TryGetValue(id, out float cd) || cd <= 0f;
                bool shouldTrigger = _spec.RequireExitBeforeRetrigger ? !wasInside : cooldownReady;
                if (shouldTrigger && cooldownReady)
                {
                    TriggerTarget(target);
                    _targetCooldowns[id] = Mathf.Max(0f, _spec.PerTargetCooldownSeconds);
                }
            }

            // Targets queue-free/dead while inside must not remain latched forever.
            _insideTargets.RemoveWhere(id => !seenInside.Contains(id));
        }

        public override void _Process(double delta)
        {
            if (_spec == null)
            {
                return;
            }

            float dt = Mathf.Max(0f, (float)delta);
            if (_armingRemaining > 0f)
            {
                _armingRemaining = Mathf.Max(0f, _armingRemaining - dt);
                float t = 1f - _armingRemaining / 0.24f;
                float eased = t * t * (3f - 2f * t);

                if (_circleSprite != null)
                {
                    float flash = Mathf.Sin(eased * Mathf.Pi) * 0.16f;
                    float phaseBloom = Mathf.Sin(eased * Mathf.Pi) * 0.035f;
                    _circleSprite.Scale = _baseCircleScale * (1f + phaseBloom);
                    _circleSprite.Modulate = new Color(1f + flash, 1f + flash, 1f + flash, Mathf.Lerp(0.55f, 1f, eased));
                }

                if (_crystalSprite != null)
                {
                    _crystalSprite.Visible = true;
                    _crystalSprite.Scale = new Vector2(
                        _baseCrystalScale.X * Mathf.Lerp(0.82f, 1f, eased),
                        _baseCrystalScale.Y * Mathf.Lerp(0.16f, 1f, eased));
                    float halfHeight = (_crystalSprite.Texture?.GetHeight() ?? 0f) * _crystalSprite.Scale.Y * 0.5f;
                    _crystalSprite.Position = new Vector2(_baseCrystalPosition.X, _crystalGroundBaseY - halfHeight);
                    _crystalSprite.Modulate = new Color(1f, 1f, 1f, eased);
                }

                if (_armingRemaining <= 0f)
                {
                    ResetVisualPose();
                    GD.Print($"[CombatField] ACTIVE id={_spec.FieldId}");
                }
                return;
            }

            if (_pulseRemaining <= 0f)
            {
                return;
            }

            _pulseRemaining = Mathf.Max(0f, _pulseRemaining - dt);
            float duration = Mathf.Max(0.01f, _spec.TriggerPulseSeconds);
            float pulseT = 1f - _pulseRemaining / duration;
            float wave = Mathf.Sin(Mathf.Clamp(pulseT, 0f, 1f) * Mathf.Pi);

            if (_circleSprite != null)
            {
                _circleSprite.Scale = _baseCircleScale * (1f + wave * 0.07f);
                _circleSprite.Modulate = _spec.IdleModulate.Lerp(_spec.TriggerModulate, wave * 0.70f);
            }

            if (_crystalSprite != null)
            {
                _crystalSprite.Scale = _baseCrystalScale * (1f + wave * 0.12f);
                _crystalSprite.Position = _baseCrystalPosition - Vector2.Up * (wave * 1.4f);
                _crystalSprite.Modulate = _spec.IdleModulate.Lerp(_spec.TriggerModulate, wave * 0.86f);
            }

            if (_pulseRemaining <= 0f)
            {
                ResetVisualPose();
            }
        }

        private void TriggerTarget(CombatCharacter target)
        {
            if (target == null || _spec?.HitProfile == null)
            {
                return;
            }

            Vector2 direction = target.GlobalPosition - GlobalPosition;
            if (direction.LengthSquared() <= 0.001f)
            {
                direction = _owner.FacingDirection;
            }
            direction = direction.Normalized();

            target.ReceiveHit(new AshesofaDyingWorld.Combat.Model.HitRequest
            {
                Attacker = _owner,
                Target = target,
                Action = _action,
                Profile = _spec.HitProfile,
                DamageMultiplier = Mathf.Max(0f, _damageMultiplier * _spec.DamageMultiplier),
                HitOrigin = GlobalPosition,
                AttackDirection = direction
            });

            StartPulse();
            GD.Print($"[CombatField] TRIGGER id={_spec.FieldId} target={target.CombatantId} chill={_spec.HitProfile.ChillStacks} knockback={_spec.HitProfile.KnockbackForce:0}");
        }

        private void BuildVisuals()
        {
            Texture2D circleTexture = _spec.CircleTexture;
            Texture2D crystalTexture = _spec.CrystalTexture;

            if (circleTexture == null && _spec.ArmedTexture != null)
            {
                _circleSprite = new Sprite2D
                {
                    Name = "ArmedFallback",
                    Texture = _spec.ArmedTexture,
                    Centered = true,
                    Scale = Vector2.One * Mathf.Max(0.01f, _spec.VisualScale),
                    Modulate = _spec.IdleModulate,
                    ZIndex = -1
                };
                AddChild(_circleSprite);
                _baseCircleScale = _circleSprite.Scale;
                return;
            }

            if (circleTexture != null)
            {
                _circleSprite = new Sprite2D
                {
                    Name = "WardCircle",
                    Texture = circleTexture,
                    Centered = true,
                    Scale = Vector2.One * Mathf.Max(0.01f, _spec.VisualScale),
                    Modulate = _spec.IdleModulate,
                    ZIndex = -3
                };
                AddChild(_circleSprite);
                _baseCircleScale = _circleSprite.Scale;
            }

            if (crystalTexture != null)
            {
                float visualScale = Mathf.Max(0.01f, _spec.CrystalVisualScale);
                _baseCrystalScale = Vector2.One * visualScale;
                _baseCrystalPosition = new Vector2(
                    0f,
                    _crystalGroundBaseY - crystalTexture.GetHeight() * visualScale * 0.5f);
                _crystalSprite = new Sprite2D
                {
                    Name = "WardCrystal",
                    Texture = crystalTexture,
                    Centered = true,
                    Scale = _baseCrystalScale,
                    Position = _baseCrystalPosition,
                    Modulate = _spec.IdleModulate,
                    ZIndex = 1
                };
                AddChild(_crystalSprite);
            }
        }

        private void ResetVisualPose()
        {
            if (_circleSprite != null)
            {
                _circleSprite.Scale = _baseCircleScale;
                _circleSprite.Modulate = _spec?.IdleModulate ?? Colors.White;
            }

            if (_crystalSprite != null)
            {
                _crystalSprite.Scale = _baseCrystalScale;
                _crystalSprite.Position = _baseCrystalPosition;
                _crystalSprite.Modulate = _spec?.IdleModulate ?? Colors.White;
            }
        }

        private void StartPulse()
        {
            _pulseRemaining = Mathf.Max(_pulseRemaining, Mathf.Max(0.05f, _spec?.TriggerPulseSeconds ?? 0.2f));
        }

        private void TickCooldowns(float dt)
        {
            if (_targetCooldowns.Count == 0)
            {
                return;
            }

            var ids = new List<ulong>(_targetCooldowns.Keys);
            foreach (ulong id in ids)
            {
                float next = _targetCooldowns[id] - dt;
                if (next <= 0f)
                {
                    _targetCooldowns.Remove(id);
                }
                else
                {
                    _targetCooldowns[id] = next;
                }
            }
        }
    }
}
