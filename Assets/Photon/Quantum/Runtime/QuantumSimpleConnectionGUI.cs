namespace Quantum.Demo {
  using Photon.Realtime;
  using System;
  using UnityEngine;

  /// <summary>
  /// A Unity script that demonstrates how to connect to a Quantum cloud and start a Quantum game session.
  /// </summary>
  public class QuantumSimpleConnectionGUI : QuantumMonoBehaviour {
    /// <summary>
    /// The RuntimeConfig to use for the Quantum game session. The RuntimeConfig describes custom game properties.
    /// </summary>
    public RuntimeConfig RuntimeConfig;
    /// <summary>
    /// The RuntimePlayers to add to the Quantum game session. The RuntimePlayers describe individual custom player properties.
    /// </summary>
    public RuntimePlayer RuntimePlayer;
    /// <summary>
    /// The Photon RealtimeClient object that represents the connection to the Quantum cloud.
    /// </summary>
    public RealtimeClient Client => _client;
    /// <summary>
    /// Access the session runner that runs the Quantum game session.
    /// </summary>
    public QuantumRunner Runner => _runner;

    RealtimeClient _client;
    QuantumRunner _runner;
    GUIStyle _style;
    State _state = State.Disconnected;

    enum State {
      Disconnected,
      Connecting,
      Starting,
      Running,
      ShuttingDown,
      Error
    }

    /// <summary>
    /// 1) await MatchmakingExtensions.ConnectToRoomAsync
    /// 2) await SessionRunner.StartAsync
    /// 3) QuantumGame.AddPlayer
    /// </summary>
    async void Connect() {
      // Enabled runInBackground to improve the online stability in background mode.
      // Works on all platforms, but mobile and WebGL games should consider what behavior is best.
      Application.runInBackground = true;

      try {
        _state = State.Connecting;

        var connectionArguments = new MatchmakingArguments {
          PhotonSettings = new AppSettings(PhotonServerSettings.Global.AppSettings),
          PluginName = "QuantumPlugin",
          MaxPlayers = Quantum.Input.MAX_COUNT,
          UserId = Guid.NewGuid().ToString()
        };

        // Connect and wait until joined a Photon room
        _client = await MatchmakingExtensions.ConnectToRoomAsync(connectionArguments);

        _state = State.Starting;

        // Optionally create a scope that updates the Photon connection while starting the game
        // which is helpful when the start procedure takes longer when loading levels for example.
        using var connectionScope = new ConnectionServiceScope(_client);

        // Create online game arguments and start the session
        var sessionRunnerArguments = new SessionRunner.Arguments();
        sessionRunnerArguments.InitForMultiplayer(RuntimeConfig, Client, Client.UserId);      
        _runner = (QuantumRunner)await SessionRunner.StartAsync(sessionRunnerArguments);

        // Add a player to the game
        _runner.Game.AddPlayer(RuntimePlayer);

        _state = State.Running;
      } catch (Exception e) {
        Debug.LogError($"Error connecting to Photon cloud or starting Quantum online simulation: {e.Message}");
        _state = State.Error;
      }
    }

    /// <summary>
    /// await SessionRunner.ShutdownAsync
    /// await RealtimeClient.DisconnectAsync
    /// </summary>
    async void Disconnect() {
      try {
        _state = State.ShuttingDown;

        if (_runner) await _runner.ShutdownAsync();
        _runner = null;

        await _client?.DisconnectAsync();
        _client = null;

        _state = State.Disconnected;
      } catch (Exception e) {
        Debug.LogError($"Error disconnecting from Photon cloud or shutting down Quantum online simulation: {e.Message}");
        _state = State.Error;
      }
    }

    void OnGUI() {
      _style ??= new GUIStyle(GUI.skin.button) { fontSize = 16 };
      var rect = new Rect(0, 0, 200, 60) { center = new Vector2(Screen.width / 2, 60) };

      switch (_state) {
        case State.Disconnected:
          if (GUI.Button(rect, "Connect", _style)) {
            Connect();
          }
          break;
        case State.Running:
          if (GUI.Button(rect, "Shutdown", _style)) {
            Disconnect();
          }
          break;
        default:
          var enabled = GUI.enabled;
          GUI.enabled = false;
          if (GUI.Button(rect, _state.ToString(), _style)) { }
          GUI.enabled = enabled;
          break;
      }
    }
  }
}
