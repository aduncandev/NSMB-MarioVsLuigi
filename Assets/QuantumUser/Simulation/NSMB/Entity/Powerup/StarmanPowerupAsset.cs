using Photon.Deterministic;
using Quantum;

public class StarmanPowerupAsset : PowerupAsset {

    public FP StarmanDuration = 10;

    public override bool SpecialSpawnConditions(Frame f) {
        return true; // no restrictions
    }

    public override unsafe PowerupReserveResult Collect(Frame f, EntityRef marioEntity) {
        var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
        mario->InvincibilityFrames = (ushort) (StarmanDuration * f.UpdateRate);

        f.Signals.OnMarioPlayerBecameInvincible(marioEntity);
        return PowerupReserveResult.CollectNewIgnoreOld;
    }
}