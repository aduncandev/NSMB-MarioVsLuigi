using Photon.Deterministic;
using Quantum;

public class MegaMushroomPowerupAsset : PowerupAsset {

    public FP GrowAnimationDuration = FP._1_50;
    public override unsafe int CountPlayersWithState(Frame f) {
        int playersWithPower = 0;
        foreach ((var _, var otherPlayer) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
            // check if another player matches the powerUP state
            if (otherPlayer->CurrentPowerupState == PowerupState.MegaMushroom && otherPlayer->MegaMushroomStartFrames == 0) {
                playersWithPower++;
            }
        }
        return playersWithPower;
    }

    protected override unsafe void OnCollected(Frame f, EntityRef marioEntity) {
        var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);
        var marioPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(marioEntity);

        mario->MegaMushroomStartFrames = (byte) (GrowAnimationDuration * f.UpdateRate);
        mario->IsSliding = false;
        mario->CurrentKnockback = KnockbackStrength.None;
        mario->KnockbackGetupFrames = 0;
        if (f.Unsafe.TryGetPointer(mario->HeldEntity, out Holdable* holdable)) {
            holdable->DropWithoutThrowing(f, mario->HeldEntity);
        }
        if (marioPhysicsObject->IsTouchingGround) {
            mario->JumpState = JumpState.None;
        }
        marioPhysicsObject->IsFrozen = true;
        marioPhysicsObject->Velocity = FPVector2.Zero;
    }
}