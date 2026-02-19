using System;
using Photon.Deterministic;
using Quantum;

public class CoinItemAsset : AssetObject {
    [Flags]
    public enum TypeFlags {
        None = 0,
        Big = 1 << 0,
        Vertical = 1 << 1,
        Custom = 1 << 2,
        LivesOnly = 1 << 3,
        Block = 1 << 4,
        Roulette = 1 << 5,
        NotPowerUP = 1 << 6,
        NoStateChange = 1 << 7,
        Disadvantage = 1 << 8,
    }
    public AssetRef<EntityPrototype> Prefab;
    public FP SpawnChance = FP._0_10, AboveAverageBonus = 0, BelowAverageBonus = 0;
    public SoundEffect BlockSpawnSoundEffect = SoundEffect.World_Block_Powerup;
    public TypeFlags Flags = TypeFlags.None;
    public int MaxNumberOfItems = 0;

    public FPVector2 CameraSpawnOffset = new(0, FP.FromString("1.68"));

    public virtual unsafe bool SpecialSpawnConditions(Frame f) {
        if (MaxNumberOfItems > 0) {
            int numOfItems = 0;
            foreach ((var _, var foundItem) in f.Unsafe.GetComponentBlockIterator<CoinItem>()) {
                if (foundItem->Scriptable == this) {
                    numOfItems++;
                }
            }

            return numOfItems < MaxNumberOfItems;
        }
        return true;
    }
}