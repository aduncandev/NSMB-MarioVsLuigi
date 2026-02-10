using Quantum;
using System;
using System.Collections.Generic;
using UnityEngine;

public class CharacterAsset : AssetObject, ISoundOverrideProvider {

    public AssetRef<EntityPrototype> Prototype;

    public string UiString;
    public string TranslationString;

#if QUANTUM_UNITY
    public Sprite LoadingSmallSprite;
    public Sprite LoadingLargeSprite;
    public Sprite ReadySprite;

    public Sprite SelectionSprite;
    public Color SelectionColor = Color.white;
    public int SelectionOrder;

    public RuntimeAnimatorController SmallOverrides;
    public RuntimeAnimatorController LargeOverrides;
#endif

    public SoundEffectOverride[] SfxOverrides;

    [NonSerialized] private Dictionary<SoundEffect, SoundEffectOverride> overridesDict;
    public override void Loaded(IResourceManager resourceManager) {
        overridesDict = new();
        if (SfxOverrides != null) {
            foreach (var @override in SfxOverrides) {
                overridesDict[@override.SoundEffect] = @override;
            }
        }
    }

    public SoundEffectOverride GetOverride(SoundEffect sfx) {
        overridesDict.TryGetValue(sfx, out var result);
        return result;
    }
}