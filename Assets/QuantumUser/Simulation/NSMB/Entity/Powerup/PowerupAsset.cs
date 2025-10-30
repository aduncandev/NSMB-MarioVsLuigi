using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;

public class PowerupAsset : CoinItemAsset, ISoundEffectOverrideProvider {
    public PowerupType Type;
    public PowerupState State;

    public bool SoundPlaysEverywhere;
    public SoundEffect SoundEffect = SoundEffect.Player_Sound_PowerupCollect;

#if QUANTUM_UNITY
    public UnityEngine.Sprite ReserveSprite;
#endif

    public bool AvoidPlayers;
    public FP Speed;
    public FP BounceStrength;
    public FP TerminalVelocity;

    public bool FollowAnimationCurve;
    public FPAnimationCurve AnimationCurveX;
    public FPAnimationCurve AnimationCurveY;

    public sbyte StatePriority = -1, ItemPriority = -1;

    public SoundEffectOverride[] SfxOverrides;

    [NonSerialized] private Dictionary<SoundEffect, SoundEffectOverride> overridesDict;
    public override void Loaded(IResourceManager resourceManager) {
        base.Loaded(resourceManager);

        overridesDict = new();
        if (SfxOverrides != null) {
            foreach (var @override in SfxOverrides) {
                overridesDict[@override.SoundEffect] = @override;
            }
        }
    }

    public SoundEffectOverride GetOverrideForSfx(SoundEffect sfx) {
        overridesDict.TryGetValue(sfx, out var result);
        return result;
    }
}

public enum PowerupType {
    Basic,
    Starman,
    ExtraLife,
}