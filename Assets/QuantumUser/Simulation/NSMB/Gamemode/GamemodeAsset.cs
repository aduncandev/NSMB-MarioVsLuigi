using Photon.Deterministic;
using Quantum.Prototypes;
using System;
using System.Linq;

namespace Quantum {
    public abstract unsafe class GamemodeAsset : AssetObject {

        public string NamePrefix, TranslationKey, DescriptionTranslationKey, DiscordRpcKey;
        public string ObjectiveSymbolPrefix;
        public AssetRef<CoinItemAsset>[] AllCoinItems;
        public AssetRef<CoinItemAsset> FallbackCoinItem;
        public AssetRef<EntityPrototype> LooseCoinPrototype;

        public GameRulesPrototype DefaultRules;

        public abstract void EnableGamemode(Frame f);

        public abstract void DisableGamemode(Frame f);

        public abstract void CheckForGameEnd(Frame f);

        public abstract int GetObjectiveCount(Frame f, PlayerRef player);

        public abstract int GetObjectiveCount(Frame f, MarioPlayer* mario);

        public bool CanItemSpawn(Frame f, CoinItemAsset coinItem, bool fromBlock) {
            var flag = coinItem.Flags;
            var rules = f.Global->Rules;
            if (flag.HasFlag(CoinItemAsset.TypeFlags.Custom) &! rules.CustomPowerupsEnabled) { return false; }
            if (flag.HasFlag(CoinItemAsset.TypeFlags.Roulette) && fromBlock) { return false; }
            if (flag.HasFlag(CoinItemAsset.TypeFlags.Block) &! fromBlock) { return false; }
            if (!coinItem.SpecialSpawnConditions(f)) { return false; }
            if (coinItem is PowerupAsset powerUP) {
                var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);
                PowerupAsset[] bannedPowerUPs = stage.BannedPowerUPs;
                if (bannedPowerUPs.Any(p => p == powerUP)) {
                    return false;
                }
            }

            return true;
        }

        public virtual CoinItemAsset GetRandomItem(Frame f, MarioPlayer* mario, bool fromBlock) {
            var stage = f.FindAsset<VersusStageData>(f.Map.UserAsset);

            // "Losing" variable based on ln(x+1)

            int ourObjectiveCount = GetTeamObjectiveCount(f, mario->GetTeam(f)) ?? 0;
            FP averageObjectiveCount = GetAverageObjectiveCount(f);

            FP totalChance = 0;
            foreach (AssetRef<CoinItemAsset> coinItemAsset in AllCoinItems) {
                CoinItemAsset coinItem = f.FindAsset(coinItemAsset);
                if (!CanItemSpawn(f, coinItem, fromBlock)) {
                    continue;
                }

                totalChance += GetItemSpawnWeight(f, coinItem, averageObjectiveCount, ourObjectiveCount);
            }

            FP rand = mario->RNG.Next(0, totalChance);
            foreach (AssetRef<CoinItemAsset> coinItemAsset in AllCoinItems) {
                CoinItemAsset coinItem = f.FindAsset(coinItemAsset);
                if (!CanItemSpawn(f, coinItem, fromBlock)) {
                    continue;
                }

                FP chance = GetItemSpawnWeight(f, coinItem, averageObjectiveCount, ourObjectiveCount);

                if (rand < chance) {
                    return coinItem;
                }

                rand -= chance;
            }

            return f.FindAsset(FallbackCoinItem);
        }

        public abstract FP GetItemSpawnWeight(Frame f, CoinItemAsset item, FP averageObjectiveCount, int ourObjectiveCount);

        public virtual int? GetWinningTeam(Frame f, out int winningObjectiveCount) {
            winningObjectiveCount = 0;
            int? winningTeam = null;
            bool tie = false;
            
            Span<int> teamObjectiveCounts = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, teamObjectiveCounts);

            for (int i = 0; i < Constants.MaxPlayers; i++) {
                int objectiveCount = teamObjectiveCounts[i];
                if (objectiveCount < 0) {
                    continue;
                } else if (winningTeam == null) {
                    winningTeam = i;
                    winningObjectiveCount = objectiveCount;
                    tie = false;
                } else if (objectiveCount > winningObjectiveCount) {
                    winningTeam = i;
                    winningObjectiveCount = objectiveCount;
                    tie = false;
                } else if (objectiveCount == winningObjectiveCount) {
                    tie = true;
                }
            }

            return tie ? null : winningTeam;
        }

        public virtual void GetAllTeamsObjectiveCounts(Frame f, Span<int> teamObjectiveCounts) {
            for (int i = 0; i < teamObjectiveCounts.Length; i++) {
                teamObjectiveCounts[i] = -1;
            }

            foreach ((var _, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                if (mario->Disconnected || (mario->Lives <= 0 && f.Global->Rules.IsLivesEnabled)) {
                    continue;
                }
                if (mario->GetTeam(f) is not byte team) {
                    continue;
                }

                if (teamObjectiveCounts[team] == -1) {
                    teamObjectiveCounts[team] = 0;
                }

                if (team < teamObjectiveCounts.Length) {
                    teamObjectiveCounts[team] += GetObjectiveCount(f, mario);
                }
            }
        }

        public virtual int? GetTeamObjectiveCount(Frame f, byte? nullableTeam) {
            if (nullableTeam is not byte team) {
                return null;
            }
            return GetTeamObjectiveCount(f, team);
        }

        public virtual int GetTeamObjectiveCount(Frame f, byte team) {
            int sum = 0;
            foreach ((var _, var mario) in f.Unsafe.GetComponentBlockIterator<MarioPlayer>()) {
                if (mario->GetTeam(f) != team
                    || (mario->Lives <= 0 && f.Global->Rules.IsLivesEnabled)
                    || mario->Disconnected) {
                    continue;
                }

                sum += GetObjectiveCount(f, mario);
            }

            return sum;
        }

        public virtual int GetFirstPlaceObjectiveCount(Frame f) {
            Span<int> teamObjectives = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, teamObjectives);

            int max = 0;
            foreach (int objectiveCount in teamObjectives) {
                if (objectiveCount > max) {
                    max = objectiveCount;
                }
            }

            return max;
        }

        public virtual FP GetAverageObjectiveCount(Frame f) {
            Span<int> teamObjectives = stackalloc int[Constants.MaxPlayers];
            GetAllTeamsObjectiveCounts(f, teamObjectives);

            int aliveTeamCount = 0;
            int aliveTeam = -1;
            for (int i = 0; i < teamObjectives.Length; i++) {
                if (teamObjectives[i] > -1) {
                    aliveTeamCount++;
                    aliveTeam = i;
                }
            }

            int sum = 0;
            foreach (int objectiveCount in teamObjectives) {
                if (objectiveCount > 0) sum += objectiveCount;
            }
            return (FP)sum / aliveTeamCount;
        }

        public virtual EntityRef SpawnLooseCoin(Frame f, FPVector2 position) {
            EntityRef newCoinEntity = f.Create(LooseCoinPrototype);
            var coinTransform = f.Unsafe.GetPointer<Transform2D>(newCoinEntity);
            var coinPhysicsObject = f.Unsafe.GetPointer<PhysicsObject>(newCoinEntity);
            coinTransform->Position = position;
            coinPhysicsObject->Velocity.Y = f.RNG->Next(Constants._4_50, 5);

            return newCoinEntity;
        }
    }
}