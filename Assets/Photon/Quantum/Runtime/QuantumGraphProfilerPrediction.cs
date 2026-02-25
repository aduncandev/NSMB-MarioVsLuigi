namespace Quantum.Profiling {
#if !QUANTUM_DISABLE_GRAPHPROFILER
  /// <summary>
  /// A Quantum graph profiler that shows how far the simulation is in predicting.
  /// </summary>
  public sealed class QuantumGraphProfilerPrediction : QuantumGraphProfilerValueSeries {
    /// <inheritdoc/>
    protected override void OnUpdate() {
      int predictedFrames = 0;

      if (QuantumRunner.Default?.Game?.Frames?.Predicted!= null) {
        predictedFrames =
          QuantumRunner.Default.Game.Frames.Predicted.Number -
          QuantumRunner.Default.Game.Frames.Verified.Number;
      }

      AddValue(predictedFrames);
    }
  }
#endif
}
