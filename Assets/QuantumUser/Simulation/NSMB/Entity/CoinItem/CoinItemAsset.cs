using System;
using Photon.Deterministic;
using Quantum;

public unsafe class CoinItemAsset : AssetObject {

    public AssetRef<EntityPrototype> Prefab;
    public FP SpawnChance = FP._0_10, AboveAverageBonus = 0, BelowAverageBonus = 0;
    public SoundEffect BlockSpawnSoundEffect = SoundEffect.World_Block_Powerup;
    public TypeFlags Flags = TypeFlags.SpawnableFromCoins | TypeFlags.SpawnableFromRoulette;
    public int MaxNumberOfItems = 0;

    public FPVector2 CameraSpawnOffset = new(0, FP.FromString("1.68"));

    public virtual int CountItemsExisting(Frame f) {
        int numOfItems = 0;
        foreach ((_, var coinItem) in f.Unsafe.GetComponentBlockIterator<CoinItem>()) {
            if (coinItem->Scriptable == this) {
                numOfItems++;
            }
        }
        return numOfItems;
    }

    public virtual unsafe bool CanSpawn(Frame f, bool fromRouletteBlock) {
        if (fromRouletteBlock && !Flags.HasFlag(TypeFlags.SpawnableFromCoins)) {
            return false;
        }
        if (fromRouletteBlock && !Flags.HasFlag(TypeFlags.SpawnableFromRoulette)) {
            return false;
        }
        if (Flags.HasFlag(TypeFlags.NonVanillaItem) && !f.Global->Rules.CustomPowerupsEnabled) {
            return false;
        }
        if (Flags.HasFlag(TypeFlags.LivesEnabledOnly) && !f.Global->Rules.IsLivesEnabled) {
            return false;
        }
        if (MaxNumberOfItems > 0) {
            return CountItemsExisting(f) < MaxNumberOfItems;
        }
        return true;
    }

    [Flags]
    public enum TypeFlags : byte {
        None = 0,
        SpawnableFromCoins = 1 << 0,
        SpawnableFromRoulette = 1 << 1,
        NonVanillaItem = 1 << 2,
        LivesEnabledOnly = 1 << 3,
    }
}