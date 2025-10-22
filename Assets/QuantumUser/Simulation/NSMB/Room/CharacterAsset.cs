using Quantum;
using UnityEngine;

public class CharacterAsset : AssetObject {

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

    public AssetRef<SoundEffectOverrideList> SoundEffectOverrides;
#endif
}