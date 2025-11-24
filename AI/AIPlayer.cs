using System;
using System.Collections.Generic;
using System.Collections.Concurrent; // ← חשוב!
using System.Linq;
using checkersclaude.AI;

namespace checkersclaude
{
    public class AIPlayer
    {
        public DeepNeuralNetwork Brain { get; private set; }
        public PlayerStats Stats { get; private set; }

        private const int InputSize = 64;
        private static readonly int[] HiddenSizes = { 128, 64, 32 };
        private const int OutputSize = 1;

        // ✅ FIX: Use ConcurrentDictionary instead of Dictionary for thread safety
        private ConcurrentDictionary<string, double> evaluationCache;
        private const int MAX_CACHE_SIZE = 10000;

        public AIPlayer(Random random = null)
        {
            Brain = new DeepNeuralNetwork(InputSize, HiddenSizes, OutputSize, random);
            Stats = new PlayerStats();
            evaluationCache = new ConcurrentDictionary<string, double>();
        }

        public AIPlayer(DeepNeuralNetwork brain)
        {
            Brain = brain;
            Stats = new PlayerStats();
            evaluationCache = new ConcurrentDictionary<string, double>();
        }

        public Move ChooseMove(Board board, List<Move> validMoves, PieceColor color)
        {
            if (validMoves == null || validMoves.Count == 0)
                return null;

            if (validMoves.Count == 1)
                return validMoves[0];

            // Prioritize jumps (forced captures)
            var jumpMoves = validMoves.Where(m => m.IsJump).ToList();
            if (jumpMoves.Count > 0)
            {
                // Choose best jump based on evaluation
                return jumpMoves.OrderByDescending(m =>
                    EvaluateMove(board, m, color)).First();
            }

            // Evaluate all regular moves
            double bestScore = double.MinValue;
            Move bestMove = validMoves[0];

            foreach (Move move in validMoves)
            {
                double score = EvaluateMove(board, move, color);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
            }

            Stats.TotalMoves++;
            return bestMove;
        }

        private double EvaluateMove(Board board, Move move, PieceColor color)
        {
            string cacheKey = $"{board.GetStateString()}_{move.From}_{move.To}";

            // ✅ FIX: Use TryGetValue for thread-safe read
            if (evaluationCache.TryGetValue(cacheKey, out double cachedValue))
                return cachedValue;

            double[] boardState = GetBoardStateAfterMove(board, move, color);
            double[] output = Brain.FeedForward(boardState);

            double strategicBonus = CalculateStrategicValue(board, move, color);
            double finalScore = output[0] + strategicBonus * 0.15;

            // ✅ FIX: Thread-safe cache management
            if (evaluationCache.Count >= MAX_CACHE_SIZE)
            {
                // Clear 50% of cache to avoid contention
                var keysToRemove = evaluationCache.Keys.Take(MAX_CACHE_SIZE / 2).ToList();
                foreach (var key in keysToRemove)
                {
                    evaluationCache.TryRemove(key, out _);
                }
            }

            // ✅ FIX: TryAdd is thread-safe
            evaluationCache.TryAdd(cacheKey, finalScore);

            return finalScore;
        }

        private double[] GetBoardStateAfterMove(Board board, Move move, PieceColor color)
        {
            double[] state = new double[InputSize];
            Board simBoard = SimulateMove(board, move);

            int index = 0;
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Position pos = new Position(row, col);
                    Piece piece = simBoard.GetPiece(pos);

                    if (piece == null)
                    {
                        state[index] = 0.0;
                    }
                    else if (piece.Color == color)
                    {
                        // Base value for piece type
                        state[index] = piece.Type == PieceType.King ? 3.0 : 1.0;

                        // Add positional bonus
                        state[index] += GetAdvancedPositionValue(pos, piece, color) * 0.6;
                    }
                    else
                    {
                        // Negative value for opponent pieces
                        state[index] = piece.Type == PieceType.King ? -3.0 : -1.0;
                        state[index] -= GetAdvancedPositionValue(pos, piece, piece.Color) * 0.6;
                    }

                    index++;
                }
            }

            return state;
        }

        private Board SimulateMove(Board board, Move move)
        {
            Board simBoard = board.Clone();
            Piece movingPiece = simBoard.GetPiece(move.From);

            if (movingPiece != null)
            {
                simBoard.RemovePiece(move.From);

                if (move.IsJump && move.JumpedPositions != null)
                {
                    foreach (Position jumped in move.JumpedPositions)
                        simBoard.RemovePiece(jumped);
                }

                simBoard.SetPiece(move.To, movingPiece);

                // Check for promotion
                if (movingPiece.Type != PieceType.King)
                {
                    if ((movingPiece.Color == PieceColor.Red && move.To.Row == 0) ||
                        (movingPiece.Color == PieceColor.Black && move.To.Row == 7))
                    {
                        movingPiece.PromoteToKing();
                    }
                }
            }

            return simBoard;
        }

        /// <summary>
        /// Advanced position evaluation based on checkers research and expert strategies
        /// </summary>
        private double GetAdvancedPositionValue(Position pos, Piece piece, PieceColor color)
        {
            double value = 0.0;

            // 1. CENTER CONTROL (Most Important Strategy)
            double centerBonus = CalculateCenterControl(pos);
            value += centerBonus * 0.5;

            // 2. BACK ROW DEFENSE
            if (IsBackRow(pos, color) && piece.Type != PieceType.King)
            {
                value += 0.4;
            }

            // 3. EDGE PENALTY (Critical Discovery!)
            if (IsEdgePosition(pos))
            {
                value -= 0.3;

                if (piece.Type != PieceType.King)
                {
                    value -= 0.2;
                }
            }

            // 4. ADVANCEMENT BONUS
            double advancementValue = color == PieceColor.Red
                ? (7 - pos.Row) * 0.08
                : pos.Row * 0.08;
            value += advancementValue;

            // 5. KING MOBILITY BONUS
            if (piece.Type == PieceType.King)
            {
                value += 0.3;

                if (IsCenterFour(pos))
                {
                    value += 0.4;
                }
            }

            return value;
        }

        private double CalculateCenterControl(Position pos)
        {
            if (IsCenterFour(pos))
            {
                return 0.6;
            }

            if (pos.Col >= 2 && pos.Col <= 5 && pos.Row >= 2 && pos.Row <= 5)
            {
                return 0.4;
            }

            if (pos.Col >= 1 && pos.Col <= 6 && pos.Row >= 1 && pos.Row <= 6)
            {
                return 0.2;
            }

            return 0.0;
        }

        private bool IsCenterFour(Position pos)
        {
            return (pos.Row == 3 || pos.Row == 4) && (pos.Col == 3 || pos.Col == 4);
        }

        private bool IsEdgePosition(Position pos)
        {
            return pos.Col == 0 || pos.Col == 7 || pos.Row == 0 || pos.Row == 7;
        }

        private bool IsBackRow(Position pos, PieceColor color)
        {
            return (color == PieceColor.Red && pos.Row == 7) ||
                   (color == PieceColor.Black && pos.Row == 0);
        }

        private bool IsDoubleCorner(Position pos, PieceColor color)
        {
            if (color == PieceColor.Red)
            {
                return (pos.Row >= 5 && pos.Col >= 5);
            }
            else
            {
                return (pos.Row <= 2 && pos.Col <= 2);
            }
        }

        private double CalculateStrategicValue(Board board, Move move, PieceColor color)
        {
            double value = 0.0;

            // 1. JUMP BONUSES
            if (move.IsJump)
            {
                value += move.JumpedPositions.Count * 3.0;

                if (move.JumpedPositions.Count > 1)
                {
                    value += move.JumpedPositions.Count * 1.0;
                }

                foreach (var jumpedPos in move.JumpedPositions)
                {
                    var jumpedPiece = board.GetPiece(jumpedPos);
                    if (jumpedPiece != null && jumpedPiece.Type == PieceType.King)
                    {
                        value += 2.0;
                    }
                }
            }

            // 2. PROMOTION BONUS
            Piece movingPiece = board.GetPiece(move.From);
            if (movingPiece != null && movingPiece.Type != PieceType.King)
            {
                if ((color == PieceColor.Red && move.To.Row == 0) ||
                    (color == PieceColor.Black && move.To.Row == 7))
                {
                    value += 2.5;
                }
            }

            // 3. CENTER MOVE BONUS
            if (IsCenterFour(move.To))
            {
                value += 0.8;
            }
            else if (move.To.Col >= 2 && move.To.Col <= 5 &&
                     move.To.Row >= 2 && move.To.Row <= 5)
            {
                value += 0.5;
            }

            // 4. EDGE AVOIDANCE
            if (IsEdgePosition(move.To))
            {
                value -= 0.5;

                if (!move.IsJump)
                {
                    value -= 0.3;
                }
            }

            // 5. BACK ROW PRESERVATION
            if (movingPiece != null && IsBackRow(move.From, color))
            {
                if (!move.IsJump && movingPiece.Type != PieceType.King)
                {
                    value -= 0.4;
                }
            }

            // 6. FORMATION MAINTENANCE
            if (CreatesStrongFormation(board, move, color))
            {
                value += 0.4;
            }

            // 7. TEMPO ADVANTAGE
            if (!IsEdgePosition(move.To))
            {
                int advancement = color == PieceColor.Red
                    ? move.From.Row - move.To.Row
                    : move.To.Row - move.From.Row;

                if (advancement > 0)
                {
                    value += advancement * 0.15;
                }
            }

            return value;
        }

        private bool CreatesStrongFormation(Board board, Move move, PieceColor color)
        {
            int[] directions = { -1, 1 };
            int friendlyNeighbors = 0;

            foreach (int rowDir in directions)
            {
                foreach (int colDir in directions)
                {
                    var neighborPos = new Position(move.To.Row + rowDir, move.To.Col + colDir);
                    if (board.IsValidPosition(neighborPos))
                    {
                        var neighbor = board.GetPiece(neighborPos);
                        if (neighbor != null && neighbor.Color == color && neighbor.Position != move.From)
                        {
                            friendlyNeighbors++;
                        }
                    }
                }
            }

            return friendlyNeighbors >= 2;
        }

        public void UpdateGameResult(GameResult result, int piecesRemaining, int opponentPiecesRemaining)
        {
            Stats.GamesPlayed++;

            switch (result)
            {
                case GameResult.Win:
                    Stats.Wins++;
                    Stats.PiecesCaptured += 12 - opponentPiecesRemaining;
                    break;
                case GameResult.Loss:
                    Stats.Losses++;
                    Stats.PiecesLost += 12 - piecesRemaining;
                    break;
                case GameResult.Draw:
                    Stats.Draws++;
                    break;
            }
        }

        public void CalculateFitness()
        {
            double fitness = 0.0;

            fitness += Stats.Wins * 150.0;
            fitness -= Stats.Losses * 60.0;
            fitness += Stats.Draws * 20.0;

            double captureRatio = Stats.TotalMoves > 0
                ? (double)Stats.PiecesCaptured / Stats.TotalMoves
                : 0.0;
            fitness += captureRatio * 60.0;

            double survivalRate = Stats.GamesPlayed > 0
                ? 1.0 - ((double)Stats.PiecesLost / (Stats.GamesPlayed * 12))
                : 0.0;
            fitness += survivalRate * 40.0;

            fitness += Stats.KingsMade * 20.0;
            fitness += Stats.KingsCaptured * 25.0;
            fitness -= Stats.KingsLost * 30.0;

            Brain.Fitness = Math.Max(0, fitness);
        }

        public void ClearCache()
        {
            evaluationCache.Clear();
        }

        public AIPlayer Clone()
        {
            return new AIPlayer(Brain.Clone());
        }

        public void Mutate(double mutationRate)
        {
            Brain.Mutate(mutationRate);
        }

        public AIPlayer Crossover(AIPlayer partner, Random random)
        {
            DeepNeuralNetwork childBrain = Brain.Crossover(partner.Brain);
            return new AIPlayer(childBrain);
        }
    }

    public class PlayerStats
    {
        public int GamesPlayed { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
        public int TotalMoves { get; set; }
        public int PiecesCaptured { get; set; }
        public int PiecesLost { get; set; }
        public int KingsMade { get; set; }
        public int KingsCaptured { get; set; }
        public int KingsLost { get; set; }

        public double WinRate => GamesPlayed > 0 ? (double)Wins / GamesPlayed : 0.0;
    }
}