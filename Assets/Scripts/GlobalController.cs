using NSMB.Addons;
using NSMB.Networking;
using NSMB.Quantum;
using NSMB.UI.Loading;
using NSMB.UI.Options;
using NSMB.UI.Translation;
using NSMB.Utilities.Extensions;
using Quantum;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using NSMB.UI.Game;
using NSMB.Sound;


#if UNITY_STANDALONE
using NSMB.UI.MainMenu.Submenus.Replays;
using UnityEngine.Profiling;
#endif

namespace NSMB {
    public class GlobalController : Singleton<GlobalController> {

        //---Events
        public static event Action ResolutionChanged;

        //---Public Variables
        public TranslationManager translationManager;
        public DiscordController discordController;
        public RumbleManager rumbleManager;
        public AnimatedFader fader;
        public AddonManager addonManager;
        public PauseOptionMenuManager optionsManager;
        public AudioMixerManager audioMixerManager;
        public SimulationConfig config;

        public ScriptableRendererFeature outlineFeature;
        public GameObject graphy, connecting;
        public LoadingCanvas loadingCanvas;
        public Image fullscreenFadeImage;
        public Sprite[] pingIndicators;
        public AudioSource sfx;

        public PlayerSlotInfo[] playerSlots;

        [NonSerialized] public bool checkedForVersion = false, firstConnection = true;
        [NonSerialized] public int windowWidth = 1280, windowHeight = 720;

        public AssetRef<CharacterAsset> defaultCharacter;

        //---Private Variables
        private Coroutine totalAudioFadeRoutine;
#if IDLE_LOCK_30FPS
        private int previousVsyncCount, previousFrameRate;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void CreateInstance() {
            Instantiate(Resources.Load("Static/GlobalController"));
        }

        public void OnValidate() {
            this.SetIfNull(ref discordController);
        }

        public void Awake() {
            Set(this);

            firstConnection = true;
            checkedForVersion = false;
        }

        public void Start() {
            AuthenticationHandler.IsAuthenticating = false;
            Settings.Controls.Enable();
            Settings.Controls.Debug.FPSMonitor.performed += ToggleFpsMonitor;
            QuantumEvent.Subscribe<EventStartGameEndFade>(this, OnStartGameEndFade);
            QuantumCallback.Subscribe<CallbackUnitySceneLoadDone>(this, OnUnitySceneLoadDone);
            loadingCanvas.Startup();
        }

        public void OnDestroy() {
            Settings.Controls.Debug.FPSMonitor.performed -= ToggleFpsMonitor;
            Settings.Controls.Disable();
        }

        public void Update() {
            int newWindowWidth = Screen.width;
            int newWindowHeight = Screen.height;

            //todo: this jitters to hell
#if UNITY_STANDALONE
            var keyboard = Keyboard.current;

            if (Screen.fullScreenMode == FullScreenMode.Windowed && keyboard.leftShiftKey.isPressed && (windowWidth != newWindowWidth || windowHeight != newWindowHeight)) {
                newWindowHeight = (int) (newWindowWidth * (9f / 16f));
                Screen.SetResolution(newWindowWidth, newWindowHeight, FullScreenMode.Windowed);
            }

            if (Debug.isDebugBuild) {
                if (keyboard.f9Key.wasPressedThisFrame) {
                    if (Profiler.enabled) {
                        Profiler.enabled = false;
                        PlaySound(SoundEffect.Player_Sound_Powerdown);
                    } else {
                        Profiler.maxUsedMemory = 256 * 1024 * 1024;
                        Profiler.logFile = "profile-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        Profiler.enableBinaryLog = true;
                        Profiler.enabled = true;
                        PlaySound(SoundEffect.Player_Sound_PowerupCollect);
                    }
                }

                /*
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha9)) {
                    var canvas = FindFirstObjectByType<MainMenuCanvas>();
                    if (canvas) {
                        var blur = canvas.transform.Find("MainMenu").Find("Blur").gameObject;
                        blur.SetActive(!blur.activeSelf);
                    }
                }
                */
            }

            if (keyboard.f6Key.wasPressedThisFrame) {
                System.Diagnostics.Process.Start(Application.consoleLogPath);
            }

            if (keyboard.f7Key.wasPressedThisFrame) {
                System.Diagnostics.Process.Start(ReplayListManager.ReplayDirectory);
            }

            if (keyboard.f8Key.wasPressedThisFrame) {
                System.Diagnostics.Process.Start(AddonManager.LocalFolderPath);
            }
#endif

            if (windowWidth != newWindowWidth || windowHeight != newWindowHeight) {
                windowWidth = newWindowWidth;
                windowHeight = newWindowHeight;
                ResolutionChanged?.Invoke();
            }

            if ((int) (Time.unscaledTime + Time.unscaledDeltaTime) > (int) Time.unscaledTime) {
                // Update discord every second
                discordController.UpdateActivity();
            }
        }

#if IDLE_LOCK_30FPS
        public void OnApplicationFocus(bool focus) {
            if (focus) {
                QualitySettings.vSyncCount = previousVsyncCount;
                Application.targetFrameRate = previousFrameRate;
            } else {
                // Lock framerate when losing focus to (hopefully) disable browsers slowing the game
                previousVsyncCount = QualitySettings.vSyncCount;
                previousFrameRate = Application.targetFrameRate;

                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 30;
            }
        }
#endif

        public void OnUnitySceneLoadDone(CallbackUnitySceneLoadDone e) {
            if (e.SceneName != null) {
                foreach (int slot in e.Game.GetLocalPlayerSlots()) {
                    e.Game.AddCommand(slot, new CommandPlayerLoaded());
                }
            }

            discordController.UpdateActivity();
            this.StopCoroutineNullable(ref totalAudioFadeRoutine);
            audioMixerManager.SetFloat(AudioMixerManager.KeyOverride, 0f);
            StartCoroutine(FadeFullscreenImage(0, 1/3f, 0.1f));
        }

        public void PlaySound(SoundEffect soundEffect) {
            sfx.PlayOneShot(soundEffect);
        }

        private IEnumerator FadeFullscreenImage(float target, float fadeDuration, float delay = 0) {
            float original = fullscreenFadeImage.color.a;
            float timer = fadeDuration;
            if (delay > 0) {
                yield return new WaitForSeconds(delay);
            }

            Color color = fullscreenFadeImage.color;
            while (timer > 0) {
                timer -= Time.deltaTime;
                color.a = Mathf.Lerp(original, target, 1 - (timer / fadeDuration));
                fullscreenFadeImage.color = color;
                yield return null;
            }
        }

        private void OnStartGameEndFade(EventStartGameEndFade e) {
            if (MvLSceneLoader.Instance.CurrentLoadedMap != null) {
                // In a game scene
                StartCoroutine(FadeFullscreenImage(1, 1/3f));
                totalAudioFadeRoutine = StartCoroutine(audioMixerManager.FadeOut(AudioMixerManager.KeyOverride));
            }
        }

        private void ToggleFpsMonitor(InputAction.CallbackContext obj) {
            graphy.SetActive(!graphy.activeSelf);
        }
    }
}
