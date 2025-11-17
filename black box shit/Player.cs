using System;
using System.Collections.Generic;
using System.Linq;

namespace checkersclaude
{
    public class Player
    {
        public NeuralNetwork Brain { get; private set; }
        public double Fitness { get; set; }

        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
        public int TotalMoves { get; set; }
        public int PiecesCaptured { get; set; }
        public int KingsCaptured { get; set; }
        public int KingsLost { get; set; }
        public int KingsMade { get; set; }
        public int PiecesLost { get; set; }
        public double AverageMoveQuality { get; set; }

        private const int InputSize = 32;
        private const int HiddenSize = 64; // Increased for better learning
        private const int OutputSize = 1;

        public Player(Random random = null)
        {
            Brain = new NeuralNetwork(InputSize, HiddenSize, OutputSize, random);
            ResetStats();
        }

        public Player(NeuralNetwork brain)
        {
            Brain = brain;
            ResetStats();
        }

        public void ResetStats()
        {
            Fitness = 0;
            Wins = 0;
            Losses = 0;
            Draws = 0;
            TotalMoves = 0;
            PiecesCaptured = 0;
            PiecesLost = 0;
            KingsCaptured = 0;
            KingsLost = 0;
            KingsMade = 0;
            AverageMoveQuality = 0;
        }

        public Move ChooseMove(Board board, List<Move> validMoves, PieceColor color)
        {
            if (validMoves == null || validMoves.Count == 0)
                return null;

            if (validMoves.Count == 1)
                return validMoves[0];

            // Evaluate each move with enhanced scoring
            double bestScore = double.MinValue;
            Move bestMove = validMoves[0];
            double totalScore = 0;

            foreach (Move move in validMoves)
            {
                double score = EvaluateMove(board, move, color);
                totalScore += score;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
            }

            // Track average move quality for fitness calculation
            AverageMoveQuality += totalScore / validMoves.Count;
            TotalMoves++;

            // Track statistics
            if (bestMove.IsJump)
            {
                PiecesCaptured += bestMove.JumpedPositions.Count;

                // Check if we captured any kings
                foreach (var jumpedPos in bestMove.JumpedPositions)
                {
                    Piece jumpedPiece = board.GetPiece(jumpedPos);
                    if (jumpedPiece != null && jumpedPiece.Type == PieceType.King)
                        KingsCaptured++;
                }
            }

            // Check if this move creates a king
            Piece movingPiece = board.GetPiece(bestMove.From);
            if (movingPiece != null && movingPiece.Type != PieceType.King)
            {
                bool willBeKing = (movingPiece.Color == PieceColor.Red && bestMove.To.Row == 0) ||
                                 (movingPiece.Color == PieceColor.Black && bestMove.To.Row == 7);
                if (willBeKing)
                    KingsMade++;
            }

            return bestMove;
        }

        private double EvaluateMove(Board board, Move move, PieceColor color)
        {
            // Get board state after move
            double[] boardState = GetBoardStateAfterMove(board, move, color);

            // Get neural network evaluation
            double[] output = Brain.FeedForward(boardState);
            double neuralScore = output[0];

            // Add heuristic bonuses for better decision making
            double heuristicScore = CalculateHeuristicScore(board, move, color);

            // Combine neural network and heuristic (70% neural, 30% heuristic)
            return neuralScore * 0.7 + heuristicScore * 0.3;
        }

        private double CalculateHeuristicScore(Board board, Move move, PieceColor color)
        {
            double score = 0;
            Piece movingPiece = board.GetPiece(move.From);

            if (movingPiece == null)
                return 0;

            // Bonus for captures
            if (move.IsJump)
            {
                score += move.JumpedPositions.Count * 5.0;

                // Extra bonus for capturing kings
                foreach (var jumpedPos in move.JumpedPositions)
                {
                    Piece jumpedPiece = board.GetPiece(jumpedPos);
                    if (jumpedPiece != null && jumpedPiece.Type == PieceType.King)
                        score += 3.0;
                }
            }

            // Bonus for making a king
            bool willBeKing = (movingPiece.Color == PieceColor.Red && move.To.Row == 0) ||
                             (movingPiece.Color == PieceColor.Black && move.To.Row == 7);
            if (willBeKing && movingPiece.Type != PieceType.King)
                score += 4.0;

            // Bonus for advancing pieces (except kings)
            if (movingPiece.Type != PieceType.King)
            {
                int advancement = movingPiece.Color == PieceColor.Red ?
                    (move.From.Row - move.To.Row) : (move.To.Row - move.From.Row);
                score += advancement * 0.5;
            }

            // Bonus for controlling center
            double centerDistance = Math.Abs(move.To.Row - 3.5) + Math.Abs(move.To.Col - 3.5);
            score += (7 - centerDistance) * 0.3;

            // Penalty for moving to edges (can get trapped)
            if (move.To.Col == 0 || move.To.Col == 7)
                score -= 0.5;

            // Bonus for king mobility
            if (movingPiece.Type == PieceType.King)
                score += 1.0;

            return score;
        }

        private double[] GetBoardStateAfterMove(Board board, Move move, PieceColor color)
        {
            double[] state = new double[InputSize];
            int index = 0;

            Piece movingPiece = board.GetPiece(move.From);
            if (movingPiece == null)
                return state;

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    if ((row + col) % 2 == 0) continue; // Skip light squares

                    Position pos = new Position(row, col);
                    Piece piece = board.GetPiece(pos);

                    // Simulate the move
                    if (pos.Equals(move.From))
                        piece = null;
                    else if (pos.Equals(move.To))
                        piece = movingPiece;
                    else if (move.IsJump && move.JumpedPositions != null && move.JumpedPositions.Contains(pos))
                        piece = null;

                    // Enhanced encoding: distinguish regular pieces and kings
                    if (piece == null)
                        state[index] = 0;
                    else if (piece.Color == color)
                        state[index] = piece.Type == PieceType.King ? 1.0 : 0.5;
                    else
                        state[index] = piece.Type == PieceType.King ? -1.0 : -0.5;

                    index++;
                }
            }

            return state;
        }

        public void CalculateFitness()
        {
            // Enhanced multi-factor fitness calculation
            double winBonus = Wins * 200;
            double lossPenalty = Losses * 80;
            double drawBonus = Draws * 50;

            double captureBonus = PiecesCaptured * 15;
            double captureKingBonus = KingsCaptured * 40;
            double kingsMadeBonus = KingsMade * 25;

            double lossPiecePenalty = PiecesLost * 15;
            double lostKingPenalty = KingsLost * 40;

            // Efficiency bonuses
            double moveEfficiency = TotalMoves > 0 ? (PiecesCaptured * 150.0 / TotalMoves) : 0;
            double avgQualityBonus = TotalMoves > 0 ? (AverageMoveQuality / TotalMoves) * 20 : 0;

            // Win rate bonus
            int totalGames = Wins + Losses + Draws;
            double winRate = totalGames > 0 ? (double)Wins / totalGames : 0;
            double winRateBonus = winRate * 100;

            // Penalize extremely long games (stalling)
            double movePenalty = TotalMoves > 1000 ? (TotalMoves - 1000) * 0.1 : 0;

            Fitness = winBonus - lossPenalty + drawBonus +
                     captureBonus + captureKingBonus + kingsMadeBonus -
                     lossPiecePenalty - lostKingPenalty +
                     moveEfficiency + avgQualityBonus + winRateBonus - movePenalty;

            // Add small random noise to break ties
            Fitness += (new Random().NextDouble() - 0.5) * 10;

            Fitness = Math.Max(0, Fitness);
        }

        public Player Clone()
        {
            Player clone = new Player(Brain.Clone());
            return clone;
        }

        public void Mutate(double mutationRate)
        {
            Brain.Mutate(mutationRate);
        }

        public Player Crossover(Player partner, Random random)
        {
            NeuralNetwork childBrain = Brain.Crossover(partner.Brain);
            return new Player(childBrain);
        }
    }
}