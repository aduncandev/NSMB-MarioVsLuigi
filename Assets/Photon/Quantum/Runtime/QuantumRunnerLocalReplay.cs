namespace Quantum {
  using Photon.Deterministic;
  using UnityEngine;
  using UnityEngine.SceneManagement;

  /// <summary>
  /// An example of how to start a Quantum replay simulation from a replay file.
  /// Also works for loading a snapshot to start a local session from.
  /// </summary>
  public class QuantumRunnerLocalReplay : QuantumMonoBehaviour {
    /// <summary>
    /// Replay JSON file.
    /// </summary>
    [InlineHelp]
    public TextAsset ReplayFile;
    /// <summary>
    /// Quantum asset database Json file.
    /// </summary>
    [InlineHelp]
    public TextAsset DatabaseFile;
    /// <summary>
    /// Simulation speed multiplier to playback the replay in a different speed.
    /// </summary>
    [InlineHelp]
    public float SimulationSpeedMultiplier = 1.0f;
    /// <summary>
    /// Toggle the replay gui label on/off.
    /// </summary>
    [InlineHelp]
    public bool ShowReplayLabel;
    /// <summary>
    /// Set the <see cref="DeltaTimeType" /> to <see cref="SimulationUpdateTime.EngineDeltaTime" /> to not progress the
    /// simulation time during break points.
    /// </summary>
    [InlineHelp]
    public SimulationUpdateTime DeltaTimeType = SimulationUpdateTime.EngineDeltaTime;
    /// <summary>
    /// Set InstantReplaySettings to enable instant replays.
    /// </summary>
    [InlineHelp]
    public InstantReplaySettings InstantReplayConfig = InstantReplaySettings.Default;

    int _startFrame;
    int _endFrame;
    QuantumRunner _runner;

    /// <summary>
    /// Unity start event, will start the Quantum runner and simulation after deserializing the replay file.
    /// </summary>
    public void Start() {
      if (QuantumRunner.Default != null || SceneManager.sceneCount > 1) {
        // Prevents to start the simulation (again/twice) when..
        // a) there already is a runner, because the scene is reloaded during Quantum Unity map loading (AutoLoadSceneFromMap) or
        // b) this scene is not the first scene that is ever loaded (most likely a menu scene is involved that starts the simulation itself)
        enabled = false;
        return;
      }

      var gameMenu = FindFirstObjectByType<QuantumStartUI>();
      if (gameMenu != null && gameMenu.gameObject.activeSelf) {
        // If a game menu / start GUI is present, we assume that the game is started from the menu code.
        // In this case, we don't want to start the simulation here, but rather let the menu scene handle it.
        enabled = false;
        return;
      }

      if (ReplayFile == null) {
        Log.Error("QuantumRunnerLocalReplay - not replay file selected.");
        return;
      }

      var serializer = new QuantumUnityJsonSerializer();
      var replayFile = JsonUtility.FromJson<QuantumReplayFile>(ReplayFile.text);

      if (replayFile == null) {
        Log.Error("Failed to read replay file or file is empty.");
        return;
      }

      _startFrame = replayFile.InitialTick;
      _endFrame = replayFile.LastTick;

      Log.Info($"### Starting Quantum from a replay ###");

      var arguments = new SessionRunner.Arguments();
      arguments.InitForReplay(replayFile, serializer, assets: DatabaseFile != null ? DatabaseFile.bytes : null);
      arguments.InstantReplaySettings = InstantReplayConfig;
      arguments.DeltaTimeType = DeltaTimeType;

      _runner = (QuantumRunner)SessionRunner.Start(arguments);

      if (replayFile.Checksums?.Checksums != null) {
        _runner.Game.StartVerifyingChecksums(replayFile.Checksums);
      }
    }

    /// <summary>
    /// Unity Update event will update the simulation if a custom <see cref="SimulationSpeedMultiplier"/> was set.
    /// </summary>
    public void Update() {
      if (_runner?.Session != null) {
        // Set the session ticking to manual to inject custom delta time.
        _runner.IsSessionUpdateDisabled = SimulationSpeedMultiplier != 1.0f;
        if (_runner.IsSessionUpdateDisabled) {
          switch (_runner.DeltaTimeType) {
            case SimulationUpdateTime.Default:
            case SimulationUpdateTime.EngineUnscaledDeltaTime:
              _runner.Service(Time.unscaledDeltaTime * SimulationSpeedMultiplier);
              break;
            case SimulationUpdateTime.EngineDeltaTime:
              _runner.Service(Time.deltaTime * SimulationSpeedMultiplier);
              break;
          }
        }
      }
    }

#if UNITY_EDITOR

    void OnGUI() {
      if (ShowReplayLabel && _runner?.Session != null && _runner.Session.GameMode == DeterministicGameMode.Replay) {
        GUI.contentColor = Color.red;
        GUI.Label(new Rect(10, 30, 200, 100), "REPLAY RUNNING");
        GUI.HorizontalSlider(new Rect(10, 50, 150, 100), _runner.Session.FrameVerified.Number, _startFrame, _endFrame);
      }
    }

#endif
  }
}