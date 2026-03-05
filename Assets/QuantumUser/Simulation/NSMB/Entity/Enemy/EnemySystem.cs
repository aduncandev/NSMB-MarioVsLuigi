//#define MULTITHREADED

using Photon.Deterministic;
using Quantum.Task;

namespace Quantum {
#if MULTITHREADED
    using System.Collections.Generic;
    public unsafe class EnemySystem : SystemThreadedFilter<EnemySystem.Filter>, ISignalOnStageReset, ISignalOnTryLiquidSplash, ISignalOnBeforeInteraction,
        ISignalOnEnemyDespawned, ISignalOnEnemyRespawned, ISignalOnMarioPlayerMegaMushroomFootstep {

        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Enemy* Enemy;
            public PhysicsCollider2D* Collider;
        }

        private static readonly EntityRefComparer entityRefComparer = new();
        private readonly List<EntityRef> despawningEntities = new();
        private TaskDelegateHandle sendSignalsTaskHandle;

        protected override void OnInitUser(Frame f) {
            f.Context.PlayerOnlyMask = f.Layers.GetLayerMask("Player");
            f.Context.CircleRadiusTwo = Shape2D.CreateCircle(2);
        }

        protected override TaskHandle Schedule(Frame f, TaskHandle taskHandle) {
            if (f.ComponentCount<Enemy>() <= 0) {
                return taskHandle;
            }
            if (!sendSignalsTaskHandle.IsValid) {
                f.Context.TaskContext.RegisterDelegate(SendSignalsTask, ProfilerName, ref sendSignalsTaskHandle);
            }

            var updateTask = base.Schedule(f, taskHandle);
            var sendSignalsTask = f.Context.TaskContext.AddMainThreadTask(sendSignalsTaskHandle, null, updateTask);
            return sendSignalsTask;
        }

        public override void Update(FrameThreadSafe f, ref Filter filter) {
            var enemy = filter.Enemy;

            // handle respawning
            if (enemy->RespawnTimer > 0 &! enemy->DisableRespawning) HandleDelayedRespawn(f, filter, stage);
            if (!enemy->IsActive) {
                return;
            }

            var transform = filter.Transform;
            var collider = filter.Collider;

            // Despawn off bottom of stage
            if (transform->Position.Y + collider->Shape.Box.Extents.Y + collider->Shape.Centroid.Y < stage.StageWorldMin.Y) {
                enemy->IsActive = false;
                enemy->IsDead = true;
                if (!enemy->DisableRespawning) enemy->SetDelayedRespawn();
                if (f.Unsafe.TryGetPointer(filter.Entity, out PhysicsObject* physicsObject)) {
                    physicsObject->IsFrozen = true;
                }

                f.Signals.OnEnemyDespawned(filter.Entity);
                return;
            }

            if (enemy->StayAtHomeWhenOffscreen) OffscreenCheck(f, filter, stage);
        }

        public void SendSignalsTask(FrameThreadSafe f, int start, int count, void* arg) {
            using var _profiler = HostProfiler.Start("EnemySystem.SendSignalsTask");

            despawningEntities.Sort(entityRefComparer);
            foreach (var entity in despawningEntities) {
                ((Frame) f).Signals.OnEnemyDespawned(entity);
            }
            despawningEntities.Clear();
        }

        public class EntityRefComparer : IComparer<EntityRef> {
            public int Compare(EntityRef x, EntityRef y) {
                if (x.Index == y.Index) {
                    return x.Version - y.Version;
                }
                return x.Index - y.Index;
            }
        }
#else
    public unsafe class EnemySystem : SystemMainThreadEntityFilter<Enemy, EnemySystem.Filter>, ISignalOnStageReset, ISignalOnTryLiquidSplash, ISignalOnBeforeInteraction,
        ISignalOnEnemyDespawned, ISignalOnEnemyRespawned, ISignalOnMarioPlayerMegaMushroomFootstep {
        public struct Filter {
            public EntityRef Entity;
            public Transform2D* Transform;
            public Enemy* Enemy;
            public PhysicsCollider2D* Collider;
        }

        public override void OnInit(Frame f) {
            f.Context.PlayerOnlyMask = f.Layers.GetLayerMask("Player");
            f.Context.CircleRadiusTwo = Shape2D.CreateCircle(2);
        }

        public override void Update(Frame f, ref Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;

            // handle respawning
            if (enemy->RespawnTimer > 0 &! enemy->DisableRespawning) HandleDelayedRespawn(f, filter, stage);
            if (!enemy->IsActive) {
                return;
            }

            var transform = filter.Transform;
            var collider = filter.Collider;

            // Despawn off bottom of stage
            if (transform->Position.Y + collider->Shape.Box.Extents.Y + collider->Shape.Centroid.Y < stage.StageWorldMin.Y) {
                enemy->IsActive = false;
                enemy->IsDead = true;
                if (!enemy->DisableRespawning) enemy->SetDelayedRespawn();
                if (f.Unsafe.TryGetPointer(filter.Entity, out PhysicsObject* physicsObject)) {
                    physicsObject->IsFrozen = true;
                }

                f.Signals.OnEnemyDespawned(filter.Entity);
                return;
            }

            if (enemy->StayAtHomeWhenOffscreen) OffscreenCheck(f, filter, stage);
        }

#endif
        public static void EnemyBumpTurnaround(Frame f, EntityRef entityA, EntityRef entityB) {
            EnemyBumpTurnaround(f, entityA, entityB, true);
        }

        public static void EnemyBumpTurnaroundOnlyFirst(Frame f, EntityRef entityA, EntityRef entityB) {
            EnemyBumpTurnaround(f, entityA, entityB, false);
        }

        public static void EnemyBumpTurnaround(Frame f, EntityRef entityA, EntityRef entityB, bool turnBoth) {
            var enemyA = f.Unsafe.GetPointer<Enemy>(entityA);
            var enemyB = f.Unsafe.GetPointer<Enemy>(entityB);
            var transformA = f.Unsafe.GetPointer<Transform2D>(entityA);
            var transformB = f.Unsafe.GetPointer<Transform2D>(entityB);

            QuantumUtils.UnwrapWorldLocations(f, transformA->Position, transformB->Position, out var ourPos, out var theirPos);
            bool right = ourPos.X > theirPos.X;
            if (ourPos.X == theirPos.X) {
                right = ourPos.Y < theirPos.Y;
            }
            enemyA->ChangeFacingRight(f, entityA, right);
            if (turnBoth) {
                enemyB->ChangeFacingRight(f, entityB, !right);
            }
        }

        public void HandleDelayedRespawn(Frame f, Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;
            if (QuantumUtils.Decrement(ref enemy->RespawnTimer)) {
                enemy->Respawn(f, filter.Entity, false);
                f.Events.EnemyAfterDelayedRespawn(filter.Entity);
            }

            if (enemy->RespawnTimer == enemy->RespawnSparklesTimer && enemy->RespawnSparklesTimer != 0) {
                f.Events.EnemyRespawnSparkles(filter.Entity);
            }
        }

        public void OffscreenCheck(Frame f, Filter filter, VersusStageData stage) {
            var enemy = filter.Enemy;
            var allPlayersFilter = f.Filter<MarioPlayer, Transform2D>();
            var transform = filter.Transform;
            var entity = filter.Entity;

            bool marioInSpawnpoint = false;

            // check if any Mario is near the enemy
            while (allPlayersFilter.NextUnsafe(out _, out _, out Transform2D* marioTransform)) {
                QuantumUtils.WrappedDistance(stage, transform->Position, marioTransform->Position, out FP distanceToMario);
                if (FPMath.Abs(distanceToMario) < Constants.EnemyHomeBoxWidth) {
                    return;
                }

                QuantumUtils.WrappedDistance(stage, enemy->Spawnpoint, marioTransform->Position, out FP marioDistanceToSpawnpoint);
                if (FPMath.Abs(marioDistanceToSpawnpoint) < Constants.EnemyHomeBoxBuffer) {
                    marioInSpawnpoint = true;
                }
            }

            if (marioInSpawnpoint) {
                // just teleport if the enemy didn't leave its home
                if (f.Unsafe.TryGetPointer(filter.Entity, out PhysicsObject* physicsObject)) {
                    if (physicsObject->IsFrozen) return;
                    physicsObject->Velocity = FPVector2.Zero;
                }

                // turn to face the player while in the shadows
                var shouldFaceRight = false;
                var closestMario = enemy->FindClosestPlayer(f, entity);

                // use closest player and face them
                if (f.Unsafe.TryGetPointer(closestMario, out Transform2D* closestMarioTransform)) {
                    QuantumUtils.WrappedDistance(f, enemy->Spawnpoint, closestMarioTransform->Position, out FP xDiff);
                    shouldFaceRight = xDiff < 0;
                }

                enemy->FacingRight = shouldFaceRight;

                transform->Teleport(f, enemy->Spawnpoint);
                f.Signals.OnEnemyReturnedHome(entity);
            } else {
                // "kill" the enemy if a Mario is in its spawnpoint
                enemy->IsActive = false;
                enemy->IsDead = true;
                enemy->SetDelayedRespawn();
            }
        }

        public void OnStageReset(Frame f, QBoolean full) {
            var filter = f.Filter<Enemy, Transform2D>();

            while (filter.NextUnsafe(out EntityRef entity, out Enemy* enemy, out Transform2D* transform)) {
                if (enemy->IsActive) {
                    // Check for respawning blocks killing us
                    if (!f.Unsafe.TryGetPointer(entity, out PhysicsObject* physicsObject)
                        || physicsObject->DisableCollision) {
                        continue;
                    }
                    if (!f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)) {
                        continue;
                    }

                    if (PhysicsObjectSystem.BoxInGround(f, transform->Position, collider->Shape, entity: entity)) {
                        f.Signals.OnEnemyKilledByStageReset(entity);
                    }
                } else {
                    // Check for respawns
                    if (enemy->DisableRespawning) {
                        continue;
                    }

                    if (!enemy->IgnorePlayerWhenRespawning) {
                        Physics2D.HitCollection playerHits = f.Physics2D.OverlapShape(enemy->Spawnpoint, 0, f.Context.CircleRadiusTwo, f.Context.PlayerOnlyMask);
                        if (playerHits.Count > 0) {
                            continue;
                        }
                    }

                    enemy->Respawn(f, entity, true);
                    f.Signals.OnEnemyRespawned(entity);
                }
            }
        }

        public void OnTryLiquidSplash(Frame f, EntityRef entity, EntityRef liquid, QBoolean exit, bool* doSplash) {
            if (f.Unsafe.TryGetPointer(entity, out Enemy* enemy)) {
                *doSplash &= enemy->IsActive;
            }
        }

        public void OnBeforeInteraction(Frame f, EntityRef entity, bool* allowInteraction) {
            if (f.Unsafe.TryGetPointer(entity, out Enemy* enemy)) {
                *allowInteraction &= enemy->IsAlive;
            }
        }

        public void OnEnemyDespawned(Frame f, EntityRef entity) {
            if (f.Has<Enemy>(entity) && f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)) {
                collider->Enabled = false;
            }
        }

        public void OnEnemyRespawned(Frame f, EntityRef entity) {
            if (f.Has<Enemy>(entity) && f.Unsafe.TryGetPointer(entity, out PhysicsCollider2D* collider)) {
                collider->Enabled = true;
            }
        }

        public void OnMarioPlayerMegaMushroomFootstep(Frame f) {
            var it = f.Unsafe.FilterStruct<Filter>();
            Filter filter = default;
            while (it.Next(&filter)) {
                if (!filter.Enemy->IsAlive
                    || !f.Unsafe.TryGetPointer(filter.Entity, out PhysicsObject* physicsObject)
                    || physicsObject->IsFrozen
                    || physicsObject->DisableCollision
                    || !physicsObject->IsTouchingGround) {
                    continue;
                }
                
                physicsObject->Velocity.Y = Constants._3_50;
                physicsObject->IsTouchingGround = false;
            }
        }
    }
}