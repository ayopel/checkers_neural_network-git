using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace checkersclaude
{
    public class TrainingSystem
    {
        public List<AIPlayer> Population { get; private set; }
        public int Generation { get; private set; }
        public AIPlayer BestPlayer { get; private set; }
        public TrainingStats CurrentStats { get; private set; }

        private readonly int populationSize;
        private readonly double mutationRate;
        private readonly double elitePercentage;
        private readonly Random random;
        private readonly TrainingConfig config;

        // שמירה אוטומטית
        private readonly int autoSaveInterval = 10;
        private readonly string checkpointDir = "checkpoints";

        public TrainingSystem(TrainingConfig config)
        {
            this.config = config;
            this.populationSize = config.PopulationSize;
            this.mutationRate = config.MutationRate;
            this.elitePercentage = config.ElitePercentage;
            this.random = config.Seed.HasValue ? new Random(config.Seed.Value) : new Random();

            Generation = 0;
            CurrentStats = new TrainingStats();

            // יצירת תיקיית checkpoints
            if (!Directory.Exists(checkpointDir))
            {
                try
                {
                    Directory.CreateDirectory(checkpointDir);
                }
                catch { }
            }

            InitializePopulation();
        }

        private void InitializePopulation()
        {
            Population = new List<AIPlayer>();
            for (int i = 0; i < populationSize; i++)
            {
                Population.Add(new AIPlayer(random));
            }
        }

        public void RunGeneration()
        {
            Generation++;

            // איפוס סטטיסטיקות
            foreach (var player in Population)
            {
                lock (player.Stats)
                {
                    player.Stats.GamesPlayed = 0;
                    player.Stats.Wins = 0;
                    player.Stats.Losses = 0;
                    player.Stats.Draws = 0;
                }
            }

            RunTournament();

            foreach (var player in Population)
            {
                player.CalculateFitness();
            }

            BestPlayer = Population.OrderByDescending(p => p.Brain.Fitness).First();

            UpdateStats();

            EvolvePopulation();

            // שמירה אוטומטית
            if (Generation % autoSaveInterval == 0)
            {
                SaveCheckpoint();
            }
        }

        private void RunTournament()
        {
            int gamesPerPair = config.GamesPerPair;

            var matchups = GenerateMatchups();

            if (config.UseParallelProcessing)
            {
                Parallel.ForEach(matchups, matchup =>
                {
                    for (int game = 0; game < gamesPerPair; game++)
                    {
                        AIPlayer red = game % 2 == 0 ? matchup.Item1 : matchup.Item2;
                        AIPlayer black = game % 2 == 0 ? matchup.Item2 : matchup.Item1;
                        PlayGame(red, black);
                    }
                });
            }
            else
            {
                foreach (var matchup in matchups)
                {
                    for (int game = 0; game < gamesPerPair; game++)
                    {
                        AIPlayer red = game % 2 == 0 ? matchup.Item1 : matchup.Item2;
                        AIPlayer black = game % 2 == 0 ? matchup.Item2 : matchup.Item1;
                        PlayGame(red, black);
                    }
                }
            }
        }

        private List<Tuple<AIPlayer, AIPlayer>> GenerateMatchups()
        {
            var matchups = new List<Tuple<AIPlayer, AIPlayer>>();

            for (int i = 0; i < Population.Count; i++)
            {
                int opponents = Math.Min(config.OpponentsPerPlayer, Population.Count - 1);

                for (int j = 0; j < opponents; j++)
                {
                    int opponentIndex = (i + j + 1) % Population.Count;
                    if (opponentIndex != i)
                    {
                        matchups.Add(new Tuple<AIPlayer, AIPlayer>(Population[i], Population[opponentIndex]));
                    }
                }
            }

            return matchups;
        }

        private void PlayGame(AIPlayer redPlayer, AIPlayer blackPlayer)
        {
            GameEngine game = new GameEngine();
            int moveCount = 0;
            int maxMoves = config.MaxMovesPerGame;

            Dictionary<string, int> stateHistory = new Dictionary<string, int>();

            while (game.State != GameState.RedWins &&
                   game.State != GameState.BlackWins &&
                   moveCount < maxMoves)
            {
                AIPlayer currentPlayer = game.State == GameState.RedTurn ? redPlayer : blackPlayer;
                PieceColor currentColor = game.State == GameState.RedTurn ? PieceColor.Red : PieceColor.Black;

                List<Move> validMoves = GetAllValidMoves(game, currentColor);

                if (validMoves.Count == 0)
                    break;

                Move chosenMove = currentPlayer.ChooseMove(game.Board, validMoves, currentColor);

                if (chosenMove == null)
                    break;

                // Thread-safe update של סטטיסטיקות
                if (chosenMove.IsJump)
                {
                    lock (currentPlayer.Stats)
                    {
                        currentPlayer.Stats.PiecesCaptured += chosenMove.JumpedPositions.Count;
                    }
                }

                game.SelectPiece(chosenMove.From);
                game.MovePiece(chosenMove.To);

                Piece movedPiece = game.Board.GetPiece(chosenMove.To);
                if (movedPiece != null && movedPiece.Type == PieceType.King)
                {
                    lock (currentPlayer.Stats)
                    {
                        currentPlayer.Stats.KingsMade++;
                    }
                }

                string boardState = game.Board.GetStateString();
                if (!stateHistory.ContainsKey(boardState))
                    stateHistory[boardState] = 0;
                stateHistory[boardState]++;

                if (stateHistory[boardState] >= 3)
                {
                    UpdateGameResultThreadSafe(redPlayer, GameResult.Draw,
                        game.Board.GetAllPieces(PieceColor.Red).Count,
                        game.Board.GetAllPieces(PieceColor.Black).Count);
                    UpdateGameResultThreadSafe(blackPlayer, GameResult.Draw,
                        game.Board.GetAllPieces(PieceColor.Black).Count,
                        game.Board.GetAllPieces(PieceColor.Red).Count);
                    return;
                }

                moveCount++;
            }

            int redPieces = game.Board.GetAllPieces(PieceColor.Red).Count;
            int blackPieces = game.Board.GetAllPieces(PieceColor.Black).Count;

            if (game.State == GameState.RedWins)
            {
                UpdateGameResultThreadSafe(redPlayer, GameResult.Win, redPieces, blackPieces);
                UpdateGameResultThreadSafe(blackPlayer, GameResult.Loss, blackPieces, redPieces);
            }
            else if (game.State == GameState.BlackWins)
            {
                UpdateGameResultThreadSafe(blackPlayer, GameResult.Win, blackPieces, redPieces);
                UpdateGameResultThreadSafe(redPlayer, GameResult.Loss, redPieces, blackPieces);
            }
            else
            {
                UpdateGameResultThreadSafe(redPlayer, GameResult.Draw, redPieces, blackPieces);
                UpdateGameResultThreadSafe(blackPlayer, GameResult.Draw, blackPieces, redPieces);
            }
        }

        /// <summary>
        /// עדכון תוצאות משחק ב-thread safe
        /// </summary>
        private void UpdateGameResultThreadSafe(AIPlayer player, GameResult result, int pieces, int opponentPieces)
        {
            player.UpdateGameResult(result, pieces, opponentPieces);
        }

        private List<Move> GetAllValidMoves(GameEngine game, PieceColor color)
        {
            List<Move> allMoves = new List<Move>();
            List<Piece> pieces = game.Board.GetAllPieces(color);
            MoveValidator validator = new MoveValidator(game.Board);

            foreach (Piece piece in pieces)
            {
                List<Move> pieceMoves = validator.GetValidMoves(piece);
                allMoves.AddRange(pieceMoves);
            }

            return allMoves;
        }

        /// <summary>
        /// אבולוציה של האוכלוסייה עם diversity injection
        /// </summary>
        private void EvolvePopulation()
        {
            List<AIPlayer> newGeneration = new List<AIPlayer>();

            var sortedPopulation = Population.OrderByDescending(p => p.Brain.Fitness).ToList();

            // שימור אליטה
            int eliteCount = Math.Max(1, (int)(populationSize * elitePercentage));
            for (int i = 0; i < eliteCount; i++)
            {
                newGeneration.Add(sortedPopulation[i].Clone());
            }

            // בדיקת גיוון - אם האוכלוסייה דומה מדי, הוסף פרטים חדשים
            double diversityScore = CalculateDiversity(sortedPopulation);
            int randomInjections = diversityScore < 100.0 ? Math.Min((int)(populationSize * 0.1), 5) : 0;

            // יצירת צאצאים
            while (newGeneration.Count < populationSize - randomInjections)
            {
                AIPlayer parent1 = SelectParent(sortedPopulation);
                AIPlayer parent2 = SelectParent(sortedPopulation);

                AIPlayer child = parent1.Crossover(parent2, random);

                // מוטציה אדפטיבית
                double adaptiveMutationRate = mutationRate * (1.0 + (1.0 - parent1.Brain.Fitness / (BestPlayer.Brain.Fitness + 1.0)));
                child.Mutate(Math.Min(adaptiveMutationRate, 0.5));

                newGeneration.Add(child);
            }

            // הזרקת פרטים אקראיים לשיפור גיוון (Diversity Injection)
            for (int i = 0; i < randomInjections; i++)
            {
                newGeneration.Add(new AIPlayer(random));
            }

            Population = newGeneration;
        }

        /// <summary>
        /// חישוב ציון גיוון של האוכלוסייה
        /// </summary>
        private double CalculateDiversity(List<AIPlayer> population)
        {
            if (population.Count < 2) return 1000.0;

            double totalDifference = 0;
            int comparisons = 0;

            // השווה בין ה-10 הטובים ביותר
            for (int i = 0; i < Math.Min(10, population.Count); i++)
            {
                for (int j = i + 1; j < Math.Min(10, population.Count); j++)
                {
                    totalDifference += Math.Abs(population[i].Brain.Fitness - population[j].Brain.Fitness);
                    comparisons++;
                }
            }

            return comparisons > 0 ? totalDifference / comparisons : 0.0;
        }

        private AIPlayer SelectParent(List<AIPlayer> sortedPopulation)
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

        private void UpdateStats()
        {
            CurrentStats.Generation = Generation;
            CurrentStats.BestFitness = BestPlayer.Brain.Fitness;
            CurrentStats.AverageFitness = Population.Average(p => p.Brain.Fitness);
            CurrentStats.BestWinRate = BestPlayer.Stats.WinRate;
            CurrentStats.AverageWinRate = Population.Average(p => p.Stats.WinRate);
            CurrentStats.BestGamesPlayed = BestPlayer.Stats.GamesPlayed;
        }

        public string GetGenerationReport()
        {
            return $"Generation {CurrentStats.Generation} | " +
                   $"Best Fitness: {CurrentStats.BestFitness:F2} | " +
                   $"Avg Fitness: {CurrentStats.AverageFitness:F2} | " +
                   $"Best Win Rate: {CurrentStats.BestWinRate:P1} | " +
                   $"Avg Win Rate: {CurrentStats.AverageWinRate:P1}";
        }

        /// <summary>
        /// שמירת checkpoint אוטומטית
        /// </summary>
        private void SaveCheckpoint()
        {
            try
            {
                string filename = Path.Combine(checkpointDir, $"checkpoint_gen_{Generation}.dat");
                BestPlayer.Brain.SaveToFile(filename);

                // שמירת מטא-דאטה
                string metaFile = Path.Combine(checkpointDir, $"checkpoint_gen_{Generation}_meta.txt");
                File.WriteAllText(metaFile,
                    $"Generation: {Generation}\n" +
                    $"Best Fitness: {BestPlayer.Brain.Fitness:F2}\n" +
                    $"Avg Fitness: {CurrentStats.AverageFitness:F2}\n" +
                    $"Best Win Rate: {BestPlayer.Stats.WinRate:P1}\n" +
                    $"Avg Win Rate: {CurrentStats.AverageWinRate:P1}\n" +
                    $"Population Size: {populationSize}\n" +
                    $"Mutation Rate: {mutationRate}\n" +
                    $"Elite Percentage: {elitePercentage}\n" +
                    $"Timestamp: {DateTime.Now}");

                // גם שמירה לקובץ הראשי
                BestPlayer.Brain.SaveToFile("best_checkers_ai.dat");
            }
            catch (Exception ex)
            {
                // התעלם משגיאות שמירה
                System.Diagnostics.Debug.WriteLine($"Failed to save checkpoint: {ex.Message}");
            }
        }

        /// <summary>
        /// שמירה ידנית של הרשת הטובה ביותר
        /// </summary>
        public void SaveBestNetwork(string filename = "best_checkers_ai.dat")
        {
            if (BestPlayer != null)
            {
                BestPlayer.Brain.SaveToFile(filename);
            }
        }

        /// <summary>
        /// טעינת checkpoint
        /// </summary>
        public bool LoadCheckpoint(string filename)
        {
            try
            {
                var brain = checkersclaude.AI.DeepNeuralNetwork.LoadFromFile(filename);

                // החלף את הטוב ביותר באוכלוסייה
                if (Population != null && Population.Count > 0)
                {
                    Population[0] = new AIPlayer(brain);
                    BestPlayer = Population[0];
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }

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
}