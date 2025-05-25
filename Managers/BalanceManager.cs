<<<<<<< HEAD
<<<<<<< HEAD
=======
>>>>>>> 182a34f (Merge local)
using CounterStrikeSharp.API.Modules.Utils;

namespace AdvancedTeamBalance
{
    /// <summary>
    /// Manages team balancing operations
    /// </summary>
    public static class BalanceManager
    {
        private static PluginConfig _config = null!;

        public static void Initialize(PluginConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Calculate player value based on the configured balance mode
        /// </summary>
        private static double GetPlayerValue(Player player, string balanceMode)
        {
            return balanceMode.ToLower() switch
            {
                "kd" => player.Stats.KDRatio,
                "kda" => player.Stats.KDARatio,
                "score" => player.Stats.Score,
                "winrate" => player.Stats.WinRate,
                _ => player.Stats.KDARatio // Default to KDA if mode is unrecognized
            };
        }

        /// <summary>
        /// Calculate the average team strength based on the selected balance metric
        /// </summary>
        private static double CalculateTeamStrength(List<Player> team, string balanceMode)
        {
            if (team.Count == 0)
                return 0;

            double totalValue = team.Sum(player => GetPlayerValue(player, balanceMode));
            return totalValue / team.Count;
        }

        /// <summary>
        /// Balance teams by size and skill
        /// </summary>
        /// <returns>BalanceResult containing information about the performed balancing</returns>
        public static BalanceResult BalanceTeams(
            List<Player> tPlayers,
            List<Player> ctPlayers,
            int tWinStreak,
            int ctWinStreak,
            bool logDetails = false)
        {
            string balanceMode = _config.Balancing.BalanceMode;
            int maxTeamSizeDifference = _config.TeamSwitch.MaxTeamSizeDifference;
            double strengthThreshold = _config.Balancing.SkillDifferenceThreshold;

            if (logDetails && _config.General.EnableDebug)
            {
                Console.WriteLine($"[AdvancedTeamBalance] Starting team balance process...");
                Console.WriteLine($"[AdvancedTeamBalance] Using BalanceMode: {balanceMode}");
                Console.WriteLine($"[AdvancedTeamBalance] Config OnlyBalanceByTeamSize: {_config.Balancing.OnlyBalanceByTeamSize}");
                Console.WriteLine($"[AdvancedTeamBalance] Max team size difference: {maxTeamSizeDifference}");
                Console.WriteLine($"[AdvancedTeamBalance] Strength threshold: {strengthThreshold}");
                Console.WriteLine($"[AdvancedTeamBalance] Win streaks - T: {tWinStreak}, CT: {ctWinStreak}");
                Console.WriteLine($"[AdvancedTeamBalance] Initial T Team: {tPlayers.Count} players, CT Team: {ctPlayers.Count} players");
            }

            if (_config.Balancing.OnlyBalanceByTeamSize)
            {
                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine($"[AdvancedTeamBalance] OnlyBalanceByTeamSize is TRUE. Focusing on team size balancing only.");
                }

                // In OnlyBalanceByTeamSize mode, be more aggressive about balancing
                int playersMovedBySize = 0;
                int currentDiff = Math.Abs(tPlayers.Count - ctPlayers.Count);
                
                if (currentDiff > maxTeamSizeDifference)
                {
                    // First try with eligible players
                    playersMovedBySize = BalanceTeamSizes(GetEligiblePlayers([.. tPlayers]), GetEligiblePlayers([.. ctPlayers]), tPlayers, ctPlayers, maxTeamSizeDifference, logDetails);
                    
                    // If still imbalanced, force balance immediately in OnlyBalanceByTeamSize mode
                    int newDiff = Math.Abs(tPlayers.Count - ctPlayers.Count);
                    if (newDiff > maxTeamSizeDifference)
                    {
                        if (logDetails && _config.General.EnableDebug)
                        {
                            Console.WriteLine($"[AdvancedTeamBalance] OnlyBalanceByTeamSize: Still imbalanced after eligible moves (T:{tPlayers.Count}, CT:{ctPlayers.Count}, diff:{newDiff}). Forcing immediate balance.");
                        }
                        playersMovedBySize += ForceTeamSizeBalance(tPlayers, ctPlayers, logDetails);
                    }
                }

                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine($"[AdvancedTeamBalance] OnlyBalanceByTeamSize: Total players moved for size: {playersMovedBySize}. Final T: {tPlayers.Count}, CT: {ctPlayers.Count}");
                }
                return new BalanceResult(0, 0, playersMovedBySize);
            }

            if (balanceMode.StartsWith("scramble", StringComparison.OrdinalIgnoreCase))
            {
                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine($"[AdvancedTeamBalance] Scramble mode selected ({balanceMode}), deferring to specific scramble method called by EventManager.");
                }

                return new BalanceResult(0, 0, 0);
            }

            int swapsMade = 0;

            // Filter out ineligible players for standard balancing moves
            List<Player> eligibleTPlayers = GetEligiblePlayers(tPlayers);
            List<Player> eligibleCTPlayers = GetEligiblePlayers(ctPlayers);

            if (logDetails && _config.General.EnableDebug)
            {
                Console.WriteLine($"[AdvancedTeamBalance] Eligible players for standard moves - T: {eligibleTPlayers.Count}, CT: {eligibleCTPlayers.Count}");
            }

            // First, balance team sizes if needed
            int playersMovedForSize = BalanceTeamSizes(eligibleTPlayers, eligibleCTPlayers, tPlayers, ctPlayers, maxTeamSizeDifference, logDetails);

            // Now balance team strengths if both teams have players and sizes are within limits
            if (tPlayers.Count > 0 && ctPlayers.Count > 0)
            {
                int sizeDiff = Math.Abs(tPlayers.Count - ctPlayers.Count);
                if (sizeDiff <= maxTeamSizeDifference)
                {
                    // Re-calculate eligible players after team size balancing as teams might have changed
                    eligibleTPlayers = GetEligiblePlayers(tPlayers);
                    eligibleCTPlayers = GetEligiblePlayers(ctPlayers);

                    double tStrength = CalculateTeamStrength(tPlayers, balanceMode);
                    double ctStrength = CalculateTeamStrength(ctPlayers, balanceMode);
                    double initialDifference = Math.Abs(tStrength - ctStrength);

                    double boostMultiplier = 1.0;
                    CsTeam losingTeam = CsTeam.None;

                    if (_config.Balancing.BoostAfterLoseStreak > 0)
                    {
                        if (ctWinStreak >= _config.Balancing.BoostAfterLoseStreak) // T team is losing
                        {
                            losingTeam = CsTeam.Terrorist;
                            boostMultiplier = 1.0 + (_config.Balancing.BoostPercentage / 100.0);
                            if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] T team losing streak ({ctWinStreak}), boosting T skill by {_config.Balancing.BoostPercentage}%");
                        }
                        else if (tWinStreak >= _config.Balancing.BoostAfterLoseStreak) // CT team is losing
                        {
                            losingTeam = CsTeam.CounterTerrorist;
                            boostMultiplier = 1.0 + (_config.Balancing.BoostPercentage / 100.0);
                            if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] CT team losing streak ({tWinStreak}), boosting CT skill by {_config.Balancing.BoostPercentage}%");
                        }
                    }

                    if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] Initial team strengths - T: {tStrength:F2}, CT: {ctStrength:F2}, Difference: {initialDifference:F2}");

                    if (initialDifference <= strengthThreshold && losingTeam == CsTeam.None)
                    {
                        if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] Teams already balanced by skill within threshold ({initialDifference:F2} <= {strengthThreshold})");
                        return new BalanceResult(initialDifference, 0, playersMovedForSize);
                    }

                    bool tIsStrongerOriginal = tStrength > ctStrength;
                    bool tIsStrongerForBalancing = tIsStrongerOriginal;

                    if (losingTeam != CsTeam.None)
                    {
                        // Force the losing team to be treated as weaker to ensure it gets better players or keeps its good ones.
                        tIsStrongerForBalancing = losingTeam == CsTeam.CounterTerrorist;
                        if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] Overriding strength: {losingTeam} is losing, so {(tIsStrongerForBalancing ? "T" : "CT")} is considered stronger for balancing.");
                    }
                    
                    List<Player> strongerTeamSource = tIsStrongerForBalancing ? tPlayers : ctPlayers;
                    List<Player> weakerTeamSource = tIsStrongerForBalancing ? ctPlayers : tPlayers;
                    List<Player> eligibleStrongerTeam = tIsStrongerForBalancing ? eligibleTPlayers : eligibleCTPlayers;
                    List<Player> eligibleWeakerTeam = tIsStrongerForBalancing ? eligibleCTPlayers : eligibleTPlayers;

                    swapsMade = BalanceTeamSkill(
                        eligibleStrongerTeam,
                        eligibleWeakerTeam,
                        strongerTeamSource,
                        weakerTeamSource,
                        balanceMode,
                        strengthThreshold,
                        boostMultiplier,
                        losingTeam,
                        logDetails);

                    double finalTStrength = CalculateTeamStrength(tPlayers, balanceMode);
                    double finalCTStrength = CalculateTeamStrength(ctPlayers, balanceMode);
                    double finalDifference = Math.Abs(finalTStrength - finalCTStrength);
                    if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] Final team strengths - T: {finalTStrength:F2}, CT: {finalCTStrength:F2}, Difference: {finalDifference:F2}");
                    return new BalanceResult(finalDifference, swapsMade, playersMovedForSize);
                }
                else
                {
                     if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] Skill balancing skipped due to team size difference ({sizeDiff}) > MaxTeamSizeDifference ({maxTeamSizeDifference}) after size balancing.");
                }
            }

            double finalT = CalculateTeamStrength(tPlayers, balanceMode);
            double finalCT = CalculateTeamStrength(ctPlayers, balanceMode);
            double finalDiff = Math.Abs(finalT - finalCT);
            return new BalanceResult(finalDiff, swapsMade, playersMovedForSize);
        }


        /// <summary>
        /// Get players eligible for team switching
        /// </summary>
        private static List<Player> GetEligiblePlayers(List<Player> players)
        {
            int minRoundsBeforeSwitch = _config.TeamSwitch.MinRoundsBeforeSwitch;
            if (players == null)
                return [];
            return [.. players.Where(p => p != null && p.CanBeSwitched(minRoundsBeforeSwitch))];
        }

        /// <summary>
        /// Balance teams by size, moving players from larger to smaller team
        /// Modifies allTPlayers and allCTPlayers lists.
        /// </summary>
        private static int BalanceTeamSizes(
            List<Player> eligibleTPlayers,
            List<Player> eligibleCTPlayers,
            List<Player> allTPlayers,
            List<Player> allCTPlayers,
            int maxTeamSizeDifference,
            bool logDetails)
        {
            allTPlayers ??= [];
            allCTPlayers ??= [];
            eligibleTPlayers ??= [];
            eligibleCTPlayers ??= [];

            int currentTCount = allTPlayers.Count;
            int currentCTCount = allCTPlayers.Count;
            int sizeDiff = Math.Abs(currentTCount - currentCTCount);
            int playersMoved = 0;

            if (sizeDiff <= maxTeamSizeDifference)
            {
                if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] BalanceTeamSizes: Teams already balanced in size ({currentTCount} vs {currentCTCount}). Max diff: {maxTeamSizeDifference}.");
                return 0;
            }

            bool tIsLarger = currentTCount > currentCTCount;
            List<Player> eligibleLargerTeam = tIsLarger ? eligibleTPlayers : eligibleCTPlayers;
            List<Player> sourceTeamListAll = tIsLarger ? allTPlayers : allCTPlayers;
            List<Player> targetTeamListAll = tIsLarger ? allCTPlayers : allTPlayers;
            CsTeam targetTeamEnum = tIsLarger ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
            CsTeam sourceTeamEnum = tIsLarger ? CsTeam.Terrorist : CsTeam.CounterTerrorist;

            int playersToMoveCount = (sizeDiff - maxTeamSizeDifference + 1) / 2;

            playersToMoveCount = Math.Min(playersToMoveCount, eligibleLargerTeam.Count);

            if (playersToMoveCount <= 0)
            {
                if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] BalanceTeamSizes: No players to move for size balancing. Target moves: {(sizeDiff - maxTeamSizeDifference + 1) / 2}, Eligible on larger team: {eligibleLargerTeam.Count}. Larger team ({sourceTeamEnum}) has {currentTCount} (T) vs {currentCTCount} (CT).");
                return 0;
            }
            
            if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] BalanceTeamSizes: Need to move {playersToMoveCount} eligible players from {sourceTeamEnum} ({ (tIsLarger ? currentTCount : currentCTCount) }) to {targetTeamEnum} ({ (tIsLarger ? currentCTCount : currentTCount) }).");

            List<Player> finalPlayersToMove;
            if (_config.Balancing.OnlyBalanceByTeamSize)
            {
                finalPlayersToMove = [.. eligibleLargerTeam
                                        .OrderBy(p => p.RoundsOnCurrentTeam) 
                                        .ThenBy(_ => Guid.NewGuid())
                                        .Take(playersToMoveCount)];
                if (logDetails && _config.General.EnableDebug) 
                    Console.WriteLine($"[AdvancedTeamBalance] BalanceTeamSizes (OnlyBalanceByTeamSize=true): Selecting players to move based on RoundsOnCurrentTeam then Random.");
            }
            else
            {
                var balanceModeForSort = _config.Balancing.BalanceMode;
                if (balanceModeForSort.StartsWith("scramble", StringComparison.OrdinalIgnoreCase))
                {
                    balanceModeForSort = "KDA";
                }

                finalPlayersToMove = [.. eligibleLargerTeam
                                        .OrderBy(p => GetPlayerValue(p, balanceModeForSort))
                                        .ThenBy(_ => Guid.NewGuid())
                                        .Take(playersToMoveCount)];
                 if (logDetails && _config.General.EnableDebug) 
                    Console.WriteLine($"[AdvancedTeamBalance] BalanceTeamSizes (OnlyBalanceByTeamSize=false): Selecting players to move based on skill ({balanceModeForSort}) then Random.");
            }

            foreach (var player in finalPlayersToMove)
            {
                if (sourceTeamListAll.Remove(player))
                {
                    targetTeamListAll.Add(player);
                    player.UpdateTeamState(targetTeamEnum, _config.TeamSwitch.SwitchImmunityTime);
                    playersMoved++;
                    if (logDetails && _config.General.EnableDebug)
                    {
                        double playerValueDebug = _config.Balancing.OnlyBalanceByTeamSize ? player.RoundsOnCurrentTeam : GetPlayerValue(player, _config.Balancing.BalanceMode.StartsWith("scramble", StringComparison.OrdinalIgnoreCase) ? "KDA" : _config.Balancing.BalanceMode);
                        string sortMetricDebug = _config.Balancing.OnlyBalanceByTeamSize ? "RoundsOnTeam" : (_config.Balancing.BalanceMode.StartsWith("scramble", StringComparison.OrdinalIgnoreCase) ? "KDA" : _config.Balancing.BalanceMode);
                        Console.WriteLine($"[AdvancedTeamBalance] BalanceTeamSizes: Moved Player {player.Name} (Metric ({sortMetricDebug}): {playerValueDebug:F2}) from {sourceTeamEnum} to {targetTeamEnum} for size balance.");
                    }
                }
                else
                {
                     if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] BalanceTeamSizes: WARNING - Player {player.Name} was selected to move from {sourceTeamEnum} but was not found in its source list (allTPlayers/allCTPlayers). This indicates a potential list desync.");
                }
            }
            return playersMoved;
        }

        /// <summary>
        /// Balance teams by skill, potentially boosting a losing team
        /// Modifies allStrongerTeam and allWeakerTeam lists.
        /// </summary>
        private static int BalanceTeamSkill(
            List<Player> eligibleStrongerTeam,
            List<Player> eligibleWeakerTeam,
            List<Player> allStrongerTeam,
            List<Player> allWeakerTeam,
            string balanceMode,
            double strengthThreshold,
            double boostMultiplier,
            CsTeam actualLosingTeam,
            bool logDetails)
        {
            int swapsMade = 0;
            int maxSwapsToConsider = 2;

            if (eligibleStrongerTeam.Count == 0 || eligibleWeakerTeam.Count == 0)
            {
                if (logDetails && _config.General.EnableDebug) Console.WriteLine("[AdvancedTeamBalance] Not enough eligible players (one team has 0 eligibles) for skill balancing.");
                return 0;
            }

            CsTeam strongerTeamId = allStrongerTeam.FirstOrDefault()?.Team ?? CsTeam.None;
            CsTeam weakerTeamId = allWeakerTeam.FirstOrDefault()?.Team ?? CsTeam.None;
            
            if(strongerTeamId == CsTeam.None || weakerTeamId == CsTeam.None) {
                 if (logDetails && _config.General.EnableDebug) Console.WriteLine("[AdvancedTeamBalance] Skill balancing aborted: Could not determine team ID for one or both teams (possibly empty).");
                return 0;
            }


            for (int iteration = 0; iteration < maxSwapsToConsider; iteration++)
            {
                double currentStrongerStrength = CalculateTeamStrength(allStrongerTeam, balanceMode);
                double currentWeakerStrength = CalculateTeamStrength(allWeakerTeam, balanceMode);
                
                double effectiveWeakerStrength = currentWeakerStrength;
                if (actualLosingTeam != CsTeam.None && weakerTeamId == actualLosingTeam) // If current weaker team is the one on losing streak
                {
                    effectiveWeakerStrength *= boostMultiplier; 
                     if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] Iteration {iteration}: Weaker team ({weakerTeamId}) is losing. Boosted strength for check: {currentWeakerStrength:F2} -> {effectiveWeakerStrength:F2}. Stronger team ({strongerTeamId}) strength: {currentStrongerStrength:F2}");
                }

                double currentEffectiveDifference = Math.Abs(currentStrongerStrength - effectiveWeakerStrength);
                
                if (currentStrongerStrength <= effectiveWeakerStrength || currentEffectiveDifference <= strengthThreshold)
                {
                    if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] Iteration {iteration}: Teams considered balanced by skill. Stronger: {currentStrongerStrength:F2}, Effective Weaker: {effectiveWeakerStrength:F2}, Diff: {currentEffectiveDifference:F2} <= Threshold: {strengthThreshold}. Swaps made in this cycle: {swapsMade}.");
                    break;
                }

                var bestSwap = FindBestSwap(
                    eligibleStrongerTeam, eligibleWeakerTeam,
                    allStrongerTeam, allWeakerTeam,
                    balanceMode,
                    actualLosingTeam,
                    boostMultiplier);

                if (bestSwap == null)
                {
                    if (logDetails && _config.General.EnableDebug) Console.WriteLine("[AdvancedTeamBalance] No beneficial skill swap found.");
                    break;
                }

                var (strongPlayerToSwap, weakPlayerToSwap, newPredictedDiff) = bestSwap.Value;
                
                double newActualDiffAfterSwap = CalculatePotentialActualDifference(allStrongerTeam, allWeakerTeam, strongPlayerToSwap, weakPlayerToSwap, balanceMode);

                if (newActualDiffAfterSwap >= Math.Abs(currentStrongerStrength - currentWeakerStrength) && iteration > 0)
                {
                     if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] Best swap found ({strongPlayerToSwap.Name} <=> {weakPlayerToSwap.Name}) results in new actual diff {newActualDiffAfterSwap:F2}, not better than current actual diff {Math.Abs(currentStrongerStrength-currentWeakerStrength):F2}. Stopping swaps.");
                    break;
                }

                // Perform the swap
                allStrongerTeam.Remove(strongPlayerToSwap);
                allWeakerTeam.Remove(weakPlayerToSwap);
                allStrongerTeam.Add(weakPlayerToSwap);
                allWeakerTeam.Add(strongPlayerToSwap);

                strongPlayerToSwap.UpdateTeamState(weakerTeamId, _config.TeamSwitch.SwitchImmunityTime);
                weakPlayerToSwap.UpdateTeamState(strongerTeamId, _config.TeamSwitch.SwitchImmunityTime);

                eligibleStrongerTeam.Remove(strongPlayerToSwap);
                eligibleWeakerTeam.Remove(weakPlayerToSwap);

                swapsMade++;
                if (logDetails && _config.General.EnableDebug)
                {
                    double spVal = GetPlayerValue(strongPlayerToSwap, balanceMode);
                    double wpVal = GetPlayerValue(weakPlayerToSwap, balanceMode);
                    Console.WriteLine($"[AdvancedTeamBalance] Skill Swap {swapsMade}: {strongPlayerToSwap.Name} (Val: {spVal:F2}) from {strongerTeamId} with {weakPlayerToSwap.Name} (Val: {wpVal:F2}) from {weakerTeamId}. New predicted diff (from FindBestSwap logic): {newPredictedDiff:F2}");
                }
                eligibleStrongerTeam = GetEligiblePlayers(allStrongerTeam);
                eligibleWeakerTeam = GetEligiblePlayers(allWeakerTeam);
                if (eligibleStrongerTeam.Count == 0 || eligibleWeakerTeam.Count == 0) break;
            }
            return swapsMade;
        }
        
        private static double CalculatePotentialActualDifference(List<Player> currentStrongerTeam, List<Player> currentWeakerTeam, Player pStrong, Player pWeak, string balanceMode)
        {
            // Simulate the swap
            var tempStrongerTeam = new List<Player>(currentStrongerTeam.Where(p => p.SteamId != pStrong.SteamId))
            {
                pWeak
            };
            var tempWeakerTeam = new List<Player>(currentWeakerTeam.Where(p => p.SteamId != pWeak.SteamId))
            {
                pStrong
            };

            double sStrength = CalculateTeamStrength(tempStrongerTeam, balanceMode);
            double wStrength = CalculateTeamStrength(tempWeakerTeam, balanceMode);
            return Math.Abs(sStrength - wStrength);
        }


        /// <summary>
        /// Find the best player swap to balance team strengths, potentially prioritizing a losing team
        /// </summary>
        private static (Player StrongPlayer, Player WeakPlayer, double NewDifferenceScore)? FindBestSwap(
            List<Player> eligibleStrongerTeam, List<Player> eligibleWeakerTeam,
            List<Player> allStrongerTeamMembers, List<Player> allWeakerTeamMembers,
            string balanceMode, CsTeam actualLosingTeam, double boostMultiplier)
        {
            (Player, Player, double)? bestSwapFound = null;
            double bestDifferenceScore = double.MaxValue;

            CsTeam strongerTeamId = allStrongerTeamMembers.FirstOrDefault()?.Team ?? CsTeam.None;
            CsTeam weakerTeamId = allWeakerTeamMembers.FirstOrDefault()?.Team ?? CsTeam.None;

            if (strongerTeamId == CsTeam.None || weakerTeamId == CsTeam.None) return null; // Should not happen

            foreach (var pStrong in eligibleStrongerTeam)
            {
                foreach (var pWeak in eligibleWeakerTeam)
                {
                    var tempStrongerTeam = new List<Player>(allStrongerTeamMembers.Where(p => p.SteamId != pStrong.SteamId))
                    {
                        pWeak
                    };
                    var tempWeakerTeam = new List<Player>(allWeakerTeamMembers.Where(p => p.SteamId != pWeak.SteamId))
                    {
                        pStrong
                    };

                    double newStrongerStrength = CalculateTeamStrength(tempStrongerTeam, balanceMode);
                    double newWeakerStrength = CalculateTeamStrength(tempWeakerTeam, balanceMode);
                    
                    double currentActualDifference = Math.Abs(newStrongerStrength - newWeakerStrength);
                    double scoreForThisSwap = currentActualDifference;

                    // If the weaker team (that pStrong is moving to) is the actualLosingTeam,
                    // this swap is "good" for the boost. We want to favor it.
                    // Reduce its "difference score" to make it more attractive.
                    if (actualLosingTeam != CsTeam.None && weakerTeamId == actualLosingTeam)
                    {
                        // Player pStrong (from stronger team) is moving to weakerTeam (which is losing)
                        // Player pWeak (from weaker team) is moving to strongerTeam
                        // This should generally result in pStrong > pWeak if it's to help the losing team.
                        if (GetPlayerValue(pStrong, balanceMode) > GetPlayerValue(pWeak, balanceMode))
                        {
                            scoreForThisSwap *= 1.0 / boostMultiplier; // Make score smaller (better)
                            // Example: if boostMultiplier is 1.2, score becomes score / 1.2
                        }
                    }
                    
                    if (scoreForThisSwap < bestDifferenceScore)
                    {
                        bestDifferenceScore = scoreForThisSwap;
                        bestSwapFound = (pStrong, pWeak, currentActualDifference);
                    }
                }
            }
            return bestSwapFound;
        }


        /// <summary>
        /// Scrambles teams randomly
        /// </summary>
        public static bool ScrambleTeamsRandom(List<Player> allPlayers, bool logDetails = false)
        {
            if (allPlayers.Count < 2)
            {
                if (logDetails && _config.General.EnableDebug) Console.WriteLine("[AdvancedTeamBalance] Not enough players for scramble (minimum 2 required)");
                return false;
            }

            var eligiblePlayers = allPlayers.Where(p => !p.IsExemptFromSwitching && !p.IsAlive).ToList();
            if (eligiblePlayers.Count < 2)
            {
                if (logDetails && _config.General.EnableDebug) Console.WriteLine("[AdvancedTeamBalance] Not enough non-exempt/non-alive players for scramble");
                return false;
            }

            var random = new Random();
            var shuffled = eligiblePlayers.OrderBy(_ => random.Next()).ToList();
            var halfwayPoint = (int)Math.Ceiling(shuffled.Count / 2.0);

            for (int i = 0; i < shuffled.Count; i++)
            {
                var newTeam = i < halfwayPoint ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
                if (shuffled[i].Team != newTeam)
                {
                    shuffled[i].UpdateTeamState(newTeam, 0);
                    if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] Scramble (Random): Assigned {shuffled[i].Name} to {newTeam}");
                }
            }

            if (_config.Balancing.ResetStatsAfterScramble)
            {
                foreach (var player in eligiblePlayers) player.Stats.Reset();
                if (logDetails && _config.General.EnableDebug) Console.WriteLine("[AdvancedTeamBalance] Reset player stats after random scramble for eligible players.");
            }
            return true;
        }

        /// <summary>
        /// Scrambles teams based on skill, trying to create balanced teams
        /// </summary>
        public static bool ScrambleTeamsBySkill(List<Player> allPlayers, bool logDetails = false)
        {
            if (allPlayers.Count < 2)
            {
                if (logDetails && _config.General.EnableDebug) Console.WriteLine("[AdvancedTeamBalance] Not enough players for skill-based scramble (minimum 2 required)");
                return false;
            }

            var eligiblePlayers = allPlayers.Where(p => !p.IsExemptFromSwitching && !p.IsAlive).ToList();
            if (eligiblePlayers.Count < 2)
            {
                if (logDetails && _config.General.EnableDebug) Console.WriteLine("[AdvancedTeamBalance] Not enough non-exempt/non-alive players for skill-based scramble");
                return false;
            }
            
            string balanceModeForSort = _config.Balancing.BalanceMode;

            if (balanceModeForSort.StartsWith("scramble", StringComparison.OrdinalIgnoreCase)) balanceModeForSort = "KDA";

            var sortedPlayers = eligiblePlayers.OrderByDescending(p => GetPlayerValue(p, balanceModeForSort)).ToList();

            if (logDetails && _config.General.EnableDebug)
            {
                Console.WriteLine("[AdvancedTeamBalance] Scrambling teams by skill (sorted by " + balanceModeForSort + "):");
                foreach (var player in sortedPlayers) Console.WriteLine($"  - {player.Name}: {GetPlayerValue(player, balanceModeForSort):F2}");
            }

            List<Player> teamT = [];
            List<Player> teamCT = [];
            double skillT = 0, skillCT = 0;

            foreach (var player in sortedPlayers)
            {
                double playerSkill = GetPlayerValue(player, balanceModeForSort);
                if (skillT <= skillCT || (teamT.Count <= teamCT.Count && eligiblePlayers.Count % 2 != 0 && teamT.Count == teamCT.Count))
                {
                    teamT.Add(player);
                    skillT += playerSkill;
                }
                else
                {
                    teamCT.Add(player);
                    skillCT += playerSkill;
                }
            }
            
            foreach(var player in teamT) {
                if(player.Team != CsTeam.Terrorist) {
                    player.UpdateTeamState(CsTeam.Terrorist, 0);
                     if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] Skill Scramble: Assigned {player.Name} to Terrorist");
                }
            }
            foreach(var player in teamCT) {
                 if(player.Team != CsTeam.CounterTerrorist) {
                    player.UpdateTeamState(CsTeam.CounterTerrorist, 0);
                    if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] Skill Scramble: Assigned {player.Name} to CounterTerrorist");
                 }
            }


            if (_config.Balancing.ResetStatsAfterScramble)
            {
                foreach (var player in eligiblePlayers) player.Stats.Reset();
                if (logDetails && _config.General.EnableDebug) Console.WriteLine("[AdvancedTeamBalance] Reset player stats after skill scramble for eligible players.");
            }
            return true;
        }

        /// <summary>
        /// Forces team size balance by moving players.
        /// In OnlyBalanceByTeamSize mode, ignores most restrictions to ensure MaxTeamSizeDifference.
        /// Modifies tPlayers and ctPlayers lists.
        /// </summary>
        public static int ForceTeamSizeBalance(
            List<Player> tPlayers,
            List<Player> ctPlayers,
            bool logDetails = false)
        {
            if (tPlayers == null || ctPlayers == null) return 0;

            int tCount = tPlayers.Count;
            int ctCount = ctPlayers.Count;
            int diff = tCount - ctCount; // Positive if T is larger, negative if CT is larger
            int maxDiff = _config.TeamSwitch.MaxTeamSizeDifference;

            if (Math.Abs(diff) <= maxDiff) return 0; // Already balanced enough

            int playersMoved = 0;

            int numToMove = (Math.Abs(diff) - maxDiff + 1) / 2;
            if (numToMove <= 0) return 0;

            List<Player> sourceTeam = diff > 0 ? tPlayers : ctPlayers;
            List<Player> destinationTeam = diff > 0 ? ctPlayers : tPlayers;
            CsTeam destinationEnum = diff > 0 ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
            CsTeam sourceEnum = diff > 0 ? CsTeam.Terrorist : CsTeam.CounterTerrorist;

            IEnumerable<Player> candidateQuery;

            if (_config.Balancing.OnlyBalanceByTeamSize)
            {
                // In OnlyBalanceByTeamSize mode, be very aggressive - only respect admin exemptions
                candidateQuery = sourceTeam
                    .Where(p => !p.IsExemptFromSwitching)  // Only check admin exemptions
                    .OrderBy(p => p.IsAlive ? 1 : 0)       // Prefer dead players first
                    .ThenBy(p => p.RoundsOnCurrentTeam);   // Then by rounds on team
                    
                if (logDetails && _config.General.EnableDebug) 
                    Console.WriteLine($"[AdvancedTeamBalance] FORCED BALANCE (OnlyBalanceByTeamSize=true): Using aggressive mode - ignoring alive status and immunities, only respecting admin exemptions.");
            }
            else
            {
                // Normal mode - respect all restrictions
                candidateQuery = sourceTeam
                    .Where(p => !p.IsExemptFromSwitching && !p.IsAlive)
                    .OrderBy(p => p.RoundsOnCurrentTeam);
                    
                if (logDetails && _config.General.EnableDebug) 
                    Console.WriteLine($"[AdvancedTeamBalance] FORCED BALANCE (Normal mode): Using standard restrictions.");
            }

            var candidatesToMove = candidateQuery
                .Take(numToMove)
                .ToList();

            if (candidatesToMove.Count == 0)
            {
                if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] FORCED BALANCE: No eligible candidates on {sourceEnum} to move.");
                return 0;
            }
            
            if (logDetails && _config.General.EnableDebug) Console.WriteLine($"[AdvancedTeamBalance] FORCED BALANCE: Attempting to move {candidatesToMove.Count} (max {numToMove}) players from {sourceEnum} to {destinationEnum}.");

            foreach (var player in candidatesToMove)
            {
                sourceTeam.Remove(player);
                destinationTeam.Add(player);
                
                // Use different immunity times based on aggressiveness
                int immunityTime = _config.Balancing.OnlyBalanceByTeamSize ? 0 : _config.TeamSwitch.SwitchImmunityTime / 2;
                player.UpdateTeamState(destinationEnum, immunityTime);
                playersMoved++;
                
                if (logDetails && _config.General.EnableDebug) 
                    Console.WriteLine($"[AdvancedTeamBalance] FORCED move: {player.Name} to {destinationEnum} (rounds on team: {player.RoundsOnCurrentTeam}, was alive: {player.IsAlive}, immunity: {immunityTime}s)");
            }

            return playersMoved;
        }
    }

    /// <summary>
    /// Contains the results of a team balancing operation
    /// </summary>
    public class BalanceResult(double finalDifference, int swapsMade, int playersMoved)
    {
        /// <summary>
        /// Final skill difference between teams (0 if OnlyBalanceByTeamSize is true)
        /// </summary>
        public double FinalDifference { get; } = finalDifference;

        /// <summary>
        /// Number of player swaps made for skill balancing (0 if OnlyBalanceByTeamSize is true)
        /// </summary>
        public int SwapsMade { get; } = swapsMade;

        /// <summary>
        /// Number of players moved for size balancing
        /// </summary>
        public int PlayersMoved { get; } = playersMoved;
    }
}
<<<<<<< HEAD
=======
using CounterStrikeSharp.API.Modules.Utils;

namespace AdvancedTeamBalance
{
    /// <summary>
    /// Manages team balancing operations
    /// </summary>
    public static class BalanceManager
    {
        private static PluginConfig _config = null!;

        public static void Initialize(PluginConfig config)
        {
            _config = config;
        }
        
        /// <summary>
        /// Calculate player value based on the configured balance mode
        /// </summary>
        private static double GetPlayerValue(Player player, string balanceMode)
        {
            return balanceMode.ToLower() switch
            {
                "kd" => player.Stats.KDRatio,
                "kda" => player.Stats.KDARatio,
                "score" => player.Stats.Score,
                "winrate" => player.Stats.WinRate,
                _ => player.Stats.KDARatio
            };
        }
        
        /// <summary>
        /// Calculate the average team strength based on the selected balance metric
        /// </summary>
        private static double CalculateTeamStrength(List<Player> team, string balanceMode)
        {
            if (team.Count == 0)
                return 0;
                
            double totalValue = team.Sum(player => GetPlayerValue(player, balanceMode));
            return totalValue / team.Count;
        }
        
        /// <summary>
        /// Balance teams by size and skill
        /// </summary>
        /// <returns>BalanceResult containing information about the performed balancing</returns>
        public static BalanceResult BalanceTeams(
            List<Player> tPlayers, 
            List<Player> ctPlayers,
            int tWinStreak,
            int ctWinStreak,
            bool logDetails = false)
        {
            string balanceMode = _config.Balancing.BalanceMode;
            int maxTeamSizeDifference = _config.TeamSwitch.MaxTeamSizeDifference;
            double strengthThreshold = _config.Balancing.SkillDifferenceThreshold / 100.0;
            
            if (logDetails && _config.General.EnableDebug)
            {
                Console.WriteLine($"[AdvancedTeamBalance] Starting team balance process...");
                Console.WriteLine($"[AdvancedTeamBalance] Using BalanceMode: {balanceMode}");
                Console.WriteLine($"[AdvancedTeamBalance] Max team size difference: {maxTeamSizeDifference}");
                Console.WriteLine($"[AdvancedTeamBalance] Strength threshold: {strengthThreshold}");
                Console.WriteLine($"[AdvancedTeamBalance] Win streaks - T: {tWinStreak}, CT: {ctWinStreak}");
                Console.WriteLine($"[AdvancedTeamBalance] T Team: {tPlayers.Count} players, CT Team: {ctPlayers.Count} players");
            }
            
            // Skip scramble modes - they're handled separately
            if (balanceMode.StartsWith("scramble", StringComparison.OrdinalIgnoreCase))
            {
                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine($"[AdvancedTeamBalance] Scramble mode selected, deferring to scramble method.");
                }
                return new BalanceResult(0, 0, 0);
            }
            
            int swapsMade = 0;

            // First, filter out ineligible players
            bool isRoundPrestart = EventManager.IsRoundPreStartPhase();
            List<Player> eligibleTPlayers = GetEligiblePlayers(tPlayers, isRoundPrestart);
            List<Player> eligibleCTPlayers = GetEligiblePlayers(ctPlayers, isRoundPrestart);
            
            if (logDetails && _config.General.EnableDebug)
            {
                Console.WriteLine($"[AdvancedTeamBalance] Eligible players - T: {eligibleTPlayers.Count}, CT: {eligibleCTPlayers.Count}");
                
                if (eligibleTPlayers.Count == 0 || eligibleCTPlayers.Count == 0)
                {
                    Console.WriteLine($"[AdvancedTeamBalance] Not enough eligible players. IsRoundPrestart: {isRoundPrestart}");
                    
                    // Debug why players are ineligible
                    int exemptT = tPlayers.Count(p => p.IsExemptFromSwitching);
                    int immunityT = tPlayers.Count(p => p.ImmunityTimeRemaining > 0);
                    int tooNewT = tPlayers.Count(p => p.RoundsOnCurrentTeam < _config.TeamSwitch.MinRoundsBeforeSwitch);
                    int aliveT = tPlayers.Count(p => p.IsAlive);
                    
                    int exemptCT = ctPlayers.Count(p => p.IsExemptFromSwitching);
                    int immunityCT = ctPlayers.Count(p => p.ImmunityTimeRemaining > 0);
                    int tooNewCT = ctPlayers.Count(p => p.RoundsOnCurrentTeam < _config.TeamSwitch.MinRoundsBeforeSwitch);
                    int aliveCT = ctPlayers.Count(p => p.IsAlive);
                    
                    Console.WriteLine($"[AdvancedTeamBalance] T ineligible: Exempt={exemptT}, Immunity={immunityT}, TooNew={tooNewT}, Alive={aliveT}");
                    Console.WriteLine($"[AdvancedTeamBalance] CT ineligible: Exempt={exemptCT}, Immunity={immunityCT}, TooNew={tooNewCT}, Alive={aliveCT}");
                }
            }

            // First, balance team sizes if needed
            int playersMoved = BalanceTeamSizes(eligibleTPlayers, eligibleCTPlayers, tPlayers, ctPlayers, maxTeamSizeDifference, logDetails);

            // Now balance team strengths if both teams have players
            if (tPlayers.Count > 0 && ctPlayers.Count > 0)
            {
                // Only do skill balancing if size balancing has been resolved
                int sizeDiff = Math.Abs(tPlayers.Count - ctPlayers.Count);
                if (sizeDiff <= maxTeamSizeDifference)
                {
                    // Re-calculate eligible players after team size balancing
                    eligibleTPlayers = GetEligiblePlayers(tPlayers, isRoundPrestart);
                    eligibleCTPlayers = GetEligiblePlayers(ctPlayers, isRoundPrestart);
                    
                    // Calculate initial team strengths
                    double tStrength = CalculateTeamStrength(tPlayers, balanceMode);
                    double ctStrength = CalculateTeamStrength(ctPlayers, balanceMode);
                    double initialDifference = Math.Abs(tStrength - ctStrength);
                    
                    // Check for skill boosting for losing team if configured
                    double boostMultiplier = 1.0;
                    CsTeam losingTeam = CsTeam.None;
                    
                    if (_config.Balancing.BoostAfterLoseStreak > 0)
                    {
                        // Check T team losing streak
                        if (ctWinStreak >= _config.Balancing.BoostAfterLoseStreak)
                        {
                            // T team is losing, needs boost
                            losingTeam = CsTeam.Terrorist;
                            boostMultiplier = 1.0 + (_config.Balancing.BoostPercentage / 100.0);
                            
                            if (logDetails && _config.General.EnableDebug)
                            {
                                Console.WriteLine($"[AdvancedTeamBalance] T team has lost {ctWinStreak} rounds in a row");
                                Console.WriteLine($"[AdvancedTeamBalance] Boosting T team skill by {_config.Balancing.BoostPercentage}%");
                            }
                        }
                        // Check CT team losing streak
                        else if (tWinStreak >= _config.Balancing.BoostAfterLoseStreak)
                        {
                            // CT team is losing, needs boost
                            losingTeam = CsTeam.CounterTerrorist;
                            boostMultiplier = 1.0 + (_config.Balancing.BoostPercentage / 100.0);
                            
                            if (logDetails && _config.General.EnableDebug)
                            {
                                Console.WriteLine($"[AdvancedTeamBalance] CT team has lost {tWinStreak} rounds in a row");
                                Console.WriteLine($"[AdvancedTeamBalance] Boosting CT team skill by {_config.Balancing.BoostPercentage}%");
                            }
                        }
                    }
                    
                    if (logDetails && _config.General.EnableDebug)
                    {
                        Console.WriteLine($"[AdvancedTeamBalance] Initial team strengths - T: {tStrength:F2}, CT: {ctStrength:F2}, Difference: {initialDifference:F2}");
                    }
                    
                    // If team strengths are already balanced enough, we're done
                    double weakerTeamStrength = Math.Min(tStrength, ctStrength);
                    if ((initialDifference <= strengthThreshold || 
                        (weakerTeamStrength > 0 && initialDifference / weakerTeamStrength <= strengthThreshold)) 
                        && losingTeam == CsTeam.None)
                    {
                        if (logDetails && _config.General.EnableDebug)
                        {
                            Console.WriteLine($"[AdvancedTeamBalance] Teams are already balanced within threshold ({initialDifference:F2} <= {strengthThreshold})");
                        }
                        return new BalanceResult(initialDifference, 0, playersMoved);
                    }
                    
                    // If the losing team needs boosting, always try to make a swap
                    // regardless of whether there are eligible players
                    if (losingTeam != CsTeam.None)
                    {
                        // If no eligible players but we have a losing team that needs boosting,
                        // try a forced swap
                        if ((eligibleTPlayers.Count == 0 || eligibleCTPlayers.Count == 0) && isRoundPrestart)
                        {
                            // This is round prestart, so consider all non-exempt players eligible
                            eligibleTPlayers = tPlayers.Where(p => !p.IsExemptFromSwitching).ToList();
                            eligibleCTPlayers = ctPlayers.Where(p => !p.IsExemptFromSwitching).ToList();
                            
                            if (logDetails && _config.General.EnableDebug)
                            {
                                Console.WriteLine($"[AdvancedTeamBalance] Forcing eligibility for boosting losing team during prestart");
                                Console.WriteLine($"[AdvancedTeamBalance] Forced eligible - T: {eligibleTPlayers.Count}, CT: {eligibleCTPlayers.Count}");
                            }
                        }
                    }
                    
                    // Check again if we have eligible players
                    if (eligibleTPlayers.Count == 0 || eligibleCTPlayers.Count == 0)
                    {
                        if (logDetails && _config.General.EnableDebug)
                        {
                            Console.WriteLine($"[AdvancedTeamBalance] Still not enough eligible players after eligibility override attempt");
                        }
                        return new BalanceResult(initialDifference, 0, playersMoved);
                    }
                    
                    // Determine which team is stronger (without considering boost)
                    bool tIsStronger = tStrength > ctStrength;
                    
                    // If there's a losing team that needs boosting, override normal strength comparison
                    if (losingTeam != CsTeam.None)
                    {
                        // Force the losing team to be treated as the weaker team
                        tIsStronger = losingTeam != CsTeam.Terrorist;
                        
                        if (logDetails && _config.General.EnableDebug)
                        {
                            Console.WriteLine($"[AdvancedTeamBalance] Overriding team strength comparison to favor boosting {losingTeam}");
                        }
                    }
                    
                    List<Player> strongerTeam, weakerTeam;
                    List<Player> eligibleStrongerTeam, eligibleWeakerTeam;
                    if (tIsStronger)
                    {
                        strongerTeam = tPlayers;
                        weakerTeam = ctPlayers;
                        eligibleStrongerTeam = eligibleTPlayers;
                        eligibleWeakerTeam = eligibleCTPlayers;
                    }
                    else
                    {
                        strongerTeam = ctPlayers;
                        weakerTeam = tPlayers;
                        eligibleStrongerTeam = eligibleCTPlayers;
                        eligibleWeakerTeam = eligibleTPlayers;
                    }
                    
                    // Final check before attempting to balance team skill
                    if (eligibleStrongerTeam.Count == 0 || eligibleWeakerTeam.Count == 0)
                    {
                        if (logDetails && _config.General.EnableDebug)
                        {
                            Console.WriteLine($"[AdvancedTeamBalance] Cannot balance team skill: not enough eligible players in stronger/weaker teams");
                        }
                        return new BalanceResult(initialDifference, 0, playersMoved);
                    }
                    
                    // Perform skill balancing with potential boost
                    swapsMade = BalanceTeamSkill(
                        eligibleStrongerTeam, 
                        eligibleWeakerTeam, 
                        strongerTeam, 
                        weakerTeam, 
                        balanceMode, 
                        strengthThreshold,
                        boostMultiplier,
                        losingTeam,
                        logDetails);
                    
                    // Calculate final difference for return value
                    double finalTStrength = CalculateTeamStrength(tPlayers, balanceMode);
                    double finalCTStrength = CalculateTeamStrength(ctPlayers, balanceMode);
                    double finalDifference = Math.Abs(finalTStrength - finalCTStrength);
                    
                    if (logDetails && _config.General.EnableDebug)
                    {
                        Console.WriteLine($"[AdvancedTeamBalance] Final team strengths - T: {finalTStrength:F2}, CT: {finalCTStrength:F2}, Difference: {finalDifference:F2}");
                    }
                    
                    return new BalanceResult(finalDifference, swapsMade, playersMoved);
                }
            }
            
            // Calculate final difference for return value
            double tFinalStrength = CalculateTeamStrength(tPlayers, balanceMode);
            double ctFinalStrength = CalculateTeamStrength(ctPlayers, balanceMode);
            double difference = Math.Abs(tFinalStrength - ctFinalStrength);
            
            return new BalanceResult(difference, swapsMade, playersMoved);
        }
        
        /// <summary>
        /// Get players eligible for team switching
        /// </summary>
        private static List<Player> GetEligiblePlayers(List<Player> players, bool isRoundPrestart = false)
        {
            int minRoundsBeforeSwitch = _config.TeamSwitch.MinRoundsBeforeSwitch;
            
            // During round prestart, we can ignore the alive check
            if (isRoundPrestart)
            {
                return [.. players.Where(p => !p.IsExemptFromSwitching && 
                                            p.ImmunityTimeRemaining <= 0 && 
                                            p.RoundsOnCurrentTeam >= minRoundsBeforeSwitch)];
            }
            
            // Normal eligibility check
            return [.. players.Where(p => p.CanBeSwitched(minRoundsBeforeSwitch))];
        }
        
        /// <summary>
        /// Balance teams by size, moving players from larger to smaller team
        /// </summary>
        private static int BalanceTeamSizes(
            List<Player> eligibleTPlayers,
            List<Player> eligibleCTPlayers,
            List<Player> allTPlayers,
            List<Player> allCTPlayers,
            int maxTeamSizeDifference,
            bool logDetails)
        {
            int sizeDiff = Math.Abs(allTPlayers.Count - allCTPlayers.Count);
            int playersMoved = 0;
            
            if (sizeDiff <= maxTeamSizeDifference)
            {
                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine("[AdvancedTeamBalance] Teams are already balanced in size.");
                }
                return 0;
            }
            
            // Determine which team has more players
            bool tIsLarger = allTPlayers.Count > allCTPlayers.Count;
            
            List<Player> eligibleLargerTeam, allLargerTeam, allSmallerTeam;
            CsTeam largerTeam, smallerTeam;
            
            if (tIsLarger)
            {
                eligibleLargerTeam = eligibleTPlayers;
                allLargerTeam = allTPlayers;
                allSmallerTeam = allCTPlayers;
                largerTeam = CsTeam.Terrorist;
                smallerTeam = CsTeam.CounterTerrorist;
            }
            else
            {
                eligibleLargerTeam = eligibleCTPlayers;
                allLargerTeam = allCTPlayers;
                allSmallerTeam = allTPlayers;
                largerTeam = CsTeam.CounterTerrorist;
                smallerTeam = CsTeam.Terrorist;
            }
            
            int playersToMove = (sizeDiff - maxTeamSizeDifference + 1) / 2;
            playersToMove = Math.Min(playersToMove, eligibleLargerTeam.Count);
            
            if (playersToMove <= 0 || eligibleLargerTeam.Count == 0)
            {
                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine("[AdvancedTeamBalance] No eligible players for normal balancing, trying forced balance...");
                }
                
                return ForceTeamSizeBalance(allTPlayers, allCTPlayers, logDetails);
            }
            
            if (logDetails && _config.General.EnableDebug)
            {
                Console.WriteLine($"[AdvancedTeamBalance] Need to move {playersToMove} players to balance team sizes");
            }
            
            // Sort eligible players by skill (lowest first to minimize negative impact)
            var balanceMode = _config.Balancing.BalanceMode;
            var sortedPlayers = eligibleLargerTeam.OrderBy(p => GetPlayerValue(p, balanceMode)).ToList();
            
            // Move the players with the lowest skill to the smaller team
            for (int i = 0; i < playersToMove && i < sortedPlayers.Count; i++)
            {
                var player = sortedPlayers[i];
                
                // Update our tracking
                allLargerTeam.Remove(player);
                allSmallerTeam.Add(player);
                
                // Apply immunity to prevent ping-ponging
                player.UpdateTeamState(smallerTeam, _config.TeamSwitch.SwitchImmunityTime);
                
                playersMoved++;
                
                if (logDetails && _config.General.EnableDebug)
                {
                    double playerValue = GetPlayerValue(player, balanceMode);
                    Console.WriteLine($"[AdvancedTeamBalance] Moved Player {player.Name} (Value: {playerValue:F2}) from {largerTeam} to {smallerTeam}");
                }
            }
            
            return playersMoved;
        }
        
        /// <summary>
        /// Balance teams by skill, potentially boosting a losing team
        /// </summary>
        private static int BalanceTeamSkill(
            List<Player> eligibleStrongerTeam,
            List<Player> eligibleWeakerTeam,
            List<Player> allStrongerTeam,
            List<Player> allWeakerTeam,
            string balanceMode,
            double strengthThreshold,
            double boostMultiplier,
            CsTeam losingTeam,
            bool logDetails)
        {
            int swapsMade = 0;
            int maxSwaps = Math.Min(4, Math.Max(eligibleStrongerTeam.Count, eligibleWeakerTeam.Count));
            
            // If no eligible players, can't balance
            if (eligibleStrongerTeam.Count == 0 || eligibleWeakerTeam.Count == 0)
            {
                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine("[AdvancedTeamBalance] Not enough eligible players for skill balancing.");
                }
                return 0;
            }
            
            CsTeam strongerTeamId = eligibleStrongerTeam[0].Team;
            CsTeam weakerTeamId = eligibleWeakerTeam[0].Team;
            
            // Apply boost if the weaker team is the losing team that needs boosting
            double effectiveThreshold = strengthThreshold;
            if (losingTeam != CsTeam.None && weakerTeamId == losingTeam)
            {
                effectiveThreshold *= boostMultiplier;
                
                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine($"[AdvancedTeamBalance] Applied boosting to threshold: {strengthThreshold:F3} -> {effectiveThreshold:F3}");
                }
            }
            
            // Iteratively find the best swap
            for (int iteration = 0; iteration < maxSwaps; iteration++)
            {
                double strongerTeamStrength = CalculateTeamStrength(allStrongerTeam, balanceMode);
                double weakerTeamStrength = CalculateTeamStrength(allWeakerTeam, balanceMode);
                double currentDifference = Math.Abs(strongerTeamStrength - weakerTeamStrength);
                
                // If teams are balanced enough (considering potential boost), we're done
                if (currentDifference <= effectiveThreshold)
                {
                    if (logDetails && _config.General.EnableDebug)
                    {
                        Console.WriteLine($"[AdvancedTeamBalance] Teams balanced within threshold after {iteration} swaps.");
                        if (losingTeam != CsTeam.None)
                        {
                            Console.WriteLine($"[AdvancedTeamBalance] Using boosted threshold: {effectiveThreshold:F3}");
                        }
                    }
                    break;
                }
                
                var bestSwap = FindBestSwap(
                    eligibleStrongerTeam, 
                    eligibleWeakerTeam, 
                    allStrongerTeam, 
                    allWeakerTeam, 
                    balanceMode,
                    losingTeam);
                    
                if (bestSwap == null)
                {
                    if (logDetails && _config.General.EnableDebug)
                    {
                        Console.WriteLine("[AdvancedTeamBalance] No valid swap found.");
                    }
                    break;
                }
                
                var (strongPlayer, weakPlayer, newDiff) = bestSwap.Value;
                
                // Only swap if it improves the balance
                if (newDiff >= currentDifference)
                {
                    if (logDetails && _config.General.EnableDebug)
                    {
                        Console.WriteLine("[AdvancedTeamBalance] No swap would improve balance further.");
                    }
                    break;
                }
                
                // Perform the swap
                allStrongerTeam.Remove(strongPlayer);
                allWeakerTeam.Remove(weakPlayer);
                allStrongerTeam.Add(weakPlayer);
                allWeakerTeam.Add(strongPlayer);
                
                // Update player team states and apply immunity
                strongPlayer.UpdateTeamState(weakerTeamId, _config.TeamSwitch.SwitchImmunityTime);
                weakPlayer.UpdateTeamState(strongerTeamId, _config.TeamSwitch.SwitchImmunityTime);
                
                // Remove these players from eligible lists to prevent re-swapping
                eligibleStrongerTeam.Remove(strongPlayer);
                eligibleWeakerTeam.Remove(weakPlayer);
                
                swapsMade++;
                
                if (logDetails && _config.General.EnableDebug)
                {
                    double strongValue = GetPlayerValue(strongPlayer, balanceMode);
                    double weakValue = GetPlayerValue(weakPlayer, balanceMode);
                    Console.WriteLine($"[AdvancedTeamBalance] Swapped {strongPlayer.Name} (Value: {strongValue:F2}) from {strongerTeamId} with {weakPlayer.Name} (Value: {weakValue:F2}) from {weakerTeamId}");
                }
            }
            
            return swapsMade;
        }
        
        /// <summary>
        /// Find the best player swap to balance team strengths, potentially prioritizing a losing team
        /// </summary>
        private static (Player, Player, double)? FindBestSwap(
            List<Player> eligibleStrongerTeam,
            List<Player> eligibleWeakerTeam,
            List<Player> allStrongerTeam,
            List<Player> allWeakerTeam,
            string balanceMode,
            CsTeam losingTeam)
        {
            if (eligibleStrongerTeam.Count == 0 || eligibleWeakerTeam.Count == 0)
            {
                return null;
            }
            
            (Player, Player, double)? bestSwap = null;
            double bestDiff = double.MaxValue;
            
            bool boostingLosingTeam = losingTeam != CsTeam.None;
            
            CsTeam weakerTeamId = eligibleWeakerTeam[0].Team;
            bool weakerTeamIsLosing = weakerTeamId == losingTeam;
            
            // Get current strength difference for comparison
            double currentStrongerTeamStrength = CalculateTeamStrength(allStrongerTeam, balanceMode);
            double currentWeakerTeamStrength = CalculateTeamStrength(allWeakerTeam, balanceMode);
            double currentDiff = Math.Abs(currentStrongerTeamStrength - currentWeakerTeamStrength);
            
            foreach (var player1 in eligibleStrongerTeam)
            {
                foreach (var player2 in eligibleWeakerTeam)
                {
                    // Get player skill values
                    double player1Value = GetPlayerValue(player1, balanceMode);
                    double player2Value = GetPlayerValue(player2, balanceMode);
                    
                    // If we're boosting the losing team and the weaker team is losing,
                    // only consider swaps where the stronger player goes to the losing team
                    if (boostingLosingTeam && weakerTeamIsLosing && player1Value <= player2Value)
                    {
                        // Skip this swap as it doesn't help the losing team
                        continue;
                    }
                    
                    // Calculate new team strengths if players were swapped
                    var newStrongerTeam = new List<Player>(allStrongerTeam);
                    newStrongerTeam.Remove(player1);
                    newStrongerTeam.Add(player2);
                    
                    var newWeakerTeam = new List<Player>(allWeakerTeam);
                    newWeakerTeam.Remove(player2);
                    newWeakerTeam.Add(player1);
                    
                    double newStrongerTeamStrength = CalculateTeamStrength(newStrongerTeam, balanceMode);
                    double newWeakerTeamStrength = CalculateTeamStrength(newWeakerTeam, balanceMode);
                    
                    double newDiff = Math.Abs(newStrongerTeamStrength - newWeakerTeamStrength);
                    
                    if (newStrongerTeam.Count != newWeakerTeam.Count)
                    {
                        newDiff *= 1.0 + (0.1 * Math.Abs(newStrongerTeam.Count - newWeakerTeam.Count));
                    }
                    
                    double minNewStrength = Math.Min(newStrongerTeamStrength, newWeakerTeamStrength);
                    if (minNewStrength > 0)
                    {
                        double relativeDiff = newDiff / minNewStrength;
                        
                        newDiff = (newDiff * 0.6) + (relativeDiff * 0.4);
                    }
                    
                    if (boostingLosingTeam && weakerTeamIsLosing)
                    {
                        if (player1Value > player2Value)
                        {
                            newDiff *= 0.8;
                        }
                    }
                    
                    // If this swap produces a better balance, remember it
                    if (newDiff < bestDiff)
                    {
                        bestDiff = newDiff;
                        bestSwap = (player1, player2, newDiff);
                    }
                }
            }
            
            if (bestSwap != null && bestDiff >= currentDiff)
            {
                return null;
            }
            
            return bestSwap;
        }
        
        /// <summary>
        /// Scrambles teams randomly
        /// </summary>
        public static bool ScrambleTeamsRandom(List<Player> allPlayers, bool logDetails = false)
        {
            if (allPlayers.Count < 4)
            {
                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine("[AdvancedTeamBalance] Not enough players for scramble (minimum 4 required)");
                }
                return false;
            }
            
            // Filter to eligible players (those not exempt and not alive)
            // If during round prestart, ignore alive status
            var eligiblePlayers = EventManager.IsRoundPreStartPhase() 
                ? allPlayers.Where(p => !p.IsExemptFromSwitching).ToList() 
                : allPlayers.Where(p => !p.IsExemptFromSwitching && !p.IsAlive).ToList();
            
            if (eligiblePlayers.Count < 4)
            {
                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine("[AdvancedTeamBalance] Not enough non-exempt/non-alive players for scramble");
                }
                return false;
            }
            
            // Shuffle the players
            var random = new Random();
            var shuffled = eligiblePlayers.OrderBy(_ => random.Next()).ToList();
            
            // Split into even teams
            var halfwayPoint = shuffled.Count / 2;
            
            // Assign to teams
            for (int i = 0; i < shuffled.Count; i++)
            {
                var newTeam = i < halfwayPoint ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
                
                // Only update if needed
                if (shuffled[i].Team != newTeam)
                {
                    shuffled[i].UpdateTeamState(newTeam, 0);
                    
                    if (logDetails && _config.General.EnableDebug)
                    {
                        Console.WriteLine($"[AdvancedTeamBalance] Scramble: Assigned {shuffled[i].Name} to {newTeam}");
                    }
                }
            }
            
            // Reset player stats if configured
            if (_config.Balancing.ResetStatsAfterScramble)
            {
                foreach (var player in eligiblePlayers)
                {
                    player.Stats.Reset();
                }
                
                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine("[AdvancedTeamBalance] Reset player stats after scramble");
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Scrambles teams based on skill, trying to create balanced teams
        /// </summary>
        public static bool ScrambleTeamsBySkill(List<Player> allPlayers, bool logDetails = false)
        {
            if (allPlayers.Count < 4)
            {
                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine("[AdvancedTeamBalance] Not enough players for skill-based scramble (minimum 4 required)");
                }
                return false;
            }
            
            var eligiblePlayers = EventManager.IsRoundPreStartPhase() 
                ? allPlayers.Where(p => !p.IsExemptFromSwitching).ToList() 
                : allPlayers.Where(p => !p.IsExemptFromSwitching && !p.IsAlive).ToList();
            
            if (eligiblePlayers.Count < 4)
            {
                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine("[AdvancedTeamBalance] Not enough non-exempt/non-alive players for skill-based scramble");
                }
                return false;
            }
            
            // Sort by skill (highest to lowest)
            string balanceMode = _config.Balancing.BalanceMode;
            var sortedPlayers = eligiblePlayers.OrderByDescending(p => GetPlayerValue(p, balanceMode)).ToList();
            
            if (logDetails && _config.General.EnableDebug)
            {
                Console.WriteLine("[AdvancedTeamBalance] Scrambling teams by skill:");
                foreach (var player in sortedPlayers)
                {
                    Console.WriteLine($"  - {player.Name}: {GetPlayerValue(player, balanceMode):F2}");
                }
            }
            
            // Distribute players using snake draft pattern
            // Team T gets 1st, 4th, 5th, 8th, etc.
            // Team CT gets 2nd, 3rd, 6th, 7th, etc.
            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                var player = sortedPlayers[i];
                var modFour = i % 4;
                CsTeam newTeam;
                
                if (modFour == 0 || modFour == 3)
                {
                    newTeam = CsTeam.Terrorist;
                }
                else
                {
                    newTeam = CsTeam.CounterTerrorist;
                }
                
                if (player.Team != newTeam)
                {
                    player.UpdateTeamState(newTeam, 0);
                    
                    if (logDetails && _config.General.EnableDebug)
                    {
                        Console.WriteLine($"[AdvancedTeamBalance] Skill scramble: Assigned {player.Name} to {newTeam}");
                    }
                }
            }
            
            // Reset player stats if configured
            if (_config.Balancing.ResetStatsAfterScramble)
            {
                foreach (var player in eligiblePlayers)
                {
                    player.Stats.Reset();
                }
                
                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine("[AdvancedTeamBalance] Reset player stats after scramble");
                }
            }
            
            return true;
        }

        public static int ForceTeamSizeBalance(
            List<Player> tPlayers,
            List<Player> ctPlayers,
            bool logDetails = false)
        {
            // Calculate difference
            int tCount = tPlayers.Count;
            int ctCount = ctPlayers.Count;
            int diff = Math.Abs(tCount - ctCount);
            int maxDiff = _config.TeamSwitch.MaxTeamSizeDifference;
            
            if (diff <= maxDiff)
                return 0;
                
            bool tIsLarger = tCount > ctCount;
            var sourceTeam = tIsLarger ? tPlayers : ctPlayers;
            var targetTeam = tIsLarger ? ctPlayers : tPlayers;
            
            int playersToMove = (diff - maxDiff + 1) / 2;
            
            bool ignoreAliveStatus = EventManager.IsRoundPreStartPhase();
            
            var candidatesToMove = sourceTeam
                .Where(p => !p.IsExemptFromSwitching && (ignoreAliveStatus || !p.IsAlive))
                .OrderBy(p => p.RoundsOnCurrentTeam)
                .ToList();
            
            if (candidatesToMove.Count == 0)
            {
                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine("[AdvancedTeamBalance] No candidates for forced balance found");
                    int exempt = sourceTeam.Count(p => p.IsExemptFromSwitching);
                    int alive = sourceTeam.Count(p => p.IsAlive);
                    Console.WriteLine($"[AdvancedTeamBalance] Source team players that are: Exempt={exempt}, Alive={alive}");
                }
                return 0;
            }
                
            int playersMoved = 0;
            for (int i = 0; i < Math.Min(playersToMove, candidatesToMove.Count); i++)
            {
                var player = candidatesToMove[i];
                CsTeam newTeam = tIsLarger ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
                
                sourceTeam.Remove(player);
                targetTeam.Add(player);
                
                player.UpdateTeamState(newTeam, _config.TeamSwitch.SwitchImmunityTime / 2);
                playersMoved++;
                
                if (logDetails && _config.General.EnableDebug)
                {
                    Console.WriteLine($"[AdvancedTeamBalance] FORCED move: {player.Name} to {newTeam} (rounds on team: {player.RoundsOnCurrentTeam})");
                }
            }
            
            return playersMoved;
        }
    }
    
    /// <summary>
    /// Contains the results of a team balancing operation
    /// </summary>
    public class BalanceResult(double finalDifference, int swapsMade, int playersMoved)
    {
        /// <summary>
        /// Final skill difference between teams
        /// </summary>
        public double FinalDifference { get; } = finalDifference;

        /// <summary>
        /// Number of player swaps made for skill balancing
        /// </summary>
        public int SwapsMade { get; } = swapsMade;

        /// <summary>
        /// Number of players moved for size balancing
        /// </summary>
        public int PlayersMoved { get; } = playersMoved;
    }
}
>>>>>>> 74fc461956bce7b38451c220f391a210b6af4d41
=======
>>>>>>> 182a34f (Merge local)
