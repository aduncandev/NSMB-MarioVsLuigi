using Quantum;
using System;
using System.Collections.Generic;
using UnityEngine;

public class SoundEffectOverrideList : AssetObject {

    public SoundEffectOverride[] Overrides;

#if QUANTUM_UNITY

    [NonSerialized] public Dictionary<SoundEffect, AudioClip[]> OverridesDict;

    public override void Loaded(IResourceManager resourceManager) {
        base.Loaded(resourceManager);

        OverridesDict = new();
        foreach (var soundOverride in Overrides) {
            OverridesDict[soundOverride.SoundEffect] = soundOverride.AudioClips;
        }
    }
#endif

    [Serializable]
    public class SoundEffectOverride {
        public SoundEffect SoundEffect;
#if QUANTUM_UNITY
        public AudioClip[] AudioClips;
#endif
    }
}