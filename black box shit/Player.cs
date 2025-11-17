using System;
using System.Collections.Generic;
using System.Linq;

namespace checkersclaude
{
    public class Player
    {
        public NeuralNetwork Brain { get; private set; }
        public double Fitness { get; set; }
        private static Random rng = new Random();

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
            // 5% random exploration
            if (rng.NextDouble() < 0.05)
                return validMoves[rng.Next(validMoves.Count)];

            return bestMove;
        }

        private double EvaluateMove(Board board, Move move, PieceColor color)
        {
            Board sim = board.Clone();
            sim.ApplyMove(move);

            double[] state = EncodeBoard(sim, color);
            double neuralScore = Brain.FeedForward(state)[0];

            double heuristicScore = CalculateHeuristicScore(board, move, color);

            return neuralScore * 0.9 + heuristicScore * 0.1;
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

        private double[] EncodeBoard(Board board, PieceColor color)
        {
            double[] state = new double[InputSize];
            int index = 0;

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    if ((row + col) % 2 == 0) continue;

                    Piece piece = board.GetPiece(new Position(row, col));

                    if (piece == null)
                        state[index++] = 0;
                    else if (piece.Color == color)
                        state[index++] = piece.Type == PieceType.King ? 1.0 : 0.5;
                    else
                        state[index++] = piece.Type == PieceType.King ? -1.0 : -0.5;
                }
            }

            return state;
        }


        public void CalculateFitness()
        {
            // Base win/loss/draw
            double fitness = 0;
            int totalGames = Wins + Losses + Draws;

            if (totalGames == 0)
            {
                Fitness = 1; // minimal baseline
                return;
            }

            double winRate = (double)Wins / totalGames;
            double lossRate = (double)Losses / totalGames;
            double drawRate = (double)Draws / totalGames;

            // Weighted scoring
            fitness += winRate * 200;             // wins
            fitness -= lossRate * 100;            // losses
            fitness += drawRate * 50;             // draws

            // Piece metrics
            fitness += PiecesCaptured * 10;
            fitness += KingsCaptured * 25;
            fitness += KingsMade * 20;

            fitness -= PiecesLost * 5;
            fitness -= KingsLost * 20;

            // Efficiency
            if (TotalMoves > 0)
            {
                fitness += (PiecesCaptured / (double)TotalMoves) * 50;
                fitness += (AverageMoveQuality / TotalMoves) * 30;
            }

            // Bonus for shorter games (avoiding stalling)
            if (TotalMoves > 0)
                fitness += Math.Max(0, 50 - TotalMoves * 0.05);

            // Add small random noise to break ties
            fitness += (new Random().NextDouble() - 0.5) * 5;

            Fitness = Math.Max(0, fitness);
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
        public Move GetBestMove(Board board)
        {
            PieceColor color = PlayerColor(board);
            List<Piece> pieces = board.GetAllPieces(color);

            List<Move> allMoves = new List<Move>();
            foreach (var piece in pieces)
            {
                allMoves.AddRange(piece.GetValidMoves(board));
            }

            return ChooseMove(board, allMoves, color);
        }

        // Determine player color based on whose turn it is on the board
        private PieceColor PlayerColor(Board board)
        {
            // You can decide: maybe Red is always Player 1
            // Or, if you have a GameEngine reference, use its State
            return PieceColor.Red; // for simplicity
        }

        public Player Crossover(Player partner, Random random)
        {
            NeuralNetwork childBrain = Brain.Crossover(partner.Brain);
            return new Player(childBrain);
        }
    }
}