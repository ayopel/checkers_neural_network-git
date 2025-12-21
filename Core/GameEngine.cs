using System.Collections.Generic;
using System.Linq;

namespace checkers_neural_network
{
    public class GameEngine
    {
        public Board Board { get; private set; }
        public GameState State { get; private set; }
        public int MoveCount { get; private set; }

        private MoveValidator validator;
        private Piece selectedPiece;
        private bool mustContinueJumping;
        private readonly Dictionary<string, int> stateHistory;
        private readonly Stack<GameSnapshot> moveHistory;
        private int movesWithoutCapture;
        private const int MaxMovesWithoutCapture = 50;
        private const int DrawByRepetitionCount = 3;

        public GameEngine()
        {
            Board = new Board();
            validator = new MoveValidator(Board);
            State = GameState.RedTurn;
            stateHistory = new Dictionary<string, int>();
            moveHistory = new Stack<GameSnapshot>();
            MoveCount = 0;
            movesWithoutCapture = 0;
        }

        public GameEngine(Board board)
        {
            Board = board;
            validator = new MoveValidator(Board);
            State = GameState.RedTurn;
            stateHistory = new Dictionary<string, int>();
            moveHistory = new Stack<GameSnapshot>();
            MoveCount = 0;
            movesWithoutCapture = 0;
        }

        public bool SelectPiece(Position pos)
        {
            if (mustContinueJumping && selectedPiece != null)
                return selectedPiece.Position == pos;

            var piece = Board.GetPiece(pos);
            if (piece == null || piece.Color != GetCurrentTurnColor())
                return false;

            selectedPiece = piece;
            return true;
        }

        public bool MovePiece(Position to)
        {
            if (selectedPiece == null) return false;

            var validMoves = validator.GetValidMoves(selectedPiece);
            var selectedMove = validMoves.FirstOrDefault(m => m.To == to);

            if (selectedMove == null) return false;

            ExecuteMove(selectedMove);
            return true;
        }

        private void ExecuteMove(Move move)
        {
            SaveGameState();

            bool wasJump = move.IsJump;
            Board.ApplyMove(move);
            validator.ClearCache();

            MoveCount++;

            // Track moves without capture for draw detection
            if (wasJump)
            {
                movesWithoutCapture = 0;
            }
            else
            {
                movesWithoutCapture++;
            }

            if (wasJump)
            {
                // Update selected piece reference after move
                selectedPiece = Board.GetPiece(move.To);

                if (selectedPiece != null)
                {
                    var additionalJumps = validator.GetValidJumps(selectedPiece);
                    if (additionalJumps.Count > 0)
                    {
                        mustContinueJumping = true;
                        return;
                    }
                }
            }

            FinishTurn();
        }

        private void FinishTurn()
        {
            mustContinueJumping = false;
            selectedPiece = null;

            UpdateStateHistory();
            SwitchTurn();
            CheckWinCondition();
        }

        private void UpdateStateHistory()
        {
            string boardState = Board.GetStateString();
            if (!stateHistory.ContainsKey(boardState))
                stateHistory[boardState] = 0;
            stateHistory[boardState]++;
        }

        private void SaveGameState()
        {
            var snapshot = new GameSnapshot
            {
                BoardState = Board.Clone(),
                GameState = State,
                MoveCount = MoveCount,
                MovesWithoutCapture = movesWithoutCapture,
                StateHistory = new Dictionary<string, int>(stateHistory)
            };
            moveHistory.Push(snapshot);
        }

        public bool CanUndo() => moveHistory.Count > 0;

        public bool UndoMove()
        {
            if (!CanUndo()) return false;

            var snapshot = moveHistory.Pop();
            Board = snapshot.BoardState.Clone();
            validator = new MoveValidator(Board);
            State = snapshot.GameState;
            MoveCount = snapshot.MoveCount;
            movesWithoutCapture = snapshot.MovesWithoutCapture;

            stateHistory.Clear();
            foreach (var kvp in snapshot.StateHistory)
                stateHistory[kvp.Key] = kvp.Value;

            selectedPiece = null;
            mustContinueJumping = false;

            return true;
        }

        private void SwitchTurn()
        {
            State = State == GameState.RedTurn ? GameState.BlackTurn : GameState.RedTurn;
        }

        private void CheckWinCondition()
        {
            var currentColor = GetCurrentTurnColor();
            var pieces = Board.GetAllPieces(currentColor);

            // No pieces left - opponent wins
            if (pieces.Count == 0)
            {
                State = currentColor == PieceColor.Red ? GameState.BlackWins : GameState.RedWins;
                return;
            }

            // No valid moves - opponent wins
            if (!HasAnyValidMoves(currentColor))
            {
                State = currentColor == PieceColor.Red ? GameState.BlackWins : GameState.RedWins;
                return;
            }

            // Check for draw by repetition
            if (stateHistory.Values.Any(count => count >= DrawByRepetitionCount))
            {
                // Draw is treated as the current player losing (or could add GameState.Draw)
                // For now, we'll let the game continue but training system detects this
            }

            // Check for draw by 50-move rule (no captures)
            if (movesWithoutCapture >= MaxMovesWithoutCapture)
            {
                // Similar to above - training system handles this
            }
        }

        private bool HasAnyValidMoves(PieceColor color)
        {
            var pieces = Board.GetAllPieces(color);
            return pieces.Any(piece => validator.GetValidMoves(piece).Count > 0);
        }

        public List<Position> GetValidMovePositions()
        {
            if (selectedPiece == null) return new List<Position>();
            return validator.GetValidMoves(selectedPiece).Select(m => m.To).ToList();
        }

        public List<Move> GetAllValidMovesForCurrentPlayer()
        {
            return GetAllValidMoves(GetCurrentTurnColor());
        }

        public List<Move> GetAllValidMoves(PieceColor color)
        {
            var allMoves = new List<Move>();
            var pieces = Board.GetAllPieces(color);

            foreach (var piece in pieces)
                allMoves.AddRange(validator.GetValidMoves(piece));

            return allMoves;
        }

        public Piece GetSelectedPiece() => selectedPiece;

        public void DeselectPiece()
        {
            if (!mustContinueJumping)
                selectedPiece = null;
        }

        public void ResetGame()
        {
            Board = new Board();
            validator = new MoveValidator(Board);
            State = GameState.RedTurn;
            MoveCount = 0;
            movesWithoutCapture = 0;
            stateHistory.Clear();
            moveHistory.Clear();
            selectedPiece = null;
            mustContinueJumping = false;
        }

        public bool IsGameOver() =>
            State == GameState.RedWins || State == GameState.BlackWins;

        public bool IsDraw()
        {
            // Check for draw conditions
            if (stateHistory.Values.Any(count => count >= DrawByRepetitionCount))
                return true;

            if (movesWithoutCapture >= MaxMovesWithoutCapture)
                return true;

            return false;
        }

        public PieceColor? GetWinner()
        {
            if (State == GameState.RedWins) return PieceColor.Red;
            if (State == GameState.BlackWins) return PieceColor.Black;
            return null;
        }

        public PieceColor GetCurrentTurnColor() =>
            State == GameState.RedTurn ? PieceColor.Red : PieceColor.Black;

        public GameStats GetGameStats()
        {
            return new GameStats
            {
                MoveCount = MoveCount,
                RedPieces = Board.GetAllPieces(PieceColor.Red).Count,
                BlackPieces = Board.GetAllPieces(PieceColor.Black).Count,
                RedKings = Board.GetAllPieces(PieceColor.Red).Count(p => p.Type == PieceType.King),
                BlackKings = Board.GetAllPieces(PieceColor.Black).Count(p => p.Type == PieceType.King),
                MovesWithoutCapture = movesWithoutCapture
            };
        }

        public double EvaluatePosition(PieceColor forColor)
        {
            var ourPieces = Board.GetAllPieces(forColor);
            var opponentColor = forColor == PieceColor.Red ? PieceColor.Black : PieceColor.Red;
            var theirPieces = Board.GetAllPieces(opponentColor);

            double score = 0;

            foreach (var piece in ourPieces)
                score += piece.Type == PieceType.King ? 3.0 : 1.0;

            foreach (var piece in theirPieces)
                score -= piece.Type == PieceType.King ? 3.0 : 1.0;

            int ourMobility = GetAllValidMoves(forColor).Count;
            int theirMobility = GetAllValidMoves(opponentColor).Count;
            score += (ourMobility - theirMobility) * 0.1;

            return score;
        }
    }

    // ==================== SNAPSHOT & STATS ====================
    public class GameSnapshot
    {
        public Board BoardState { get; set; }
        public GameState GameState { get; set; }
        public int MoveCount { get; set; }
        public int MovesWithoutCapture { get; set; }
        public Dictionary<string, int> StateHistory { get; set; }
    }

    public class GameStats
    {
        public int MoveCount { get; set; }
        public int RedPieces { get; set; }
        public int BlackPieces { get; set; }
        public int RedKings { get; set; }
        public int BlackKings { get; set; }
        public int MovesWithoutCapture { get; set; }
    }
}