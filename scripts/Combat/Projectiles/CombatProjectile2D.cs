using Godot;
using System.Collections.Generic;
using AshesofaDyingWorld.Combat.Actors;
using AshesofaDyingWorld.Combat.Data;
using AshesofaDyingWorld.Combat.Runtime;

namespace AshesofaDyingWorld.Combat.Projectiles
{
    /// <summary>
    /// Projectile runtime chung. Damage/collision độc lập presentation.
    /// Nếu VisualProfile có core sprite thì dùng asset thật; launch sheet phát một lần
    /// lúc viên đạn rời caster. Hình học cũ chỉ là fallback có chủ ý.
    /// </summary>
    public partial class CombatProjectile2D : Node2D
    {
        private const string VisualBuild = "v9-soft-homing-target-continuity";

        private readonly HashSet<ulong> _hitTargets = new();

        private CombatCharacter _attacker;
        private CombatActionData _action;
        private ProjectileSpecData _spec;
        private ProjectileVisualProfileData _visual;
        private CombatCharacter _homingTarget;
        private Vector2 _direction = Vector2.Right;
        private Vector2 _visualCardinal = Vector2.Right;
        private float _damageMultiplier = 1f;
        private ShapeCast2D _shapeCast;
        private Node2D _visualPivot;
        private Sprite2D _coreVisual;
        private AnimatedSprite2D _launchVisual;
        private float _lifeRemaining;
        private int _targetHits;
        private bool _initialized;
        private bool _usesAssetVisual;

        public string ProjectileId => _spec?.ProjectileId ?? "projectile";

        public void Initialize(
            CombatCharacter attacker,
            CombatActionData action,
            ProjectileSpecData spec,
            Vector2 direction,
            float damageMultiplier = 1f,
            CombatCharacter homingTarget = null)
        {
            _attacker = attacker;
            _action = action;
            _spec = spec;
            _damageMultiplier = Mathf.Max(0f, damageMultiplier);
            _visual = spec?.VisualProfile ?? new ProjectileVisualProfileData();
            _homingTarget = IsValidHomingTarget(attacker, homingTarget) ? homingTarget : null;
            _direction = direction.LengthSquared() <= 0.001f
                ? Vector2.Right
                : direction.Normalized();
            _lifeRemaining = Mathf.Max(0.05f, spec?.Lifetime ?? 0.05f);
            _initialized = attacker != null && action != null && spec != null;
        }

        public override void _Ready()
        {
            if (!_initialized || !IsUsable(_attacker))
            {
                QueueFree();
                return;
            }

            Name = $"Projectile_{ProjectileId}";
            ZIndex = 20;
            SetPhysicsProcess(false);
            CallDeferred(nameof(BuildRuntimeNodes));
        }

        private void BuildRuntimeNodes()
        {
            if (!_initialized || !IsUsable(_attacker) || IsQueuedForDeletion())
            {
                QueueFree();
                return;
            }

            _usesAssetVisual = TryBuildAssetVisual();
            Rotation = _usesAssetVisual ? 0f : _direction.Angle();

            var shape = new CircleShape2D
            {
                Radius = Mathf.Max(1f, _spec.Radius)
            };
            _shapeCast = new ShapeCast2D
            {
                Name = "Sweep",
                Shape = shape,
                Enabled = true,
                CollideWithAreas = true,
                CollideWithBodies = true,
                CollisionMask = _spec.HurtboxCollisionMask | _spec.WorldCollisionMask,
                TargetPosition = Vector2.Zero
            };
            AddChild(_shapeCast);
            _shapeCast.AddException(_attacker);

            if (!_usesAssetVisual && !_visual.UseProceduralFallback)
            {
                GD.PushError(
                    $"[CombatProjectile] ASSET REQUIRED build={VisualBuild} id={ProjectileId} "
                    + $"core='{_visual.SpriteSheetPath}'. Projectile sẽ không dùng viên tròn fallback.");
            }

            QueueRedraw();
            SetPhysicsProcess(true);
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!_initialized || _shapeCast == null || !IsUsable(_attacker))
            {
                QueueFree();
                return;
            }

            float dt = Mathf.Max(0f, (float)delta);
            UpdateSoftHoming(dt);
            UpdateVisualHeading();

            Vector2 step = _direction * Mathf.Max(0f, _spec.Speed) * dt;
            if (step.LengthSquared() > 0.0001f && Sweep(step))
            {
                return;
            }

            GlobalPosition += step;
            _lifeRemaining -= dt;
            if (_lifeRemaining <= 0f)
            {
                QueueFree();
            }
        }

        public override void _Draw()
        {
            if (_spec == null || _usesAssetVisual || !_visual.UseProceduralFallback)
            {
                return;
            }

            float radius = Mathf.Max(2f, _visual.VisualWidth);
            float length = Mathf.Max(radius * 2f, _visual.VisualLength);
            DrawLine(new Vector2(-length, 0f), Vector2.Zero, _visual.GlowColor, radius * 1.5f, true);
            DrawCircle(Vector2.Zero, radius * 1.65f, _visual.GlowColor);
            DrawCircle(Vector2.Zero, radius, _visual.CoreColor);
        }

        private bool TryBuildAssetVisual()
        {
            Vector2 cardinal = ResolveCardinal(_direction);
            _visualCardinal = cardinal;
            string directionName = ResolveDirectionName(cardinal);
            int row = ResolveRow(directionName);

            string corePath = directionName == "up"
                && !string.IsNullOrWhiteSpace(_visual.UpSpriteSheetOverridePath)
                    ? _visual.UpSpriteSheetOverridePath
                    : _visual.SpriteSheetPath;

            string launchPath = directionName == "up"
                && !string.IsNullOrWhiteSpace(_visual.UpLaunchSpriteSheetOverridePath)
                    ? _visual.UpLaunchSpriteSheetOverridePath
                    : _visual.LaunchSpriteSheetPath;

            Texture2D coreSheet = LoadSheet(corePath, "core");
            Texture2D launchSheet = LoadSheet(launchPath, "launch", required: false);
            if (coreSheet == null && launchSheet == null)
            {
                return false;
            }

            _visualPivot = new Node2D { Name = "VisualPivot" };
            AddChild(_visualPivot);
            if (_visual.RotateSpriteTowardExactAim)
            {
                _visualPivot.Rotation = _direction.Angle() - cardinal.Angle();
            }

            Vector2 alignedPosition = -cardinal
                * Mathf.Max(0f, _visual.SpriteEmbeddedForwardOffset)
                * Mathf.Max(0.01f, _visual.SpriteScale);

            if (coreSheet != null)
            {
                AtlasTexture coreFrame = BuildAtlas(coreSheet, row, _visual.SpriteColumn, corePath);
                if (coreFrame != null)
                {
                    _coreVisual = new Sprite2D
                    {
                        Name = "CoreSprite",
                        Texture = coreFrame,
                        Centered = true,
                        Scale = Vector2.One * Mathf.Max(0.01f, _visual.SpriteScale),
                        Position = alignedPosition,
                        ZIndex = 2
                    };
                    _visualPivot.AddChild(_coreVisual);
                }
            }

            if (launchSheet != null && _visual.LaunchFrameCount > 0)
            {
                SpriteFrames launchFrames = BuildLaunchFrames(launchSheet, row, launchPath);
                if (launchFrames != null)
                {
                    _launchVisual = new AnimatedSprite2D
                    {
                        Name = "LaunchAnimation",
                        SpriteFrames = launchFrames,
                        Animation = "launch",
                        Centered = true,
                        Scale = Vector2.One * Mathf.Max(0.01f, _visual.LaunchSpriteScale),
                        Position = alignedPosition,
                        ZIndex = 1
                    };
                    _visualPivot.AddChild(_launchVisual);
                    _launchVisual.Frame = 0;
                    _launchVisual.Play();
                }
            }

            bool built = _coreVisual != null || _launchVisual != null;
            if (built && _visual.DebugVisualLogging)
            {
                GD.Print(
                    $"[CombatProjectile] VISUAL build={VisualBuild} id={ProjectileId} "
                    + $"dir={directionName} core={corePath} core_col={_visual.SpriteColumn} "
                    + $"launch={launchPath} launch_cols={_visual.LaunchStartColumn}-"
                    + $"{_visual.LaunchStartColumn + Mathf.Max(0, _visual.LaunchFrameCount - 1)}");
            }

            return built;
        }

        private Texture2D LoadSheet(string path, string role, bool required = true)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                if (required)
                {
                    GD.PushError($"[CombatProjectile] Thiếu {role} sprite path cho id={ProjectileId}");
                }
                return null;
            }

            Texture2D sheet = GD.Load<Texture2D>(path);
            if (sheet == null)
            {
                GD.PushError($"[CombatProjectile] Không load được {role} sheet: {path}");
            }
            return sheet;
        }

        private AtlasTexture BuildAtlas(Texture2D sheet, int row, int column, string path)
        {
            int columns = Mathf.Max(1, _visual.SpriteColumns);
            int rows = Mathf.Max(1, _visual.SpriteRows);
            int frameWidth = _visual.SpriteFrameWidth > 0
                ? _visual.SpriteFrameWidth
                : sheet.GetWidth() / columns;
            int frameHeight = _visual.SpriteFrameHeight > 0
                ? _visual.SpriteFrameHeight
                : sheet.GetHeight() / rows;

            int safeRow = Mathf.Clamp(row, 0, rows - 1);
            int safeColumn = Mathf.Clamp(column, 0, columns - 1);
            int x = safeColumn * frameWidth;
            int y = safeRow * frameHeight;
            if (frameWidth <= 0 || frameHeight <= 0
                || x + frameWidth > sheet.GetWidth()
                || y + frameHeight > sheet.GetHeight())
            {
                GD.PushError(
                    $"[CombatProjectile] Frame ngoài sheet: {path} row={safeRow} col={safeColumn} "
                    + $"grid={columns}x{rows} frame={frameWidth}x{frameHeight}");
                return null;
            }

            return new AtlasTexture
            {
                Atlas = sheet,
                Region = new Rect2(x, y, frameWidth, frameHeight)
            };
        }

        private SpriteFrames BuildLaunchFrames(Texture2D sheet, int row, string path)
        {
            int columns = Mathf.Max(1, _visual.SpriteColumns);
            int start = Mathf.Clamp(_visual.LaunchStartColumn, 0, columns - 1);
            int count = Mathf.Clamp(_visual.LaunchFrameCount, 1, columns - start);

            var frames = new SpriteFrames();
            frames.AddAnimation("launch");
            frames.SetAnimationLoop("launch", false);
            frames.SetAnimationSpeed("launch", Mathf.Max(1f, _visual.LaunchAnimationFps));

            for (int index = 0; index < count; index++)
            {
                AtlasTexture frame = BuildAtlas(sheet, row, start + index, path);
                if (frame != null)
                {
                    frames.AddFrame("launch", frame);
                }
            }

            return frames.GetFrameCount("launch") > 0 ? frames : null;
        }

        private int ResolveRow(string directionName)
        {
            int row = directionName switch
            {
                "right" => _visual.RightRow,
                "left" => _visual.LeftRow,
                "up" => _visual.UpRow,
                _ => _visual.DownRow,
            };
            return Mathf.Clamp(row, 0, Mathf.Max(0, _visual.SpriteRows - 1));
        }

        private bool Sweep(Vector2 step)
        {
            _shapeCast.TargetPosition = ToLocal(GlobalPosition + step);
            _shapeCast.ForceShapecastUpdate();

            var collisions = new List<(float Along, Node Node)>();
            int collisionCount = _shapeCast.GetCollisionCount();
            for (int index = 0; index < collisionCount; index++)
            {
                GodotObject colliderObject = _shapeCast.GetCollider(index);
                if (colliderObject is not Node colliderNode)
                {
                    continue;
                }

                Vector2 point = _shapeCast.GetCollisionPoint(index);
                float along = Mathf.Max(0f, (point - GlobalPosition).Dot(_direction));
                collisions.Add((along, colliderNode));
            }

            collisions.Sort((left, right) => left.Along.CompareTo(right.Along));
            foreach (var collision in collisions)
            {
                Node colliderNode = collision.Node;
                CombatCharacter target = FindCombatCharacter(colliderNode);
                if (target == null)
                {
                    if (_spec.StopOnWorldCollision)
                    {
                        GlobalPosition += step;
                        QueueFree();
                        return true;
                    }
                    continue;
                }

                if (target == _attacker
                    || !target.IsAlive
                    || !FactionRules.CanDamage(_attacker.Faction, target.Faction))
                {
                    continue;
                }

                ulong targetId = target.GetInstanceId();
                if (!_hitTargets.Add(targetId))
                {
                    continue;
                }

                HitProfileData hitProfile = _spec.HitProfileOverride ?? _action.HitProfile;
                if (hitProfile == null)
                {
                    QueueFree();
                    return true;
                }

                var result = _attacker.TryResolveHit(
                    target,
                    _action,
                    hitProfile,
                    GlobalPosition,
                    _direction,
                    _damageMultiplier);
                if (!result.Applied)
                {
                    continue;
                }

                _targetHits++;
                int maxHits = Mathf.Max(1, _spec.MaxTargetHits);
                if (!_spec.PierceTargets || _targetHits >= maxHits)
                {
                    GlobalPosition += step;
                    QueueFree();
                    return true;
                }
            }

            return false;
        }

        private void UpdateSoftHoming(float dt)
        {
            if (_spec == null
                || !_spec.HomingEnabled
                || _spec.HomingStrength <= 0.001f
                || !IsValidHomingTarget(_attacker, _homingTarget))
            {
                return;
            }

            Vector2 toTarget = _homingTarget.CombatCenter - GlobalPosition;
            float stopDistance = Mathf.Max(0f, _spec.HomingStopDistance);
            if (toTarget.LengthSquared() <= stopDistance * stopDistance)
            {
                return;
            }

            Vector2 desiredDirection = toTarget.Normalized();
            float currentAngle = _direction.Angle();
            float desiredAngle = desiredDirection.Angle();
            float angleDelta = Mathf.AngleDifference(currentAngle, desiredAngle);

            // HomingStrength is intentionally not "snap percentage".
            // It scales a bounded turn-rate, so 60% helps the projectile correct
            // vertical/collision-center mismatch while a moving target can still evade.
            float strength = Mathf.Clamp(_spec.HomingStrength, 0f, 1f);
            float maxTurnRadians = Mathf.DegToRad(
                Mathf.Max(0f, _spec.HomingMaxTurnDegreesPerSecond)
                * strength
                * Mathf.Max(0f, dt));

            float appliedTurn = Mathf.Clamp(angleDelta, -maxTurnRadians, maxTurnRadians);
            _direction = Vector2.FromAngle(currentAngle + appliedTurn).Normalized();
        }

        private void UpdateVisualHeading()
        {
            if (_usesAssetVisual)
            {
                if (_visualPivot != null && _visual.RotateSpriteTowardExactAim)
                {
                    _visualPivot.Rotation = _direction.Angle() - _visualCardinal.Angle();
                }
                return;
            }

            Rotation = _direction.Angle();
        }

        private static bool IsValidHomingTarget(CombatCharacter attacker, CombatCharacter target)
        {
            return attacker != null
                && target != null
                && GodotObject.IsInstanceValid(target)
                && !target.IsQueuedForDeletion()
                && target.IsAlive
                && FactionRules.IsHostile(attacker.Faction, target.Faction);
        }

        private static Vector2 ResolveCardinal(Vector2 direction)
        {
            if (direction.LengthSquared() <= 0.001f)
            {
                return Vector2.Down;
            }

            Vector2 normalized = direction.Normalized();
            if (Mathf.Abs(normalized.X) > Mathf.Abs(normalized.Y))
            {
                return normalized.X >= 0f ? Vector2.Right : Vector2.Left;
            }
            return normalized.Y >= 0f ? Vector2.Down : Vector2.Up;
        }

        private static string ResolveDirectionName(Vector2 cardinal)
        {
            if (cardinal == Vector2.Right) return "right";
            if (cardinal == Vector2.Left) return "left";
            return cardinal == Vector2.Up ? "up" : "down";
        }

        private static CombatCharacter FindCombatCharacter(Node node)
        {
            Node cursor = node;
            for (int depth = 0; cursor != null && depth < 8; depth++)
            {
                if (cursor is CombatCharacter combatCharacter)
                {
                    return combatCharacter;
                }
                cursor = cursor.GetParent();
            }
            return null;
        }

        private static bool IsUsable(Node node)
        {
            return node != null
                && GodotObject.IsInstanceValid(node)
                && !node.IsQueuedForDeletion();
        }
    }
}
