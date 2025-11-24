// ============================================================================
// FIXED TRAINING SYSTEM - Bug Fixed!
// ============================================================================
// Fixed: NullReferenceException in ExecuteMove
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace checkersclaude
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
        private const int DiversityResetThreshold = 20;

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
                Population.Add(new AIPlayer(random));
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
            BestPlayer = Population.OrderByDescending(p => p.Brain.Fitness).First();

            if (BestPlayer.Brain.Fitness > historicalBestFitness)
            {
                historicalBestFitness = BestPlayer.Brain.Fitness;
                generationsWithoutImprovement = 0;
            }
            else
            {
                generationsWithoutImprovement++;
            }
        }

        private void UpdateGenerationStats()
        {
            CurrentStats.Generation = Generation;
            CurrentStats.BestFitness = BestPlayer.Brain.Fitness;
            CurrentStats.AverageFitness = Population.Average(p => p.Brain.Fitness);
            CurrentStats.BestWinRate = BestPlayer.Stats.WinRate;
            CurrentStats.AverageWinRate = Population.Average(p => p.Stats.WinRate);
            CurrentStats.BestGamesPlayed = BestPlayer.Stats.GamesPlayed;
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
                Parallel.ForEach(matchups, matchup => PlayMatchup(matchup));
            }
            else
            {
                foreach (var matchup in matchups)
                {
                    PlayMatchup(matchup);
                }
            }
        }

        private void PlayMatchup(Tuple<AIPlayer, AIPlayer> matchup)
        {
            for (int game = 0; game < config.GamesPerPair; game++)
            {
                // Alternate colors for fairness
                bool redFirst = game % 2 == 0;
                AIPlayer red = redFirst ? matchup.Item1 : matchup.Item2;
                AIPlayer black = redFirst ? matchup.Item2 : matchup.Item1;

                PlayGame(red, black);
            }
        }

        private List<Tuple<AIPlayer, AIPlayer>> GenerateMatchups()
        {
            var matchups = new List<Tuple<AIPlayer, AIPlayer>>();
            var sortedPop = Generation > 1
                ? Population.OrderByDescending(p => p.Brain.Fitness).ToList()
                : Population.ToList();

            for (int i = 0; i < Population.Count; i++)
            {
                int numOpponents = Math.Min(config.OpponentsPerPlayer, Population.Count - 1);

                for (int j = 0; j < numOpponents; j++)
                {
                    int opponentIdx = SelectOpponent(i, sortedPop.Count);
                    if (opponentIdx != i)
                    {
                        matchups.Add(new Tuple<AIPlayer, AIPlayer>(
                            Population[i],
                            sortedPop[opponentIdx]));
                    }
                }
            }

            return matchups;
        }

        private int SelectOpponent(int playerIndex, int populationCount)
        {
            // 60% similar skill, 40% random (better learning)
            if (Generation > 1 && random.NextDouble() < 0.6)
            {
                // Similar skill opponent (within ±3 ranks)
                int minRank = Math.Max(0, playerIndex - 3);
                int maxRank = Math.Min(populationCount - 1, playerIndex + 3);
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

        private void PlayGame(AIPlayer redPlayer, AIPlayer blackPlayer)
        {
            GameEngine game = new GameEngine();
            Dictionary<string, int> stateHistory = new Dictionary<string, int>();
            int moveCount = 0;

            while (!game.IsGameOver() && moveCount < config.MaxMovesPerGame)
            {
                AIPlayer currentPlayer = game.State == GameState.RedTurn ? redPlayer : blackPlayer;
                PieceColor currentColor = game.State == GameState.RedTurn ? PieceColor.Red : PieceColor.Black;

                Move move = SelectMove(game, currentPlayer, currentColor);
                if (move == null) break;

                // Track stats and execute move
                TrackMoveStats(currentPlayer, redPlayer, blackPlayer, move, game);

                game.SelectPiece(move.From);
                game.MovePiece(move.To);

                // Check for draw by repetition
                if (IsDrawByRepetition(game.Board, stateHistory))
                {
                    RecordDraw(redPlayer, blackPlayer, game.Board);
                    return;
                }

                moveCount++;
            }

            RecordGameResult(game, redPlayer, blackPlayer);
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

            // Track captures
            currentPlayer.Stats.PiecesCaptured += move.JumpedPositions.Count;

            // Track king captures
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

            // Track king promotions
            Piece movingPiece = game.Board.GetPiece(move.From);
            if (movingPiece != null && movingPiece.Type != PieceType.King)
            {
                PieceColor color = game.State == GameState.RedTurn ? PieceColor.Red : PieceColor.Black;
                int kingRow = color == PieceColor.Red ? 0 : 7;

                if (move.To.Row == kingRow)
                {
                    currentPlayer.Stats.KingsMade++;
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

            red.UpdateGameResult(GameResult.Draw, redPieces, blackPieces);
            black.UpdateGameResult(GameResult.Draw, blackPieces, redPieces);
        }

        private void RecordGameResult(GameEngine game, AIPlayer red, AIPlayer black)
        {
            int redPieces = game.Board.GetAllPieces(PieceColor.Red).Count;
            int blackPieces = game.Board.GetAllPieces(PieceColor.Black).Count;

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
                RecordDraw(red, black, game.Board);
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

            // Elitism: keep best performers
            int eliteCount = Math.Max(2, (int)(populationSize * elitePercentage));
            for (int i = 0; i < eliteCount; i++)
            {
                newGeneration.Add(sortedPop[i].Clone());
            }

            // Breeding: create offspring
            while (newGeneration.Count < populationSize)
            {
                AIPlayer parent1 = TournamentSelect(sortedPop);
                AIPlayer parent2 = TournamentSelect(sortedPop);
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
                rate += weakness * 0.15;
            }

            // Increase if stagnating
            if (generationsWithoutImprovement > 5)
            {
                rate += 0.05 * (generationsWithoutImprovement / 5.0);
            }

            return Math.Min(rate, 0.5); // Cap at 50%
        }

        private AIPlayer TournamentSelect(List<AIPlayer> sortedPopulation)
        {
            const int tournamentSize = 5;
            int size = Math.Min(tournamentSize, sortedPopulation.Count);

            AIPlayer best = sortedPopulation[random.Next(sortedPopulation.Count)];
            for (int i = 1; i < size; i++)
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
            int replaceCount = (int)(populationSize * 0.2);

            // Replace weakest 20% with new random players
            for (int i = 0; i < replaceCount; i++)
            {
                sortedPop[i] = new AIPlayer(random);
            }

            // Heavily mutate middle 20%
            int mutateStart = replaceCount;
            int mutateEnd = mutateStart + replaceCount;

            for (int i = mutateStart; i < mutateEnd && i < sortedPop.Count; i++)
            {
                sortedPop[i].Mutate(0.3);
            }
        }

        #endregion

        #region Reporting

        public string GetGenerationReport()
        {
            string warning = generationsWithoutImprovement > 10
                ? $" ⚠ Stagnation: {generationsWithoutImprovement} gen"
                : "";

            return $"Generation {CurrentStats.Generation} | " +
                   $"Best: {CurrentStats.BestFitness:F2} | " +
                   $"Avg: {CurrentStats.AverageFitness:F2} | " +
                   $"Win Rate: {CurrentStats.BestWinRate:P1} ({CurrentStats.AverageWinRate:P1} avg)" +
                   warning;
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
        public int MaxMovesPerGame { get; set; } = 200;
        public bool UseParallelProcessing { get; set; } = true;
        public int? Seed { get; set; } = null;
    }

    public class TrainingStats
    {
        public int Generation { get; set; }
        public double BestFitness { get; set; }
        public double AverageFitness { get; set; }
        public double BestWinRate { get; set; }
        public double AverageWinRate { get; set; }
        public int BestGamesPlayed { get; set; }
    }

    #endregion
}