using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;

public class PowerupAsset : CoinItemAsset, ISoundOverrideProvider {

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
    public FP BumpedFromBelowVelocity = Constants._5_50;

    public bool FollowAnimationCurve;
    public FPAnimationCurve AnimationCurveX;
    public FPAnimationCurve AnimationCurveY;

    public sbyte StatePriority = -1, ItemPriority = -1;
    public bool EnterReserveIfOverridden = true;

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
    public override unsafe int CountItemsExisting(Frame f) {
        int numOfItems = 0;
        foreach ((var _, var foundItem) in f.Unsafe.GetComponentBlockIterator<CoinItem>()) {
            if (f.FindAsset(foundItem->Scriptable).Equals(this)) {
                numOfItems++;
            }
        }
        return numOfItems;
    }
    public override unsafe int CountPlayersWithState(Frame f) {
        int playersWithPower = 0;
        foreach ((var _, var otherPlayers) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
            // check if another player matches the powerUP state
            if (otherPlayers->CurrentPowerupState == State) {
                playersWithPower++;
            }
        }
        return playersWithPower;
    }

    public SoundEffectOverride GetOverride(SoundEffect sfx) {
        overridesDict.TryGetValue(sfx, out var result);
        return result;
    }

    public virtual unsafe PowerupReserveResult Collect(Frame f, EntityRef marioEntity) {
        var mario = f.Unsafe.GetPointer<MarioPlayer>(marioEntity);

        // Reserve if it's the same item
        if (mario->CurrentPowerupState == State) {
            mario->SetReserveItem(f, this);
            return PowerupReserveResult.KeepOldReserveNew;
        }

        var previousPowerup = QuantumUtils.FindPowerupAsset(f, mario->CurrentPowerupState);
        sbyte currentPowerupStatePriority = previousPowerup != null ? previousPowerup.StatePriority : (sbyte) -1;

        // Reserve if we have a higher priority item
        if (currentPowerupStatePriority > ItemPriority) {
            mario->SetReserveItem(f, this);
            return PowerupReserveResult.KeepOldReserveNew;
        }

        OnCollected(f, marioEntity);

        mario->PreviousPowerupState = mario->CurrentPowerupState;
        mario->CurrentPowerupState = State;
        mario->IsPropellerFlying = false;
        mario->UsedPropellerThisJump = false;
        mario->IsDrilling &= mario->IsSpinnerFlying;
        mario->PropellerLaunchFrames = 0;
        mario->IsInShell = false;

        if (previousPowerup != null && previousPowerup.EnterReserveIfOverridden) {
            if (mario->CurrentPowerupState != PowerupState.NoPowerup) {
                mario->SetReserveItem(f, previousPowerup);
            }
            return PowerupReserveResult.CollectNewReserveOld;
        } else {
            return PowerupReserveResult.CollectNewIgnoreOld;
        }
    }

    protected virtual void OnCollected(Frame f, EntityRef entity) { }
}