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