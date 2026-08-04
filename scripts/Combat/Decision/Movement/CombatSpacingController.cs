using Godot;
using AshesofaDyingWorld.Combat.Decision.Model;
using AshesofaDyingWorld.Combat.Decision.Runtime;

namespace AshesofaDyingWorld.Combat.Decision.Movement
{
    /// <summary>
    /// Chuyển intent trừu tượng thành combat pose. Không gọi SetMoveInput và không biết collision.
    /// </summary>
    public sealed class CombatSpacingController
    {
        public CombatPose BuildPose(
            in CombatSnapshot snapshot,
            in CombatIntent intent,
            CombatRoleAssignment? assignment,
            CombatBlackboard blackboard)
        {
            float minRange = Mathf.Max(0f, intent.DesiredRangeMin);
            float maxRange = Mathf.Max(minRange + 1f, intent.DesiredRangeMax);
            float middleRange = (minRange + maxRange) * 0.5f;

            Vector2 toTarget = snapshot.DirectionToTarget.LengthSquared() > 0.001f
                ? snapshot.DirectionToTarget.Normalized()
                : Vector2.Down;
            Vector2 idealRangeAnchor = snapshot.HasTarget
                ? snapshot.TargetPosition - toTarget * middleRange
                : snapshot.SelfPosition;
            Vector2 left = new Vector2(-toTarget.Y, toTarget.X);
            Vector2 anchor = idealRangeAnchor;
            CombatMovementMode mode = CombatMovementMode.Hold;

            switch (intent.Type)
            {
                case CombatIntentType.Approach:
                    mode = CombatMovementMode.Approach;
                    anchor = idealRangeAnchor;
                    break;
                case CombatIntentType.Backpedal:
                    mode = CombatMovementMode.Backpedal;
                    anchor = snapshot.TargetPosition - toTarget * Mathf.Max(maxRange, snapshot.TargetDistance + 36f);
                    break;
                case CombatIntentType.StrafeLeft:
                case CombatIntentType.OrbitCounterClockwise:
                    mode = CombatMovementMode.StrafeLeft;
                    anchor = idealRangeAnchor + left * 34f;
                    break;
                case CombatIntentType.StrafeRight:
                case CombatIntentType.OrbitClockwise:
                    mode = CombatMovementMode.StrafeRight;
                    anchor = idealRangeAnchor - left * 34f;
                    break;
                case CombatIntentType.Reposition:
                    mode = blackboard.OrbitSide < 0
                        ? CombatMovementMode.StrafeLeft
                        : CombatMovementMode.StrafeRight;
                    anchor = blackboard.OrbitSide < 0
                        ? idealRangeAnchor + left * 42f
                        : idealRangeAnchor - left * 42f;
                    break;
                case CombatIntentType.ProtectLeader:
                    mode = CombatMovementMode.FollowFormation;
                    anchor = assignment?.AnchorPosition
                        ?? (snapshot.HasLeader
                            ? snapshot.LeaderPosition - toTarget * 42f
                            : idealRangeAnchor);
                    // Không để formation kéo mage vào sát target hơn panic doctrine.
                    if (snapshot.HasTarget && anchor.DistanceTo(snapshot.TargetPosition) < minRange)
                    {
                        anchor = idealRangeAnchor;
                    }
                    break;
                case CombatIntentType.RecoverResources:
                    mode = CombatMovementMode.RetreatToAnchor;
                    anchor = snapshot.TargetPosition - toTarget * Mathf.Max(maxRange, middleRange + 24f);
                    break;
                case CombatIntentType.PanicEvade:
                    mode = CombatMovementMode.PanicEvade;
                    anchor = snapshot.HasSafeRetreatVector
                        ? snapshot.SelfPosition + snapshot.SafeRetreatVector.Normalized() * 54f
                        : snapshot.SelfPosition - toTarget * 54f;
                    break;
                case CombatIntentType.HoldRange:
                case CombatIntentType.Guard:
                case CombatIntentType.CastPrimary:
                case CombatIntentType.CastSecondary:
                case CombatIntentType.CastDefensive:
                case CombatIntentType.MeleePrimary:
                default:
                    mode = CombatMovementMode.Hold;
                    anchor = idealRangeAnchor;
                    break;
            }

            blackboard.CurrentAnchor = anchor;
            bool faceTarget = intent.Type != CombatIntentType.PanicEvade;
            return new CombatPose(anchor, minRange, maxRange, faceTarget, mode);
        }
    }
}
