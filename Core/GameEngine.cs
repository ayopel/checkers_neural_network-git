using System.Collections.Generic;
using System.Linq;

namespace checkersclaude
{
    public class GameEngine
    {
        public Board Board { get; private set; }
        public GameState State { get; private set; }

        private readonly MoveValidator validator;
        private Piece selectedPiece;
        private bool mustContinueJumping;
        private readonly Dictionary<string, int> stateHistory;
        private int movesWithoutCapture;

        public GameEngine()
        {
            Board = new Board();
            validator = new MoveValidator(Board);
            State = GameState.RedTurn;
            stateHistory = new Dictionary<string, int>();
            ResetTurnState();
        }

        public GameEngine(Board board)
        {
            Board = board;
            validator = new MoveValidator(Board);
            State = GameState.RedTurn;
            stateHistory = new Dictionary<string, int>();
            ResetTurnState();
        }

        private void ResetTurnState()
        {
            selectedPiece = null;
            mustContinueJumping = false;
            movesWithoutCapture = 0;
        }

        public bool SelectPiece(Position pos)
        {
            if (mustContinueJumping && selectedPiece != null)
                return selectedPiece.Position == pos;

            var piece = Board.GetPiece(pos);
            if (piece == null) return false;

            var currentColor = State == GameState.RedTurn ? PieceColor.Red : PieceColor.Black;
            if (piece.Color != currentColor) return false;

            selectedPiece = piece;
            return true;
        }

        public bool MovePiece(Position to)
        {
            if (selectedPiece == null) return false;

            var validMoves = validator.GetValidMoves(selectedPiece);
            var selectedMove = validMoves.FirstOrDefault(m => m.To == to);

            if (selectedMove == null) return false;

            bool wasJump = selectedMove.IsJump;

            Board.ApplyMove(selectedMove);

            if (wasJump)
            {
                movesWithoutCapture = 0;

                var additionalJumps = validator.GetValidJumps(selectedPiece);
                if (additionalJumps.Count > 0)
                {
                    mustContinueJumping = true;
                    return true;
                }
            }
            else
            {
                movesWithoutCapture++;
            }

            EndTurn();
            return true;
        }

        private void EndTurn()
        {
            mustContinueJumping = false;
            selectedPiece = null;

            string boardState = Board.GetStateString();
            if (!stateHistory.ContainsKey(boardState))
                stateHistory[boardState] = 0;
            stateHistory[boardState]++;

            SwitchTurn();
            CheckWinCondition();
        }

        private void SwitchTurn()
        {
            State = State == GameState.RedTurn ? GameState.BlackTurn : GameState.RedTurn;
        }

        private void CheckWinCondition()
        {
            var currentColor = State == GameState.RedTurn ? PieceColor.Red : PieceColor.Black;
            var pieces = Board.GetAllPieces(currentColor);

            if (pieces.Count == 0)
            {
                State = currentColor == PieceColor.Red ? GameState.BlackWins : GameState.RedWins;
                return;
            }

            bool hasValidMoves = pieces.Any(piece => validator.GetValidMoves(piece).Count > 0);
            if (!hasValidMoves)
            {
                State = currentColor == PieceColor.Red ? GameState.BlackWins : GameState.RedWins;
            }
        }

        public List<Position> GetValidMovePositions()
        {
            if (selectedPiece == null) return new List<Position>();
            return validator.GetValidMoves(selectedPiece).Select(m => m.To).ToList();
        }

        public List<Move> GetAllValidMovesForCurrentPlayer()
        {
            var currentColor = State == GameState.RedTurn ? PieceColor.Red : PieceColor.Black;
            return GetAllValidMoves(currentColor);
        }

        public List<Move> GetAllValidMoves(PieceColor color)
        {
            var allMoves = new List<Move>();
            var pieces = Board.GetAllPieces(color);

            foreach (var piece in pieces)
            {
                allMoves.AddRange(validator.GetValidMoves(piece));
            }

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
            State = GameState.RedTurn;
            stateHistory.Clear();
            ResetTurnState();
        }

        public bool IsGameOver() =>
            State == GameState.RedWins || State == GameState.BlackWins;

        public PieceColor? GetWinner()
        {
            if (State == GameState.RedWins) return PieceColor.Red;
            if (State == GameState.BlackWins) return PieceColor.Black;
            return null;
        }

        public PieceColor GetCurrentTurnColor() =>
            State == GameState.RedTurn ? PieceColor.Red : PieceColor.Black;

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
}