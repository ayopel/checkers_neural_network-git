using checkers_neural_network;
using checkers_neural_network.AI;
using System;
using System.Collections.Generic;
using System.Linq;

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

        // Strategic weights - tuned for better play
        private const double CenterWeight = 0.5;
        private const double AdvancementWeight = 0.35;
        private const double PromotionWeight = 0.6;
        private const double DiagonalWeight = 0.25;
        private const double EdgePenalty = 0.2;
        private const double BackRowBonus = 0.25;
        private const double MobilityBonus = 0.25;
        private const double ProtectionBonus = 0.35;

        // Evaluation weights
        private const double NeuralWeight = 0.6;
        private const double StrategyWeight = 0.25;
        private const double TacticsWeight = 0.15;

        private readonly Random random;

        #endregion

        #region Constructors

        public AIPlayer(Random random = null)
        {
            this.random = random ?? new Random();
            Brain = new DeepNeuralNetwork(InputSize, HiddenSizes, OutputSize, this.random);
            Stats = new PlayerStats();
        }

        public AIPlayer(DeepNeuralNetwork brain)
        {
            this.random = new Random();
            Brain = brain;
            Stats = new PlayerStats();
        }

        #endregion

        #region Move Selection

        public Move ChooseMove(Board board, List<Move> validMoves, PieceColor color)
        {
            if (validMoves == null || validMoves.Count == 0) return null;
            if (validMoves.Count == 1) return validMoves[0];

            Move bestMove = null;
            double bestScore = double.MinValue;

            // Evaluate all moves
            var scoredMoves = new List<(Move move, double score)>();

            foreach (Move move in validMoves)
            {
                double score = EvaluateMove(board, move, color);
                scoredMoves.Add((move, score));

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
            }

            // Small chance to pick second-best move for exploration during training
            if (scoredMoves.Count > 1 && random.NextDouble() < 0.05)
            {
                var sorted = scoredMoves.OrderByDescending(x => x.score).ToList();
                if (sorted.Count > 1 && sorted[1].score > bestScore * 0.9)
                {
                    bestMove = sorted[1].move;
                }
            }

            Stats.TotalMoves++;
            return bestMove;
        }

        private double EvaluateMove(Board board, Move move, PieceColor color)
        {
            // Get neural network evaluation
            double[] boardState = GetBoardStateAfterMove(board, move, color);
            double neuralScore = Brain.FeedForward(boardState)[0];

            // Normalize neural score to reasonable range
            neuralScore = Math.Tanh(neuralScore);

            // Get strategic and tactical bonuses
            double strategicScore = EvaluateStrategy(board, move, color);
            double tacticalScore = EvaluateTactics(board, move, color);

            // Combine scores with weights
            return neuralScore * NeuralWeight +
                   strategicScore * StrategyWeight +
                   tacticalScore * TacticsWeight;
        }

        #endregion

        #region Board State Evaluation

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
                        state[index++] = 0.0;
                    }
                    else
                    {
                        bool isOurs = piece.Color == color;
                        double value = EvaluatePieceValue(piece, pos, simBoard, color);
                        state[index++] = isOurs ? value : -value;
                    }
                }
            }

            return state;
        }

        private double EvaluatePieceValue(Piece piece, Position pos, Board board, PieceColor perspective)
        {
            // Base value
            double value = piece.Type == PieceType.King ? 3.0 : 1.0;

            // Positional value
            value += GetPositionValue(pos, piece, piece.Color);

            // Mobility bonus
            if (HasValidMoves(board, piece))
                value += MobilityBonus;

            // Protection bonus
            if (IsProtected(board, pos, piece.Color))
                value += ProtectionBonus;

            // Vulnerability penalty
            if (IsVulnerable(board, pos, piece.Color))
                value -= 0.4;

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
            if (pos.Col == 0 || pos.Col == 7)
                value -= EdgePenalty;

            // Corner penalty (corners are worst)
            if ((pos.Col == 0 || pos.Col == 7) && (pos.Row == 0 || pos.Row == 7))
                value -= EdgePenalty * 0.5;

            // Back row defense (only valuable early/mid game for non-kings)
            if (piece.Type != PieceType.King && IsBackRow(pos, color))
                value += BackRowBonus;

            // Diagonal control
            value += GetDiagonalValue(pos) * DiagonalWeight;

            return value;
        }

        private double GetCenterControlValue(Position pos)
        {
            // Center squares (3,3), (3,4), (4,3), (4,4) are most valuable
            double rowDist = Math.Abs(pos.Row - 3.5);
            double colDist = Math.Abs(pos.Col - 3.5);
            double distFromCenter = Math.Sqrt(rowDist * rowDist + colDist * colDist);
            return Math.Max(0, 1.0 - distFromCenter / 4.5);
        }

        private double GetAdvancementValue(Position pos, Piece piece, PieceColor color)
        {
            if (piece.Type == PieceType.King) return 0.0;
            return color == PieceColor.Red ? (7 - pos.Row) / 7.0 : pos.Row / 7.0;
        }

        private double GetPromotionProximity(Position pos, PieceColor color)
        {
            int rowsToKing = color == PieceColor.Red ? pos.Row : 7 - pos.Row;
            double progress = (7.0 - rowsToKing) / 7.0;
            return progress * progress * progress; // Cubic for more urgency near promotion
        }

        private double GetDiagonalValue(Position pos)
        {
            double value = 0.0;
            // Main diagonals are strategic
            if (pos.Row == pos.Col) value += 0.3;
            if (pos.Row + pos.Col == 7) value += 0.3;
            // Secondary diagonals
            if (Math.Abs(pos.Row - pos.Col) == 1) value += 0.15;
            if (Math.Abs(pos.Row + pos.Col - 7) == 1) value += 0.15;
            return value;
        }

        private bool IsBackRow(Position pos, PieceColor color)
        {
            return (color == PieceColor.Red && pos.Row == 7) ||
                   (color == PieceColor.Black && pos.Row == 0);
        }

        #endregion

        #region Strategic Evaluation

        private double EvaluateStrategy(Board board, Move move, PieceColor color)
        {
            double value = 0.0;

            // 1. Capture value (most important)
            value += EvaluateCaptureValue(board, move);

            // 2. Promotion value
            value += EvaluatePromotionValue(board, move, color);

            // 3. Material balance after move
            Board afterMove = SimulateMove(board, move);
            value += GetMaterialBalance(afterMove, color) * 0.4;

            // 4. Mobility advantage
            value += GetMobilityAdvantage(afterMove, color) * 0.25;

            // 5. King safety
            value += EvaluateKingSafety(board, move, color);

            // 6. Position improvement
            value += EvaluatePositionImprovement(board, move, color);

            // 7. Tempo (forcing moves)
            if (move.IsJump) value += 0.6;

            // 8. Formation (keeping pieces together)
            value += EvaluateFormation(afterMove, move, color) * 0.3;

            // 9. Endgame considerations
            value += EvaluateEndgame(board, move, color);

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
                {
                    // Kings are worth more
                    value += captured.Type == PieceType.King ? 5.0 : 2.0;

                    // Bonus for capturing advanced pieces
                    double advancement = captured.Color == PieceColor.Red
                        ? (7 - jumpedPos.Row) / 7.0
                        : jumpedPos.Row / 7.0;
                    value += advancement * 0.5;
                }
            }

            // Multi-jump bonus (exponential for chain captures)
            if (move.JumpedPositions.Count > 1)
                value += Math.Pow(move.JumpedPositions.Count, 1.5);

            return value;
        }

        private double EvaluatePromotionValue(Board board, Move move, PieceColor color)
        {
            Piece piece = board.GetPiece(move.From);
            if (piece == null || piece.Type == PieceType.King) return 0.0;

            bool promotes = (color == PieceColor.Red && move.To.Row == 0) ||
                           (color == PieceColor.Black && move.To.Row == 7);

            if (promotes)
            {
                // Higher value if we have fewer kings
                Board afterMove = SimulateMove(board, move);
                int ourKings = afterMove.GetAllPieces(color).Count(p => p.Type == PieceType.King);
                return ourKings <= 2 ? 4.0 : 3.0;
            }

            return 0.0;
        }

        private double EvaluateKingSafety(Board board, Move move, PieceColor color)
        {
            Piece piece = board.GetPiece(move.From);
            double value = 0.0;

            if (piece?.Type == PieceType.King)
            {
                Board afterMove = SimulateMove(board, move);

                // Penalty for moving king into danger
                if (IsPositionThreatened(afterMove, move.To, color))
                    value -= 1.2;

                // Bonus for moving king out of danger
                if (IsPositionThreatened(board, move.From, color) &&
                    !IsPositionThreatened(afterMove, move.To, color))
                    value += 0.8;
            }
            else if (piece != null)
            {
                // Penalty for leaving our kings undefended
                Board afterMove = SimulateMove(board, move);
                foreach (var king in afterMove.GetAllPieces(color).Where(p => p.Type == PieceType.King))
                {
                    if (IsPositionThreatened(afterMove, king.Position, color))
                        value -= 0.5;
                }
            }

            return value;
        }

        private double EvaluatePositionImprovement(Board board, Move move, PieceColor color)
        {
            Piece piece = board.GetPiece(move.From);
            if (piece == null) return 0.0;

            double fromValue = GetPositionValue(move.From, piece, color);
            double toValue = GetPositionValue(move.To, piece, color);

            return (toValue - fromValue) * 0.5;
        }

        private double EvaluateFormation(Board board, Move move, PieceColor color)
        {
            double value = 0.0;
            var ourPieces = board.GetAllPieces(color);

            // Bonus for staying connected with friendly pieces
            int neighbors = 0;
            foreach (int rowOff in new[] { -1, 1 })
            {
                foreach (int colOff in new[] { -1, 1 })
                {
                    Position neighbor = new Position(move.To.Row + rowOff, move.To.Col + colOff);
                    if (board.IsValidPosition(neighbor))
                    {
                        Piece p = board.GetPiece(neighbor);
                        if (p != null && p.Color == color)
                            neighbors++;
                    }
                }
            }
            value += neighbors * 0.15;

            return value;
        }

        private double EvaluateEndgame(Board board, Move move, PieceColor color)
        {
            PieceColor opponent = color == PieceColor.Red ? PieceColor.Black : PieceColor.Red;
            int ourPieces = board.GetAllPieces(color).Count;
            int theirPieces = board.GetAllPieces(opponent).Count;
            int totalPieces = ourPieces + theirPieces;

            if (totalPieces > 8) return 0.0; // Not endgame yet

            double value = 0.0;

            // In endgame with advantage, push towards opponent
            if (ourPieces > theirPieces)
            {
                // Centralization matters more
                value += GetCenterControlValue(move.To) * 0.8;

                // Chase opponent's pieces
                var enemyPieces = board.GetAllPieces(opponent);
                if (enemyPieces.Count > 0)
                {
                    double minDist = enemyPieces.Min(p =>
                        Math.Abs(p.Position.Row - move.To.Row) +
                        Math.Abs(p.Position.Col - move.To.Col));
                    value += (8 - minDist) * 0.1;
                }
            }
            else if (ourPieces < theirPieces)
            {
                // When losing, prefer edges (harder to corner)
                // But not corners (too easy to trap)
                bool onEdge = move.To.Row == 0 || move.To.Row == 7 ||
                              move.To.Col == 0 || move.To.Col == 7;
                bool inCorner = (move.To.Row == 0 || move.To.Row == 7) &&
                                (move.To.Col == 0 || move.To.Col == 7);

                if (onEdge && !inCorner) value += 0.3;
                if (inCorner) value -= 0.5;
            }

            return value;
        }

        #endregion

        #region Tactical Evaluation

        private double EvaluateTactics(Board board, Move move, PieceColor color)
        {
            Board afterMove = SimulateMove(board, move);
            MoveValidator validator = new MoveValidator(afterMove);
            PieceColor opponent = color == PieceColor.Red ? PieceColor.Black : PieceColor.Red;

            double value = 0.0;

            // Count threats we create (potential captures next turn)
            foreach (var piece in afterMove.GetAllPieces(color))
            {
                var jumps = piece.Type == PieceType.King
                    ? validator.GetValidKingJumps(piece)
                    : validator.GetValidJumps(piece);

                foreach (var jump in jumps)
                {
                    foreach (var jumpedPos in jump.JumpedPositions)
                    {
                        Piece target = afterMove.GetPiece(jumpedPos);
                        if (target != null)
                        {
                            // More value for threatening kings
                            value += target.Type == PieceType.King ? 0.8 : 0.4;
                        }
                    }
                }
            }

            // Count threats against us (danger next turn)
            foreach (var piece in afterMove.GetAllPieces(opponent))
            {
                var jumps = piece.Type == PieceType.King
                    ? validator.GetValidKingJumps(piece)
                    : validator.GetValidJumps(piece);

                foreach (var jump in jumps)
                {
                    foreach (var jumpedPos in jump.JumpedPositions)
                    {
                        Piece target = afterMove.GetPiece(jumpedPos);
                        if (target != null && target.Color == color)
                        {
                            // More penalty for our kings being threatened
                            value -= target.Type == PieceType.King ? 0.6 : 0.3;
                        }
                    }
                }
            }

            // Avoid setting up opponent for multi-jumps
            int maxEnemyChain = GetMaxJumpChain(afterMove, opponent);
            if (maxEnemyChain > 1)
                value -= maxEnemyChain * 0.5;

            return value;
        }

        private int GetMaxJumpChain(Board board, PieceColor color)
        {
            int maxChain = 0;
            MoveValidator validator = new MoveValidator(board);

            foreach (var piece in board.GetAllPieces(color))
            {
                var jumps = piece.Type == PieceType.King
                    ? validator.GetValidKingJumps(piece)
                    : validator.GetValidJumps(piece);

                foreach (var jump in jumps)
                {
                    maxChain = Math.Max(maxChain, jump.JumpedPositions.Count);
                }
            }

            return maxChain;
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
            {
                material += piece.Type == PieceType.King ? 3.0 : 1.0;
            }
            return material;
        }

        private double GetMobilityAdvantage(Board board, PieceColor color)
        {
            MoveValidator validator = new MoveValidator(board);
            PieceColor opponent = color == PieceColor.Red ? PieceColor.Black : PieceColor.Red;

            int ourMoves = CountMoves(board, validator, color);
            int theirMoves = CountMoves(board, validator, opponent);

            // Penalize heavily if we're about to lose (no moves)
            if (ourMoves == 0) return -10.0;
            if (theirMoves == 0) return 5.0;

            return (ourMoves - theirMoves) * 0.15;
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

        private bool HasValidMoves(Board board, Piece piece)
        {
            MoveValidator validator = new MoveValidator(board);
            return validator.GetValidMoves(piece).Count > 0;
        }

        private bool IsProtected(Board board, Position pos, PieceColor color)
        {
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

        private bool IsVulnerable(Board board, Position pos, PieceColor color)
        {
            PieceColor opponent = color == PieceColor.Red ? PieceColor.Black : PieceColor.Red;

            // Check if any enemy can jump us
            foreach (int rowDir in new[] { -1, 1 })
            {
                foreach (int colDir in new[] { -1, 1 })
                {
                    // Enemy position that could jump us
                    Position enemyPos = new Position(pos.Row - rowDir, pos.Col - colDir);
                    // Landing position for that jump
                    Position landingPos = new Position(pos.Row + rowDir, pos.Col + colDir);

                    if (board.IsValidPosition(enemyPos) && board.IsValidPosition(landingPos))
                    {
                        Piece enemy = board.GetPiece(enemyPos);
                        Piece landing = board.GetPiece(landingPos);

                        if (enemy != null && enemy.Color == opponent && landing == null)
                        {
                            // Check if enemy can move in this direction
                            if (enemy.Type == PieceType.King ||
                                (enemy.Color == PieceColor.Red && rowDir > 0) ||
                                (enemy.Color == PieceColor.Black && rowDir < 0))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private bool IsPositionThreatened(Board board, Position pos, PieceColor color)
        {
            PieceColor opponent = color == PieceColor.Red ? PieceColor.Black : PieceColor.Red;
            MoveValidator validator = new MoveValidator(board);

            foreach (var enemyPiece in board.GetAllPieces(opponent))
            {
                var jumps = enemyPiece.Type == PieceType.King
                    ? validator.GetValidKingJumps(enemyPiece)
                    : validator.GetValidJumps(enemyPiece);

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

            simBoard.RemovePiece(move.From);

            if (move.IsJump && move.JumpedPositions != null)
            {
                foreach (Position jumped in move.JumpedPositions)
                    simBoard.RemovePiece(jumped);
            }

            simBoard.SetPiece(move.To, piece);

            if (piece.Type != PieceType.King)
            {
                bool shouldPromote = (piece.Color == PieceColor.Red && move.To.Row == 0) ||
                                    (piece.Color == PieceColor.Black && move.To.Row == 7);
                if (shouldPromote)
                    piece.PromoteToKing();
            }

            return simBoard;
        }

        #endregion

        #region Game Result & Fitness

        public void UpdateGameResult(GameResult result, int piecesRemaining, int opponentPiecesRemaining)
        {
            Stats.GamesPlayed++;

            switch (result)
            {
                case GameResult.Win:
                    Stats.Wins++;
                    // FIX: Don't add to PiecesCaptured here - it's tracked during gameplay
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

            if (Stats.GamesPlayed == 0)
            {
                Brain.Fitness = 0;
                return;
            }

            // === Win/Loss Record (Primary Factor) ===
            // Wins are very valuable
            fitness += Stats.Wins * 100.0;
            // Losses hurt but not as much
            fitness -= Stats.Losses * 40.0;
            // Draws are okay - better than losing
            fitness += Stats.Draws * 30.0;

            // === Win Rate Bonus (Rewards Consistency) ===
            double winRate = Stats.WinRate;
            if (winRate > 0.6)
            {
                // Bonus for high win rate
                fitness += (winRate - 0.5) * 200.0;
            }

            // === Capture Efficiency ===
            if (Stats.TotalMoves > 0)
            {
                double captureRatio = (double)Stats.PiecesCaptured / Stats.TotalMoves;
                fitness += captureRatio * 40.0;

                // Bonus for high capture games
                double capturesPerGame = (double)Stats.PiecesCaptured / Stats.GamesPlayed;
                if (capturesPerGame > 6)
                {
                    fitness += (capturesPerGame - 6) * 10.0;
                }
            }

            // === Survival Rate ===
            if (Stats.GamesPlayed > 0)
            {
                double maxPossibleLost = Stats.GamesPlayed * 12.0;
                double survivalRate = 1.0 - (Stats.PiecesLost / maxPossibleLost);
                fitness += survivalRate * 25.0;
            }

            // === King Management ===
            fitness += Stats.KingsMade * 12.0;
            fitness += Stats.KingsCaptured * 18.0;
            fitness -= Stats.KingsLost * 20.0;

            // === Dominance Bonus ===
            // Extra reward for winning by large margins
            if (Stats.Wins > 0)
            {
                double avgPiecesKept = 12.0 - ((double)Stats.PiecesLost / Stats.GamesPlayed);
                if (avgPiecesKept > 8)
                {
                    fitness += (avgPiecesKept - 8) * 15.0;
                }
            }

            Brain.Fitness = Math.Max(0, fitness);
        }

        #endregion

        #region Evolution Methods

        public AIPlayer Clone()
        {
            var clone = new AIPlayer(Brain.Clone());
            return clone;
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

    #region Player Stats

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
        public double DrawRate => GamesPlayed > 0 ? (double)Draws / GamesPlayed : 0.0;
        public double LossRate => GamesPlayed > 0 ? (double)Losses / GamesPlayed : 0.0;
    }

    #endregion
}