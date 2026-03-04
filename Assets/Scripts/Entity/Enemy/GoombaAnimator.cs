using NSMB.Sound;
using NSMB.Utilities;
using NSMB.Utilities.Components;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Entities.Enemies {
    public unsafe class GoombaAnimator : QuantumEntityViewComponent {

        //---Serialized Variables
        [SerializeField] private SpriteRenderer sRenderer;
        [SerializeField] private Sprite deadSprite;
        [SerializeField] private GameObject specialKillParticle;
        [SerializeField] private GameObject respawnParticle;
        [SerializeField] private LegacyAnimateSpriteRenderer legacyAnimation;
        [SerializeField] private SoundEffectPlayer sfx;

        private GameObject activeRespawnParticle;

        public void OnValidate() {
            this.SetIfNull(ref sRenderer, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref legacyAnimation, UnityExtensions.GetComponentType.Children);
            this.SetIfNull(ref sfx);
        }

        public void Start() {
            QuantumEvent.Subscribe<EventEnemyKilled>(this, OnEnemyKilled, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventPlayComboSound>(this, OnPlayComboSound, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventEnemyRespawnSparkles>(this, OnEnemyRespawnSparkles, FilterOutReplayFastForward);
        }

        public override unsafe void OnUpdateView() {
            Frame f = PredictedFrame;

            if (!f.Exists(EntityRef)) {
                return;
            }

            if (f.Global->GameState >= GameState.Ended) {
                legacyAnimation.enabled = false;
                return;
            }

            var enemy = f.Unsafe.GetPointer<Enemy>(EntityRef);
            var goomba = f.Unsafe.GetPointer<Goomba>(EntityRef);
            var freezable = f.Unsafe.GetPointer<Freezable>(EntityRef);

            sRenderer.enabled = enemy->IsActive;
            legacyAnimation.enabled = enemy->IsAlive && !freezable->IsFrozen(f);
            sRenderer.flipX = enemy->FacingRight;

            if (enemy->IsDead) {
                if (goomba->DeathAnimationFrames > 0) {
                    // Stomped
                    sRenderer.sprite = deadSprite;
                } else {
                    // Special killed
                    transform.rotation *= Quaternion.Euler(0, 0, 400f * (enemy->FacingRight ? -1 : 1) * Time.deltaTime);
                }
            } else {
                transform.rotation = Quaternion.identity;
            }
        }

        private void OnPlayComboSound(EventPlayComboSound e) {
            if (e.Entity != EntityRef) {
                return;
            }

            sfx.PlayOneShot(QuantumViewUtils.GetComboSoundEffect(e.Combo));
        }

        private void OnEnemyKilled(EventEnemyKilled e) {
            if (e.Enemy != EntityRef) {
                return;
            }

            if (e.KillReason == EnemyKillReason.Groundpounded) {
                Instantiate(specialKillParticle, transform.position + Vector3.up * 0.2f, Quaternion.identity);
            }
        }
        
        private void OnEnemyRespawnSparkles(EventEnemyRespawnSparkles e) {
            if (e.Entity != EntityRef) {
                return;
            }
            Frame f = PredictedFrame;

            var enemy = f.Unsafe.GetPointer<Enemy>(EntityRef);
            activeRespawnParticle = Instantiate(respawnParticle, enemy->Spawnpoint.ToUnityVector3() + (Vector3.up * 0.25f), Quaternion.identity);
            foreach (ParticleSystem particle in activeRespawnParticle.GetComponentsInChildren<ParticleSystem>()) {
                var main = particle.main;
                main.startColor = Color.saddleBrown;
            }

            sfx.PlayOneShot(SoundEffect.Player_Sound_Respawn);
        }
    }
}
