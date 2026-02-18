using System;
using Photon.Deterministic;
using Quantum;

public class CoinItemAsset : AssetObject {
    [Flags]
    public enum TypeFlags {
        None = 0,
        BigPower = 1 << 0,
        VerticalPower = 1 << 1,
        Custom = 1 << 2,
        LivesOnly = 1 << 3,
        BlockOnly = 1 << 4,
        RouletteOnly = 1 << 5,
        NotPowerUP = 1 << 6,
        NoStateChange = 1 << 7,
    }
    public AssetRef<EntityPrototype> Prefab;
    public FP SpawnChance = FP._0_10, AboveAverageBonus = 0, BelowAverageBonus = 0;
    public SoundEffect BlockSpawnSoundEffect = SoundEffect.World_Block_Powerup;
    public TypeFlags Flags = TypeFlags.None;
    public int MaxNumberOfItems = 0;
    public int MaxMatchingPowerStates = 0; // no more items with a matching powerUP state will spawn if above 0

    public FPVector2 CameraSpawnOffset = new(0, FP.FromString("1.68"));
    
    public virtual unsafe int CountItemsExisting(Frame f) {
        return 0;
    }

    public virtual unsafe int CountPlayersWithState(Frame f) {
        return 0;
    }
}