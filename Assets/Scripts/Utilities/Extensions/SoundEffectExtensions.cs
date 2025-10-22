using NSMB;
using Quantum;
using UnityEngine;

public static partial class SoundEffectExtensions {
    public static AudioClip GetClip(this SoundEffect soundEffect, SoundEffectOverrideList overridesList = null, int? variant = null) {
        if (overridesList == null || !overridesList.OverridesDict.TryGetValue(soundEffect, out AudioClip[] clips)) {
            // Fallback to default sound effects.
            QuantumUnityDB.GetGlobalAsset(GlobalController.Instance.soundEffects).OverridesDict.TryGetValue(soundEffect, out clips);
        }

        if (clips != null && clips.Length > 0) {
            variant ??= Random.Range(0, clips.Length);
            return clips[QuantumUtils.Modulo(variant.Value, clips.Length)];
        } else {
            return null;
        }
    }
}