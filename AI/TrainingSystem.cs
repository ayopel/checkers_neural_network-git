// ============================================================================
// IMPROVED TRAINING SYSTEM
// ============================================================================
// Fixed: Double-counting captures bug
// Improved: Better matchmaking, adaptive training
// ============================================================================

using checkers_neural_network;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace checkers_neural_network
{
    public class TrainingSystem
    {
        #region Properties

        public List<AIPlayer> Population { get; private set; }
        public int Generation { get; private set; }
        public AIPlayer BestPlayer { get; private set; }
        public TrainingStats CurrentStats { get; private set; }

        private readonly int populationSize;
        private readonly double mutationRate;
        private readonly double elitePercentage;
        private readonly Random random;
        private readonly TrainingConfig config;

        private double historicalBestFitness = 0;
        private int generationsWithoutImprovement = 0;
        private const int DiversityResetThreshold = 15;

        // Thread-safe stats tracking
        private readonly object statsLock = new object();

        #endregion

        #region Initialization

        public TrainingSystem(TrainingConfig config)
        {
            this.config = config;
            this.populationSize = config.PopulationSize;
            this.mutationRate = config.MutationRate;
            this.elitePercentage = config.ElitePercentage;
            this.random = config.Seed.HasValue ? new Random(config.Seed.Value) : new Random();

            Generation = 0;
            CurrentStats = new TrainingStats();
            InitializePopulation();
        }

        private void InitializePopulation()
        {
            Population = new List<AIPlayer>(populationSize);
            for (int i = 0; i < populationSize; i++)
            {
                Population.Add(new AIPlayer(new Random(random.Next())));
            }
        }

        #endregion

        #region Main Training Loop

        public void RunGeneration()
        {
            Generation++;

            ResetPlayerStats();
            RunTournament();
            CalculateAllFitness();
            UpdateBestPlayer();
            UpdateGenerationStats();
            EvolvePopulation();
            CheckForStagnation();
        }

        private void ResetPlayerStats()
        {
            foreach (var player in Population)
            {
                player.Stats.GamesPlayed = 0;
                player.Stats.Wins = 0;
                player.Stats.Losses = 0;
                player.Stats.Draws = 0;
                player.Stats.TotalMoves = 0;
                player.Stats.PiecesCaptured = 0;
                player.Stats.PiecesLost = 0;
                player.Stats.KingsMade = 0;
                player.Stats.KingsCaptured = 0;
                player.Stats.KingsLost = 0;
            }
        }

        private void CalculateAllFitness()
        {
            foreach (var player in Population)
            {
                player.CalculateFitness();
            }
        }

        private void UpdateBestPlayer()
        {
            var currentBest = Population.OrderByDescending(p => p.Brain.Fitness).First();

            if (BestPlayer == null || currentBest.Brain.Fitness > BestPlayer.Brain.Fitness)
            {
                BestPlayer = currentBest.Clone();
            }

            if (currentBest.Brain.Fitness > historicalBestFitness)
            {
                historicalBestFitness = currentBest.Brain.Fitness;
                generationsWithoutImprovement = 0;
            }
            else
            {
                generationsWithoutImprovement++;
            }
        }

        private void UpdateGenerationStats()
        {
            var sortedPop = Population.OrderByDescending(p => p.Brain.Fitness).ToList();

            CurrentStats.Generation = Generation;
            CurrentStats.BestFitness = sortedPop[0].Brain.Fitness;
            CurrentStats.AverageFitness = Population.Average(p => p.Brain.Fitness);
            CurrentStats.BestWinRate = sortedPop[0].Stats.WinRate;
            CurrentStats.AverageWinRate = Population.Average(p => p.Stats.WinRate);
            CurrentStats.BestGamesPlayed = sortedPop[0].Stats.GamesPlayed;
            CurrentStats.MedianFitness = sortedPop[populationSize / 2].Brain.Fitness;
            CurrentStats.WorstFitness = sortedPop.Last().Brain.Fitness;
        }

        private void CheckForStagnation()
        {
            if (generationsWithoutImprovement >= DiversityResetThreshold)
            {
                InjectDiversity();
                generationsWithoutImprovement = 0;
            }
        }

        #endregion

        #region Tournament

        private void RunTournament()
        {
            var matchups = GenerateMatchups();

            if (config.UseParallelProcessing)
            {
                // Use thread-local random for parallel execution
                Parallel.ForEach(matchups, () => new Random(random.Next()),
                    (matchup, state, localRandom) =>
                    {
                        PlayMatchup(matchup, localRandom);
                        return localRandom;
                    },
                    _ => { });
            }
            else
            {
                foreach (var matchup in matchups)
                {
                    PlayMatchup(matchup, random);
                }
            }
        }

        private void PlayMatchup(Tuple<AIPlayer, AIPlayer> matchup, Random localRandom)
        {
            for (int game = 0; game < config.GamesPerPair; game++)
            {
                // Alternate colors for fairness
                bool redFirst = game % 2 == 0;
                AIPlayer red = redFirst ? matchup.Item1 : matchup.Item2;
                AIPlayer black = redFirst ? matchup.Item2 : matchup.Item1;

                PlayGame(red, black, localRandom);
            }
        }

        private List<Tuple<AIPlayer, AIPlayer>> GenerateMatchups()
        {
            var matchups = new List<Tuple<AIPlayer, AIPlayer>>();

            // Sort by fitness for skill-based matchmaking (after first generation)
            var sortedPop = Generation > 1
                ? Population.OrderByDescending(p => p.Brain.Fitness).ToList()
                : Population.OrderBy(p => random.Next()).ToList(); // Shuffle for gen 1

            for (int i = 0; i < Population.Count; i++)
            {
                var selectedOpponents = new HashSet<int>();
                int numOpponents = Math.Min(config.OpponentsPerPlayer, Population.Count - 1);
                int attempts = 0;
                const int maxAttempts = 100;

                while (selectedOpponents.Count < numOpponents && attempts < maxAttempts)
                {
                    int opponentIdx = SelectOpponent(i, sortedPop.Count);

                    if (opponentIdx != i && !selectedOpponents.Contains(opponentIdx))
                    {
                        selectedOpponents.Add(opponentIdx);
                        matchups.Add(new Tuple<AIPlayer, AIPlayer>(
                            sortedPop[i],
                            sortedPop[opponentIdx]));
                    }
                    attempts++;
                }
            }

            return matchups;
        }

        private int SelectOpponent(int playerIndex, int populationCount)
        {
            // Adaptive matchmaking based on generation
            double similarSkillChance = Generation > 10 ? 0.5 : 0.3;

            if (Generation > 1 && random.NextDouble() < similarSkillChance)
            {
                // Similar skill opponent (within ±4 ranks)
                int range = Math.Min(4, populationCount / 4);
                int minRank = Math.Max(0, playerIndex - range);
                int maxRank = Math.Min(populationCount - 1, playerIndex + range);
                return random.Next(minRank, maxRank + 1);
            }
            else
            {
                // Random opponent for diversity
                return random.Next(populationCount);
            }
        }

        #endregion

        #region Game Simulation

        private void PlayGame(AIPlayer redPlayer, AIPlayer blackPlayer, Random localRandom)
        {
            GameEngine game = new GameEngine();
            var stateHistory = new Dictionary<string, int>();
            int moveCount = 0;
            int movesWithoutCapture = 0;

            while (!game.IsGameOver() && moveCount < config.MaxMovesPerGame)
            {
                AIPlayer currentPlayer = game.State == GameState.RedTurn ? redPlayer : blackPlayer;
                PieceColor currentColor = game.State == GameState.RedTurn ? PieceColor.Red : PieceColor.Black;

                Move move = SelectMove(game, currentPlayer, currentColor);
                if (move == null) break;

                // Track stats BEFORE executing move
                TrackMoveStats(currentPlayer, redPlayer, blackPlayer, move, game);

                // Execute move
                game.SelectPiece(move.From);
                game.MovePiece(move.To);

                // Track moves without capture for draw detection
                if (move.IsJump)
                    movesWithoutCapture = 0;
                else
                    movesWithoutCapture++;

                // Check for draw conditions
                if (IsDrawByRepetition(game.Board, stateHistory) || movesWithoutCapture >= 40)
                {
                    RecordDraw(redPlayer, blackPlayer, game.Board);
                    return;
                }

                moveCount++;
            }

            RecordGameResult(game, redPlayer, blackPlayer, moveCount);
        }

        private Move SelectMove(GameEngine game, AIPlayer player, PieceColor color)
        {
            List<Move> validMoves = GetAllValidMoves(game, color);
            if (validMoves.Count == 0) return null;
            return player.ChooseMove(game.Board, validMoves, color);
        }

        private void TrackMoveStats(AIPlayer currentPlayer, AIPlayer redPlayer, AIPlayer blackPlayer, Move move, GameEngine game)
        {
            if (!move.IsJump) return;

            lock (statsLock)
            {
                // Track captures (ONLY here, not in RecordGameResult)
                currentPlayer.Stats.PiecesCaptured += move.JumpedPositions.Count;

                // Track king-specific stats
                foreach (var jumpedPos in move.JumpedPositions)
                {
                    Piece captured = game.Board.GetPiece(jumpedPos);
                    if (captured?.Type == PieceType.King)
                    {
                        currentPlayer.Stats.KingsCaptured++;

                        // Opponent lost a king
                        AIPlayer opponent = currentPlayer == redPlayer ? blackPlayer : redPlayer;
                        opponent.Stats.KingsLost++;
                    }
                }
            }

            // Track king promotions
            Piece movingPiece = game.Board.GetPiece(move.From);
            if (movingPiece != null && movingPiece.Type != PieceType.King)
            {
                PieceColor color = game.State == GameState.RedTurn ? PieceColor.Red : PieceColor.Black;
                int kingRow = color == PieceColor.Red ? 0 : 7;

                if (move.To.Row == kingRow)
                {
                    lock (statsLock)
                    {
                        currentPlayer.Stats.KingsMade++;
                    }
                }
            }
        }

        private bool IsDrawByRepetition(Board board, Dictionary<string, int> stateHistory)
        {
            string boardState = board.GetStateString();
            if (!stateHistory.ContainsKey(boardState))
                stateHistory[boardState] = 0;
            stateHistory[boardState]++;

            return stateHistory[boardState] >= 3;
        }

        private void RecordDraw(AIPlayer red, AIPlayer black, Board board)
        {
            int redPieces = board.GetAllPieces(PieceColor.Red).Count;
            int blackPieces = board.GetAllPieces(PieceColor.Black).Count;

            lock (statsLock)
            {
                red.UpdateGameResult(GameResult.Draw, redPieces, blackPieces);
                black.UpdateGameResult(GameResult.Draw, blackPieces, redPieces);
            }
        }

        private void RecordGameResult(GameEngine game, AIPlayer red, AIPlayer black, int moveCount)
        {
            int redPieces = game.Board.GetAllPieces(PieceColor.Red).Count;
            int blackPieces = game.Board.GetAllPieces(PieceColor.Black).Count;

            lock (statsLock)
            {
                if (game.State == GameState.RedWins)
                {
                    red.UpdateGameResult(GameResult.Win, redPieces, blackPieces);
                    black.UpdateGameResult(GameResult.Loss, blackPieces, redPieces);
                }
                else if (game.State == GameState.BlackWins)
                {
                    black.UpdateGameResult(GameResult.Win, blackPieces, redPieces);
                    red.UpdateGameResult(GameResult.Loss, redPieces, blackPieces);
                }
                else
                {
                    // Timeout or no moves - count as draw
                    red.UpdateGameResult(GameResult.Draw, redPieces, blackPieces);
                    black.UpdateGameResult(GameResult.Draw, blackPieces, redPieces);
                }
            }
        }

        private List<Move> GetAllValidMoves(GameEngine game, PieceColor color)
        {
            List<Move> allMoves = new List<Move>();
            MoveValidator validator = new MoveValidator(game.Board);

            foreach (Piece piece in game.Board.GetAllPieces(color))
            {
                allMoves.AddRange(validator.GetValidMoves(piece));
            }

            return allMoves;
        }

        #endregion

        #region Evolution

        private void EvolvePopulation()
        {
            var sortedPop = Population.OrderByDescending(p => p.Brain.Fitness).ToList();
            List<AIPlayer> newGeneration = new List<AIPlayer>(populationSize);

            // Elitism: keep best performers (unchanged)
            int eliteCount = Math.Max(2, (int)(populationSize * elitePercentage));
            for (int i = 0; i < eliteCount; i++)
            {
                newGeneration.Add(sortedPop[i].Clone());
            }

            // Add one mutated version of the best player
            var mutatedBest = sortedPop[0].Clone();
            mutatedBest.Mutate(mutationRate * 0.5);
            newGeneration.Add(mutatedBest);

            // Breeding: create offspring
            while (newGeneration.Count < populationSize)
            {
                AIPlayer parent1 = TournamentSelect(sortedPop);
                AIPlayer parent2 = TournamentSelect(sortedPop);

                // Ensure different parents
                int attempts = 0;
                while (parent1 == parent2 && attempts < 10)
                {
                    parent2 = TournamentSelect(sortedPop);
                    attempts++;
                }

                AIPlayer child = Breed(parent1, parent2);
                newGeneration.Add(child);
            }

            Population = newGeneration;
        }

        private AIPlayer Breed(AIPlayer parent1, AIPlayer parent2)
        {
            AIPlayer child = parent1.Crossover(parent2, random);
            double adaptiveMutation = CalculateAdaptiveMutationRate(parent1, parent2);
            child.Mutate(adaptiveMutation);
            return child;
        }

        private double CalculateAdaptiveMutationRate(AIPlayer parent1, AIPlayer parent2)
        {
            double rate = mutationRate;

            // Increase for weak parents (exploration)
            double avgParentFitness = (parent1.Brain.Fitness + parent2.Brain.Fitness) / 2.0;
            double bestFitness = BestPlayer?.Brain.Fitness ?? 1.0;

            if (bestFitness > 0)
            {
                double weakness = 1.0 - (avgParentFitness / bestFitness);
                rate += Math.Min(weakness * 0.1, 0.15);
            }

            // Increase if stagnating
            if (generationsWithoutImprovement > 5)
            {
                rate += 0.03 * Math.Min(generationsWithoutImprovement - 5, 10);
            }

            // Decrease mutation in later generations for fine-tuning
            if (Generation > 50 && generationsWithoutImprovement < 5)
            {
                rate *= 0.8;
            }

            return Math.Min(Math.Max(rate, 0.02), 0.4); // Clamp between 2% and 40%
        }

        private AIPlayer TournamentSelect(List<AIPlayer> sortedPopulation)
        {
            int tournamentSize = Math.Min(5, sortedPopulation.Count);

            AIPlayer best = sortedPopulation[random.Next(sortedPopulation.Count)];
            for (int i = 1; i < tournamentSize; i++)
            {
                AIPlayer contestant = sortedPopulation[random.Next(sortedPopulation.Count)];
                if (contestant.Brain.Fitness > best.Brain.Fitness)
                    best = contestant;
            }

            return best;
        }

        #endregion

        #region Diversity Management

        private void InjectDiversity()
        {
            var sortedPop = Population.OrderBy(p => p.Brain.Fitness).ToList();
            int replaceCount = (int)(populationSize * 0.25);

            // Replace weakest 25% with new random players
            for (int i = 0; i < replaceCount && i < sortedPop.Count; i++)
            {
                int popIndex = Population.IndexOf(sortedPop[i]);
                if (popIndex >= 0)
                {
                    Population[popIndex] = new AIPlayer(new Random(random.Next()));
                }
            }

            // Heavily mutate next 15%
            int mutateCount = (int)(populationSize * 0.15);
            for (int i = replaceCount; i < replaceCount + mutateCount && i < sortedPop.Count; i++)
            {
                int popIndex = Population.IndexOf(sortedPop[i]);
                if (popIndex >= 0)
                {
                    Population[popIndex].Mutate(0.35);
                }
            }
        }

        #endregion

        #region Reporting

        public string GetGenerationReport()
        {
            string warning = "";
            if (generationsWithoutImprovement > 10)
                warning = $" ⚠ Stagnation: {generationsWithoutImprovement} gen";
            else if (generationsWithoutImprovement > 5)
                warning = $" ⚡ Plateau: {generationsWithoutImprovement} gen";

            return $"Gen {CurrentStats.Generation} | " +
                   $"Best: {CurrentStats.BestFitness:F1} | " +
                   $"Avg: {CurrentStats.AverageFitness:F1} | " +
                   $"Win: {CurrentStats.BestWinRate:P0} ({CurrentStats.AverageWinRate:P0} avg)" +
                   warning;
        }

        public string GetDetailedReport()
        {
            return $"=== Generation {CurrentStats.Generation} Report ===\n" +
                   $"Best Fitness:    {CurrentStats.BestFitness:F2}\n" +
                   $"Median Fitness:  {CurrentStats.MedianFitness:F2}\n" +
                   $"Average Fitness: {CurrentStats.AverageFitness:F2}\n" +
                   $"Worst Fitness:   {CurrentStats.WorstFitness:F2}\n" +
                   $"Best Win Rate:   {CurrentStats.BestWinRate:P1}\n" +
                   $"Avg Win Rate:    {CurrentStats.AverageWinRate:P1}\n" +
                   $"Stagnation:      {generationsWithoutImprovement} generations\n" +
                   $"Historical Best: {historicalBestFitness:F2}";
        }

        #endregion
    }

    #region Configuration & Stats

    public class TrainingConfig
    {
        public int PopulationSize { get; set; } = 50;
        public double MutationRate { get; set; } = 0.1;
        public double ElitePercentage { get; set; } = 0.1;
        public int GamesPerPair { get; set; } = 2;
        public int OpponentsPerPlayer { get; set; } = 5;
        public int MaxMovesPerGame { get; set; } = 150;
        public bool UseParallelProcessing { get; set; } = true;
        public int? Seed { get; set; } = null;
    }

    public class TrainingStats
    {
        public int Generation { get; set; }
        public double BestFitness { get; set; }
        public double AverageFitness { get; set; }
        public double MedianFitness { get; set; }
        public double WorstFitness { get; set; }
        public double BestWinRate { get; set; }
        public double AverageWinRate { get; set; }
        public int BestGamesPlayed { get; set; }
    }

    #endregion
}