using checkers_neural_network;
using checkers_neural_network.AI;
using System;
using System.Collections.Generic;

namespace checkers_neural_network
{
    public class AIPlayer
    {
        #region Properties and Constants

        public DeepNeuralNetwork Brain { get; private set; }
        public PlayerStats Stats { get; private set; }

        private const int InputSize = 64;
        private static readonly int[] HiddenSizes = { 128, 64, 32 };
        private const int OutputSize = 1;

        // Board constants
        private const int BoardSize = 8;
        private const int KingRow_Red = 0;
        private const int KingRow_Black = 7;

        // Pre-calculated constants for performance
        private const double CenterWeight = 0.4;
        private const double AdvancementWeight = 0.3;
        private const double PromotionWeight = 0.5;
        private const double DiagonalWeight = 0.2;
        private const double EdgePenalty = 0.15;
        private const double BackRowBonus = 0.2;
        private const double MobilityBonus = 0.2;
        private const double ProtectionBonus = 0.3;

        // Reusable validator to avoid repeated allocations
        private MoveValidator _cachedValidator;
        private Board _cachedValidatorBoard;

        #endregion

        #region Constructors

        public AIPlayer(Random random = null)
        {
            Brain = new DeepNeuralNetwork(InputSize, HiddenSizes, OutputSize, random);
            Stats = new PlayerStats();
        }

        public AIPlayer(DeepNeuralNetwork brain)
        {
            Brain = brain;
            Stats = new PlayerStats();
        }

        #endregion

        #region Move Selection

        public Move ChooseMove(Board board, List<Move> validMoves, PieceColor color)
        {
            if (validMoves == null || validMoves.Count == 0) return null;
            if (validMoves.Count == 1) return validMoves[0];

            double bestScore = double.MinValue;
            Move bestMove = validMoves[0];

            // Simulate board once and reuse for all move evaluations
            foreach (Move move in validMoves)
            {
                double score = EvaluateMove(board, move, color);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
            }

            Stats.IncrementTotalMoves();
            return bestMove;
        }

        private double EvaluateMove(Board board, Move move, PieceColor color)
        {
            // Simulate the move once and reuse the result
            Board simBoard = SimulateMove(board, move);

            // Get neural network evaluation
            double[] boardState = GetBoardState(simBoard, color);
            double neuralScore = Brain.FeedForward(boardState)[0];

            // Get strategic and tactical bonuses using the same simulated board
            double strategicScore = EvaluateStrategy(board, simBoard, move, color);
            double tacticalScore = EvaluateTactics(simBoard, color);

            return neuralScore + strategicScore * 0.15 + tacticalScore * 0.1;
        }

        #endregion

        #region Board State Evaluation

        private double[] GetBoardState(Board board, PieceColor color)
        {
            double[] state = new double[InputSize];

            int index = 0;
            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    Position pos = new Position(row, col);
                    Piece piece = board.GetPiece(pos);

                    if (piece == null)
                    {
                        state[index++] = 0.0;
                    }
                    else
                    {
                        bool isOurs = piece.Color == color;
                        double value = EvaluatePieceValue(piece, pos, board);
                        state[index++] = isOurs ? value : -value;
                    }
                }
            }

            return state;
        }

        private double EvaluatePieceValue(Piece piece, Position pos, Board board)
        {
            // Base value
            double value = piece.Type == PieceType.King ? 3.0 : 1.0;

            // Positional value
            value += GetPositionValue(pos, piece, piece.Color);

            // Mobility bonus - use cached validator
            if (HasValidMoves(board, piece))
                value += MobilityBonus;

            // Protection bonus
            if (IsProtected(board, pos, piece.Color))
                value += ProtectionBonus;

            return value;
        }

        #endregion

        #region Position Evaluation

        private double GetPositionValue(Position pos, Piece piece, PieceColor color)
        {
            double value = 0.0;

            // Center control (smooth gradient from edges to center)
            value += GetCenterControlValue(pos) * CenterWeight;

            // Forward advancement
            value += GetAdvancementValue(pos, piece, color) * AdvancementWeight;

            // Promotion proximity (exponential for urgency)
            if (piece.Type != PieceType.King)
                value += GetPromotionProximity(pos, color) * PromotionWeight;

            // Edge penalty (pieces on edges are vulnerable)
            if (pos.Col == 0 || pos.Col == BoardSize - 1)
                value -= EdgePenalty;

            // Back row defense
            if (IsBackRow(pos, color))
                value += BackRowBonus;

            // Diagonal control
            value += GetDiagonalValue(pos) * DiagonalWeight;

            return value;
        }

        // Center control: 1.0 at center, 0.0 at corners
        private double GetCenterControlValue(Position pos)
        {
            double distFromCenter = Math.Sqrt(Math.Pow(pos.Row - 3.5, 2) + Math.Pow(pos.Col - 3.5, 2));
            return Math.Max(0, 1.0 - distFromCenter / 5.0);
        }

        // Advancement: how far the piece has progressed
        private double GetAdvancementValue(Position pos, Piece piece, PieceColor color)
        {
            if (piece.Type == PieceType.King) return 0.0;
            return color == PieceColor.Red ? (BoardSize - 1 - pos.Row) / 7.0 : pos.Row / 7.0;
        }

        // Promotion proximity: exponential (urgent near king row)
        private double GetPromotionProximity(Position pos, PieceColor color)
        {
            int rowsToKing = color == PieceColor.Red ? pos.Row : (BoardSize - 1) - pos.Row;
            double progress = ((BoardSize - 1.0) - rowsToKing) / (BoardSize - 1.0);
            return progress * progress; // Exponential
        }

        // Diagonal control: main diagonals are strategic
        private double GetDiagonalValue(Position pos)
        {
            double value = 0.0;
            if (pos.Row == pos.Col) value += 0.3;
            if (pos.Row + pos.Col == BoardSize - 1) value += 0.3;
            return value;
        }

        // Check if position is back row
        private bool IsBackRow(Position pos, PieceColor color)
        {
            return (color == PieceColor.Red && pos.Row == BoardSize - 1) ||
                   (color == PieceColor.Black && pos.Row == 0);
        }

        #endregion

        #region Strategic Evaluation

        private double EvaluateStrategy(Board originalBoard, Board afterMove, Move move, PieceColor color)
        {
            double value = 0.0;

            // 1. Capture value (most important)
            value += EvaluateCaptureValue(originalBoard, move);

            // 2. Promotion value
            value += EvaluatePromotionValue(originalBoard, move, color);

            // 3. Material balance
            value += GetMaterialBalance(afterMove, color) * 0.5;

            // 4. Mobility advantage
            value += GetMobilityAdvantage(afterMove, color) * 0.3;

            // 5. King safety
            value += EvaluateKingSafety(afterMove, move, color);

            // 6. Tempo (forcing moves)
            if (move.IsJump) value += 0.5;

            // 7. Endgame bonus
            value += EvaluateEndgame(originalBoard, move, color);

            return value;
        }

        private double EvaluateCaptureValue(Board board, Move move)
        {
            if (!move.IsJump) return 0.0;

            double value = 0.0;
            foreach (Position jumpedPos in move.JumpedPositions)
            {
                Piece captured = board.GetPiece(jumpedPos);
                if (captured != null)
                    value += captured.Type == PieceType.King ? 5.0 : 2.0;
            }

            // Multi-jump bonus
            if (move.JumpedPositions.Count > 1)
                value += move.JumpedPositions.Count * 1.5;

            return value;
        }

        private double EvaluatePromotionValue(Board board, Move move, PieceColor color)
        {
            Piece piece = board.GetPiece(move.From);
            if (piece == null || piece.Type == PieceType.King) return 0.0;

            int kingRow = color == PieceColor.Red ? KingRow_Red : KingRow_Black;
            return move.To.Row == kingRow ? 3.0 : 0.0;
        }

        private double EvaluateKingSafety(Board afterMove, Move move, PieceColor color)
        {
            Piece piece = afterMove.GetPiece(move.To);
            if (piece?.Type != PieceType.King) return 0.0;

            return IsPositionThreatened(afterMove, move.To, color) ? -0.8 : 0.0;
        }

        private double EvaluateEndgame(Board board, Move move, PieceColor color)
        {
            PieceColor opponent = color == PieceColor.Red ? PieceColor.Black : PieceColor.Red;
            int totalPieces = board.GetAllPieces(color).Count + board.GetAllPieces(opponent).Count;

            // In endgame, centralization matters more
            return totalPieces <= 6 ? GetCenterControlValue(move.To) * 0.8 : 0.0;
        }

        #endregion

        #region Tactical Evaluation

        private double EvaluateTactics(Board afterMove, PieceColor color)
        {
            MoveValidator validator = GetOrCreateValidator(afterMove);
            PieceColor opponent = color == PieceColor.Red ? PieceColor.Black : PieceColor.Red;

            double value = 0.0;

            // Count threats we create
            foreach (var piece in afterMove.GetAllPieces(color))
            {
                int threats = validator.GetValidJumps(piece).Count;
                value += threats * 0.5;
            }

            // Count threats against us
            foreach (var piece in afterMove.GetAllPieces(opponent))
            {
                int threats = validator.GetValidJumps(piece).Count;
                value -= threats * 0.3;
            }

            return value;
        }

        #endregion

        #region Material & Mobility

        private double GetMaterialBalance(Board board, PieceColor color)
        {
            PieceColor opponent = color == PieceColor.Red ? PieceColor.Black : PieceColor.Red;

            double ourMaterial = CalculateMaterial(board, color);
            double theirMaterial = CalculateMaterial(board, opponent);

            return ourMaterial - theirMaterial;
        }

        private double CalculateMaterial(Board board, PieceColor color)
        {
            double material = 0;
            foreach (var piece in board.GetAllPieces(color))
                material += piece.Type == PieceType.King ? 3.0 : 1.0;
            return material;
        }

        private double GetMobilityAdvantage(Board board, PieceColor color)
        {
            MoveValidator validator = GetOrCreateValidator(board);
            PieceColor opponent = color == PieceColor.Red ? PieceColor.Black : PieceColor.Red;

            int ourMoves = CountMoves(board, validator, color);
            int theirMoves = CountMoves(board, validator, opponent);

            return (ourMoves - theirMoves) * 0.1;
        }

        private int CountMoves(Board board, MoveValidator validator, PieceColor color)
        {
            int count = 0;
            foreach (var piece in board.GetAllPieces(color))
                count += validator.GetValidMoves(piece).Count;
            return count;
        }

        #endregion

        #region Helper Methods

        private MoveValidator GetOrCreateValidator(Board board)
        {
            // Reuse validator if board hasn't changed
            if (_cachedValidator == null || _cachedValidatorBoard != board)
            {
                _cachedValidator = new MoveValidator(board);
                _cachedValidatorBoard = board;
            }
            return _cachedValidator;
        }

        private bool HasValidMoves(Board board, Piece piece)
        {
            MoveValidator validator = GetOrCreateValidator(board);
            return validator.GetValidMoves(piece).Count > 0;
        }

        private bool IsProtected(Board board, Position pos, PieceColor color)
        {
            // Check all 4 diagonal neighbors
            int[] offsets = { -1, 1 };
            foreach (int rowOff in offsets)
            {
                foreach (int colOff in offsets)
                {
                    Position neighbor = new Position(pos.Row + rowOff, pos.Col + colOff);
                    if (board.IsValidPosition(neighbor))
                    {
                        Piece piece = board.GetPiece(neighbor);
                        if (piece != null && piece.Color == color)
                            return true;
                    }
                }
            }
            return false;
        }

        private bool IsPositionThreatened(Board board, Position pos, PieceColor color)
        {
            PieceColor opponent = color == PieceColor.Red ? PieceColor.Black : PieceColor.Red;
            MoveValidator validator = GetOrCreateValidator(board);

            foreach (var enemyPiece in board.GetAllPieces(opponent))
            {
                var jumps = validator.GetValidJumps(enemyPiece);
                foreach (var jump in jumps)
                {
                    if (jump.JumpedPositions.Contains(pos))
                        return true;
                }
            }
            return false;
        }

        private Board SimulateMove(Board board, Move move)
        {
            Board simBoard = board.Clone();
            Piece piece = simBoard.GetPiece(move.From);

            if (piece == null) return simBoard;

            // Remove from original position
            simBoard.RemovePiece(move.From);

            // Remove jumped pieces
            if (move.IsJump && move.JumpedPositions != null)
            {
                foreach (Position jumped in move.JumpedPositions)
                    simBoard.RemovePiece(jumped);
            }

            // Place at new position
            simBoard.SetPiece(move.To, piece);

            // Check for promotion
            if (piece.Type != PieceType.King)
            {
                int kingRow = piece.Color == PieceColor.Red ? KingRow_Red : KingRow_Black;
                if (move.To.Row == kingRow)
                    piece.PromoteToKing();
            }

            return simBoard;
        }

        #endregion

        #region Game Result & Fitness

        public void UpdateGameResult(GameResult result, int piecesRemaining, int opponentPiecesRemaining)
        {
            Stats.IncrementGamesPlayed();

            switch (result)
            {
                case GameResult.Win:
                    Stats.IncrementWins();
                    Stats.AddPiecesCaptured(12 - opponentPiecesRemaining);
                    break;
                case GameResult.Loss:
                    Stats.IncrementLosses();
                    Stats.AddPiecesLost(12 - piecesRemaining);
                    break;
                case GameResult.Draw:
                    Stats.IncrementDraws();
                    break;
            }
        }

        public void CalculateFitness()
        {
            double fitness = 0.0;

            // Win/loss record
            fitness += Stats.Wins * 100.0;
            fitness -= Stats.Losses * 50.0;
            fitness += Stats.Draws * 25.0;

            // Capture efficiency
            if (Stats.TotalMoves > 0)
            {
                double captureRatio = (double)Stats.PiecesCaptured / Stats.TotalMoves;
                fitness += captureRatio * 50.0;
            }

            // Survival rate
            if (Stats.GamesPlayed > 0)
            {
                double survivalRate = 1.0 - ((double)Stats.PiecesLost / (Stats.GamesPlayed * 12));
                fitness += survivalRate * 30.0;
            }

            // King management
            fitness += Stats.KingsMade * 15.0;
            fitness += Stats.KingsCaptured * 20.0;
            fitness -= Stats.KingsLost * 25.0;

            Brain.Fitness = Math.Max(0, fitness);
        }

        #endregion

        #region Evolution Methods

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

        #endregion
    }
}