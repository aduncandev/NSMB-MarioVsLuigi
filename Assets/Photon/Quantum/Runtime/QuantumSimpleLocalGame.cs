namespace Quantum.Demo {
  using System;
  using UnityEngine;

  /// <summary>
  /// A Unity script that demonstrates how to connect to a Quantum cloud and start a Quantum game session.
  /// </summary>
  public class QuantumSimpleLocalGame : QuantumMonoBehaviour {
    /// <summary>
    /// The RuntimeConfig to use for the Quantum game session. The RuntimeConfig describes custom game properties.
    /// </summary>
    public RuntimeConfig RuntimeConfig;
    /// <summary>
    /// The RuntimePlayers to add to the Quantum game session. The RuntimePlayers describe individual custom player properties.
    /// </summary>
    public RuntimePlayer RuntimePlayer;
    /// <summary>
    /// Access the session runner that runs the Quantum game session.
    /// </summary>
    public QuantumRunner Runner { get; private set; }

    async void Start() {
      try {
        // Start a local simulation and wait until it's started
        var arguments = new SessionRunner.Arguments();
        arguments.InitForLocal(RuntimeConfig);
        Runner = (QuantumRunner)await SessionRunner.StartAsync(arguments);

        // Add a player to the game
        Runner.Game.AddPlayer(RuntimePlayer);
      } catch (Exception e) {
        Debug.LogError($"Error starting local Quantum simulation: {e.Message}");
      }
    }
  }
}
