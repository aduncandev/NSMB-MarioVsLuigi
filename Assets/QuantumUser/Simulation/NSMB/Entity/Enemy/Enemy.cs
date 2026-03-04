using Photon.Deterministic;
using UnityEngine.UIElements;

namespace Quantum {
    public unsafe partial struct Enemy {
        public readonly bool IsAlive => !IsDead && IsActive;

        public EntityRef findClosestPlayer(Frame f, EntityRef entity) {
            var allPlayers = f.Filter<MarioPlayer, Transform2D>();
            allPlayers.UseCulling = false;

            VersusStageData stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
            FP closestDistance = FP.MaxValue;
            EntityRef closestPlayer = EntityRef.None;
            while (allPlayers.NextUnsafe(out EntityRef marioEntity, out MarioPlayer* mario, out Transform2D* marioTransform)) {
                if (mario->IsDead) {
                    continue;
                }

                FP newDistance = QuantumUtils.WrappedDistance(stage, Spawnpoint, marioTransform->Position);

                if (newDistance <= closestDistance) {
                    closestPlayer = marioEntity;
                    closestDistance = newDistance;
                }
            }
            return closestPlayer;
        }

        public void Respawn(Frame f, EntityRef entity) {
            var transform = f.Unsafe.GetPointer<Transform2D>(entity);

            IsActive = true;
            IsDead = false;
            transform->Teleport(f, Spawnpoint);

            if (f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)) {
                physicsObject->IsFrozen = false;
                physicsObject->Velocity = FPVector2.Zero;
                physicsObject->DisableCollision = false;
            }

            // face left by default
            var shouldFaceRight = false;
            var closestMario = findClosestPlayer(f, entity);

            // use closest player and face them
            if (f.Unsafe.TryGetPointer(closestMario, out Transform2D* marioTransform)) {
                QuantumUtils.WrappedDistance(f, Spawnpoint, marioTransform->Position, out FP xDiff);
                shouldFaceRight = xDiff < 0;
            }

            FacingRight = shouldFaceRight;
        }

        public void ChangeFacingRight(Frame f, EntityRef entity, bool newFacingRight) {
            if (FacingRight != newFacingRight) {
                FacingRight = newFacingRight;
                f.Signals.OnEnemyTurnaround(entity);
            }
        }
    }
}