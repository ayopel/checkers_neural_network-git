using System;
using System.Collections.Generic;
using System.Linq;
using checkers_neural_network.AI;

namespace checkers_neural_network
{
    /// <sumary>
    /// רמות קושי למשחק
    /// </summary>
    public enum DifficultyLevel
    {
        Easy,    // רק רשת נוירונים בסיסית
        Medium,  // רשת + בונוסים אסטרטגיים
        Hard,    // רשת + בונוסים + חיפוש עומק 1
        Expert   // רשת + בונוסים + חיפוש עומק 2
    }

    public class AIPlayer
    {
        public DeepNeuralNetwork Brain { get; private set; }
        public PlayerStats Stats { get; private set; }
        public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Hard;

        private const int InputSize = 64;
        private static readonly int[] HiddenSizes = { 128, 64, 32 };
        private const int OutputSize = 1;

        // Cache להערכות מהלכים - שיפור ביצועים משמעותי
        private Dictionary<string, double> evaluationCache = new Dictionary<string, double>();
        private const int MaxCacheSize = 10000;

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

        /// <summary>
        /// בחירת מהלך מתוך רשימת מהלכים תקפים בהתאם לרמת הקושי
        /// </summary>
        public Move ChooseMove(Board board, List<Move> validMoves, PieceColor color)
        {
            if (validMoves == null || validMoves.Count == 0)
                return null;

            if (validMoves.Count == 1)
                return validMoves[0];

            switch (Difficulty)
            {
                case DifficultyLevel.Easy:
                    return ChooseMoveBasic(board, validMoves, color);

                case DifficultyLevel.Medium:
                    return ChooseMoveWithStrategy(board, validMoves, color);

                case DifficultyLevel.Hard:
                    return ChooseMoveWithDepth1(board, validMoves, color);

                case DifficultyLevel.Expert:
                    return ChooseMoveWithDepth2(board, validMoves, color);

                default:
                    return ChooseMoveWithStrategy(board, validMoves, color);
            }
        }

        /// <summary>
        /// מצב Easy - רק רשת נוירונים בסיסית ללא בונוסים
        /// </summary>
        private Move ChooseMoveBasic(Board board, List<Move> validMoves, PieceColor color)
        {
            double bestScore = double.MinValue;
            Move bestMove = validMoves[0];

            foreach (Move move in validMoves)
            {
                double[] boardState = GetBoardStateAfterMove(board, move, color);
                double[] output = Brain.FeedForward(boardState);

                if (output[0] > bestScore)
                {
                    bestScore = output[0];
                    bestMove = move;
                }
            }

            Stats.TotalMoves++;
            return bestMove;
        }

        /// <summary>
        /// מצב Medium/Hard - רשת נוירונים עם בונוסים אסטרטגיים
        /// </summary>
        private Move ChooseMoveWithStrategy(Board board, List<Move> validMoves, PieceColor color)
        {
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

        /// <summary>
        /// מצב Hard - חיפוש עומק 1 (מסתכל גם על תגובת היריב)
        /// </summary>
        private Move ChooseMoveWithDepth1(Board board, List<Move> validMoves, PieceColor color)
        {
            double bestScore = double.MinValue;
            Move bestMove = validMoves[0];
            PieceColor opponentColor = color == PieceColor.Red ? PieceColor.Black : PieceColor.Red;

            foreach (Move move in validMoves)
            {
                Board simBoard = SimulateMove(board, move);
                double myScore = EvaluateMoveSimple(board, move, color);

                // הערכת תגובה טובה ביותר של היריב
                var opponentMoves = GetAllValidMoves(simBoard, opponentColor);

                double worstOpponentScore = double.MaxValue;
                if (opponentMoves.Count > 0)
                {
                    foreach (var oppMove in opponentMoves)
                    {
                        double oppScore = EvaluateMoveSimple(simBoard, oppMove, opponentColor);
                        if (oppScore < worstOpponentScore)
                            worstOpponentScore = oppScore;
                    }
                }
                else
                {
                    worstOpponentScore = 0;
                }

                double totalScore = myScore - worstOpponentScore * 0.5;

                if (totalScore > bestScore)
                {
                    bestScore = totalScore;
                    bestMove = move;
                }
            }

            Stats.TotalMoves++;
            return bestMove;
        }

        /// <summary>
        /// מצב Expert - חיפוש עומק 2
        /// </summary>
        private Move ChooseMoveWithDepth2(Board board, List<Move> validMoves, PieceColor color)
        {
            double bestScore = double.MinValue;
            Move bestMove = validMoves[0];
            PieceColor opponentColor = color == PieceColor.Red ? PieceColor.Black : PieceColor.Red;

            foreach (Move move in validMoves)
            {
                Board simBoard1 = SimulateMove(board, move);
                double myScore = EvaluateMoveSimple(board, move, color);

                var opponentMoves = GetAllValidMoves(simBoard1, opponentColor);
                double bestOpponentScore = double.MinValue;

                foreach (var oppMove in opponentMoves.Take(5)) // מגביל ל-5 מהלכים טובים של היריב
                {
                    Board simBoard2 = SimulateMove(simBoard1, oppMove);
                    double oppScore = EvaluateMoveSimple(simBoard1, oppMove, opponentColor);

                    // התגובה שלנו למהלך של היריב
                    var myResponseMoves = GetAllValidMoves(simBoard2, color);
                    double bestResponseScore = double.MinValue;

                    foreach (var respMove in myResponseMoves.Take(3))
                    {
                        double respScore = EvaluateMoveSimple(simBoard2, respMove, color);
                        if (respScore > bestResponseScore)
                            bestResponseScore = respScore;
                    }

                    double netOpponentScore = oppScore - bestResponseScore * 0.5;
                    if (netOpponentScore > bestOpponentScore)
                        bestOpponentScore = netOpponentScore;
                }

                double totalScore = myScore - bestOpponentScore * 0.7;

                if (totalScore > bestScore)
                {
                    bestScore = totalScore;
                    bestMove = move;
                }
            }

            Stats.TotalMoves++;
            return bestMove;
        }

        /// <summary>
        /// הערכת מהלך עם cache לשיפור ביצועים
        /// </summary>
        public double EvaluateMove(Board board, Move move, PieceColor color)
        {
            string boardKey = board.GetStateString() + move.ToString() + color.ToString();

            if (evaluationCache.TryGetValue(boardKey, out double cachedValue))
                return cachedValue;

            double[] boardState = GetBoardStateAfterMove(board, move, color);
            double[] output = Brain.FeedForward(boardState);

            double strategicBonus = CalculateStrategicValue(board, move, color);

            double result = output[0] + strategicBonus * 0.1;

            // ניהול גודל cache
            if (evaluationCache.Count >= MaxCacheSize)
                evaluationCache.Clear();

            evaluationCache[boardKey] = result;
            return result;
        }

        /// <summary>
        /// הערכה פשוטה ללא cache (לחיפוש עומק)
        /// </summary>
        private double EvaluateMoveSimple(Board board, Move move, PieceColor color)
        {
            double[] boardState = GetBoardStateAfterMove(board, move, color);
            double[] output = Brain.FeedForward(boardState);
            double strategicBonus = CalculateStrategicValue(board, move, color);
            return output[0] + strategicBonus * 0.1;
        }

        /// <summary>
        /// מחזיר את כל המהלכים התקפים עבור צבע מסוים
        /// </summary>
        private List<Move> GetAllValidMoves(Board board, PieceColor color)
        {
            var allMoves = new List<Move>();
            var pieces = board.GetAllPieces(color);
            var validator = new MoveValidator(board);

            foreach (var piece in pieces)
            {
                allMoves.AddRange(validator.GetValidMoves(piece));
            }

            return allMoves;
        }

        /// <summary>
        /// יוצר ייצוג של הלוח אחרי ביצוע מהלך
        /// </summary>
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
                        state[index] = piece.Type == PieceType.King ? 3.0 : 1.0;

                        double positionValue = GetPositionValue(pos, color);
                        state[index] += positionValue * 0.5;
                    }
                    else
                    {
                        state[index] = piece.Type == PieceType.King ? -3.0 : -1.0;

                        double positionValue = GetPositionValue(pos, piece.Color);
                        state[index] -= positionValue * 0.5;
                    }

                    index++;
                }
            }

            return state;
        }

        /// <summary>
        /// מבצע סימולציה של מהלך על לוח משוכפל
        /// </summary>
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
        /// מחשב ערך פוזיציוני - שליטה במרכז והתקדמות
        /// </summary>
        private double GetPositionValue(Position pos, PieceColor color)
        {
            double centerBonus = 0.0;
            if (pos.Col >= 2 && pos.Col <= 5)
                centerBonus = 0.2;
            if (pos.Row >= 2 && pos.Row <= 5)
                centerBonus += 0.2;

            double advancementValue = 0.0;
            if (color == PieceColor.Red)
                advancementValue = (7 - pos.Row) * 0.1;
            else
                advancementValue = pos.Row * 0.1;

            return centerBonus + advancementValue;
        }

        /// <summary>
        /// חישוב בונוס אסטרטגי משופר - כולל קפיצות, הכתרות, איומים
        /// </summary>
        private double CalculateStrategicValue(Board board, Move move, PieceColor color)
        {
            double value = 0.0;

            // בונוס לקפיצות
            if (move.IsJump)
            {
                value += move.JumpedPositions.Count * 2.0;

                // קפיצות מרובות
                if (move.JumpedPositions.Count > 1)
                    value += move.JumpedPositions.Count * 0.5;

                // בונוס לכיבוש מלכים
                foreach (var jumpedPos in move.JumpedPositions)
                {
                    Piece jumpedPiece = board.GetPiece(jumpedPos);
                    if (jumpedPiece != null && jumpedPiece.Type == PieceType.King)
                        value += 2.0;
                }
            }

            // הכתרה ודחיפות הכתרה
            Piece movingPiece = board.GetPiece(move.From);
            if (movingPiece != null && movingPiece.Type != PieceType.King)
            {
                int promotionRow = color == PieceColor.Red ? 0 : 7;

                if (move.To.Row == promotionRow)
                {
                    value += 2.0; // הכתרה מיידית
                }
                else
                {
                    // דחיפות הכתרה - ככל שקרוב יותר, הבונוס גבוה יותר (אקספוננציאלי)
                    int distanceToPromotion = Math.Abs(move.To.Row - promotionRow);
                    double baseDistance = color == PieceColor.Red ? move.From.Row : (7 - move.From.Row);
                    double newDistance = color == PieceColor.Red ? move.To.Row : (7 - move.To.Row);

                    if (newDistance < baseDistance) // התקדמות לכיוון הכתרה
                    {
                        value += Math.Pow(2, 7 - distanceToPromotion) * 0.05;
                    }
                }
            }

            // שליטה במרכז
            if (move.To.Col >= 2 && move.To.Col <= 5 && move.To.Row >= 2 && move.To.Row <= 5)
                value += 0.3;

            // זיהוי איומים - האם המהלך מסכן אותנו?
            try
            {
                Board simBoard = SimulateMove(board, move);
                PieceColor opponentColor = color == PieceColor.Red ? PieceColor.Black : PieceColor.Red;

                MoveValidator validator = new MoveValidator(simBoard);
                var opponentPieces = simBoard.GetAllPieces(opponentColor);

                bool isInDanger = false;
                foreach (var opponentPiece in opponentPieces)
                {
                    var opponentJumps = opponentPiece.Type == PieceType.King ?
                        validator.GetValidKingJumps(opponentPiece) :
                        validator.GetValidJumps(opponentPiece);

                    if (opponentJumps.Any(j => j.JumpedPositions.Contains(move.To)))
                    {
                        isInDanger = true;
                        break;
                    }
                }

                if (isInDanger)
                    value -= 1.5; // קנס על מהלך מסוכן
            }
            catch
            {
                // אם יש שגיאה בבדיקת איומים, התעלם
            }

            return value;
        }

        /// <summary>
        /// עדכון תוצאת משחק - thread safe
        /// </summary>
        public void UpdateGameResult(GameResult result, int piecesRemaining, int opponentPiecesRemaining)
        {
            lock (Stats)
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
        }

        /// <summary>
        /// חישוב ציון כושר מקיף
        /// </summary>
        public void CalculateFitness()
        {
            lock (Stats)
            {
                double fitness = 0.0;

                fitness += Stats.Wins * 100.0;
                fitness -= Stats.Losses * 50.0;
                fitness += Stats.Draws * 25.0;

                double captureRatio = Stats.TotalMoves > 0
                    ? (double)Stats.PiecesCaptured / Stats.TotalMoves
                    : 0.0;
                fitness += captureRatio * 50.0;

                double survivalRate = Stats.GamesPlayed > 0
                    ? 1.0 - ((double)Stats.PiecesLost / (Stats.GamesPlayed * 12))
                    : 0.0;
                fitness += survivalRate * 30.0;

                fitness += Stats.KingsMade * 15.0;
                fitness += Stats.KingsCaptured * 20.0;
                fitness -= Stats.KingsLost * 25.0;

                Brain.Fitness = Math.Max(0, fitness);
            }
        }

        public AIPlayer Clone()
        {
            var cloned = new AIPlayer(Brain.Clone());
            cloned.Difficulty = this.Difficulty;
            return cloned;
        }

        public void Mutate(double mutationRate)
        {
            Brain.Mutate(mutationRate);
        }

        public AIPlayer Crossover(AIPlayer partner, Random random)
        {
            DeepNeuralNetwork childBrain = Brain.Crossover(partner.Brain);
            var child = new AIPlayer(childBrain);
            child.Difficulty = this.Difficulty;
            return child;
        }

        /// <summary>
        /// ניקוי cache - שימושי בין משחקים
        /// </summary>
        public void ClearCache()
        {
            evaluationCache.Clear();
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