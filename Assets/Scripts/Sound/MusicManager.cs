using NSMB.Quantum;
using NSMB.Replay;
using NSMB.UI.Game;
using NSMB.UI.Loading;
using NSMB.Utilities;
using NSMB.Utilities.Extensions;
using Quantum;
using UnityEngine;
using UnityEngine.Audio;
using static NSMB.Utilities.QuantumViewUtils;

namespace NSMB.Sound {
    public unsafe class MusicManager : QuantumSceneViewComponent<StageContext> {

        //---Serialized Variables
        [SerializeField] private LoopingMusicPlayer musicPlayer;
        [SerializeField] private AudioMixer mixer; 

        public void OnValidate() {
            this.SetIfNull(ref musicPlayer);
        }

        public unsafe void Start() {
            QuantumCallback.Subscribe<CallbackUpdateView>(this, OnUpdateView);
            QuantumCallback.Subscribe<CallbackGameResynced>(this, OnGameResynced);
            QuantumEvent.Subscribe<EventMarioPlayerDied>(this, OnMarioPlayerDied, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventMarioPlayerRespawned>(this, OnMarioPlayerRespawned, FilterOutReplayFastForward);
            QuantumEvent.Subscribe<EventGameEnded>(this, OnGameEnded);
            QuantumEvent.Subscribe<EventGameStateChanged>(this, OnGameStateChanged);

            ActiveReplayManager.OnReplayFastForwardEnded += OnReplayFastForwardEnded;
            LoadingCanvas.OnLoadingEnded += OnLoadingEnded;
            AudioMixerManager.OnAudioMixerValueChanged += OnAudioMixerValueChanged;

            var game = QuantumRunner.DefaultGame;
            Frame f;
            if (game != null && (f = game.Frames.Predicted) != null) {
                GameState state = f.Global->GameState;
                if (state == GameState.Starting || state == GameState.Playing) {
                    // Already in a game
                    HandleMusic(game, true);
                }
            }
        }

        public void OnDestroy() {
            ActiveReplayManager.OnReplayFastForwardEnded -= OnReplayFastForwardEnded;
            LoadingCanvas.OnLoadingEnded -= OnLoadingEnded;
            AudioMixerManager.OnAudioMixerValueChanged -= OnAudioMixerValueChanged;
        }

        public void OnUpdateView(CallbackUpdateView e) {
            if (e.Game.Frames.Predicted.Global->GameState == GameState.Playing) {
                HandleMusic(e.Game, false);
            }
        }

        public void HandleMusic(QuantumGame game, bool force) {
            Frame f = game.Frames.Predicted;
            ref var rules = ref f.Global->Rules;

            if (!force && !musicPlayer.IsPlaying) {
                return;
            }

            bool invincible = false;
            bool mega = false;
            bool speedup = false;

            if (f.TryFindAsset(f.Global->Rules.Gamemode, out var gamemode)) {
                speedup = gamemode.IsFastMusicEnabled(f);
            }

            foreach ((var entity, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                if (mario->IsValid(f) && IsMarioLocal(entity)) {
                    speedup |= rules.IsLivesEnabled && mario->Lives == 1;
                    mega |= Settings.Instance.audioSpecialPowerupMusic.HasFlag(Enums.SpecialPowerupMusic.MegaMushroom) && mario->MegaMushroomFrames > 0;
                    invincible |= Settings.Instance.audioSpecialPowerupMusic.HasFlag(Enums.SpecialPowerupMusic.Starman) && mario->IsStarmanInvincible;
                }
            }

            VersusStageData stage = ViewContext.Stage;
            if (mega) {
                musicPlayer.Play(f.FindAsset(stage.MegaMushroomMusic));
            } else if (invincible) {
                musicPlayer.Play(f.FindAsset(stage.InvincibleMusic));
            } else {
                musicPlayer.Play(f.FindAsset(stage.GetCurrentMusic(f)));
            }

            musicPlayer.FastMusic = speedup;
        }

        private void OnGameEnded(EventGameEnded e) {
            musicPlayer.Stop();
        }

        private void OnMarioPlayerRespawned(EventMarioPlayerRespawned e) {
            if (IsMarioLocal(e.Entity) && !musicPlayer.IsPlaying) {
                HandleMusic(e.Game, true);
            }
        }

        private void OnMarioPlayerDied(EventMarioPlayerDied e) {
            if (IsMarioLocal(e.Entity) && Settings.Instance.audioRestartMusicOnDeath) {
                musicPlayer.Stop();
            }
        }

        private void OnGameResynced(CallbackGameResynced e) {
            if (e.Game.Frames.Predicted.Global->GameState == GameState.Playing) {
                HandleMusic(e.Game, true);
            }
        }

        private void OnReplayFastForwardEnded(ActiveReplayManager arm) {
            if (Game.Frames.Predicted.Global->GameState == GameState.Playing) {
                HandleMusic(Game, true);
            }
        }

        private void OnLoadingEnded(bool longIntro) {
            if (!longIntro && Game != null) {
                HandleMusic(Game, true);
            }
        }

        private void OnAudioMixerValueChanged(string key, float value) {
            mixer.SetFloat(key, value);
        }

        private void OnGameStateChanged(EventGameStateChanged e) {
            if (Game.Frames.Predicted.Global->GameState == GameState.Playing) {
                HandleMusic(Game, true);
            }
        }
    }
}