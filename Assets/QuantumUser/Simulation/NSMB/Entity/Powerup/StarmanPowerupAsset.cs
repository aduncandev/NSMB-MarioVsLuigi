using Photon.Deterministic;
using Quantum;

public class StarmanPowerupAsset : PowerupAsset {

    public FP StarmanDuration = 10;

    public override unsafe int CountPlayersWithState(Frame f) {
        int playersWithPower = 0;
        foreach ((var _, var otherPlayers) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
            // check if another player matches the powerUP state
            if (otherPlayers->InvincibilityFrames > 0) {
                playersWithPower++;
            }
        }
        return playersWithPower;
    }


    public override unsafe PowerupReserveResult Collect(Frame f, EntityRef marioEntity) {
        var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
        mario->InvincibilityFrames = (ushort) (StarmanDuration * f.UpdateRate);

        f.Signals.OnMarioPlayerBecameInvincible(marioEntity);
        return PowerupReserveResult.CollectNewIgnoreOld;
    }
}