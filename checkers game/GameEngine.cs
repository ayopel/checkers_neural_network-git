using System.Collections.Generic;

namespace checkersclaude
{
    public enum GameState
    {
        RedTurn,
        BlackTurn,
        RedWins,
        BlackWins,
        Draw
    }

    public class GameEngine
    {
        public Board Board { get; private set; }
        public GameState State { get; private set; }
        private MoveValidator validator;
        private Piece selectedPiece;
        private bool mustContinueJumping;
        private int movesSinceCapture;
        private const int MaxMovesWithoutCapture = 50;

        public GameEngine()
        {
            Board = new Board();
            validator = new MoveValidator(Board);
            State = GameState.RedTurn;
            selectedPiece = null;
            mustContinueJumping = false;
            movesSinceCapture = 0;
        }
        public bool IsGameOver()
        {
            return State == GameState.RedWins || State == GameState.BlackWins || State == GameState.Draw;
        }

        // Apply a move using your existing MovePiece method
        public void ApplyMove(Move move)
        {
            MovePiece(move.To);
        }


        public GameEngine(Board board)
        {
            Board = board;
            validator = new MoveValidator(Board);
            State = GameState.RedTurn;
            selectedPiece = null;
            mustContinueJumping = false;
            movesSinceCapture = 0;
        }

        public bool SelectPiece(Position pos)
        {
            if (mustContinueJumping && selectedPiece != null)
                return selectedPiece.Position.Equals(pos);

            Piece piece = Board.GetPiece(pos);
            if (piece == null)
                return false;

            PieceColor currentColor = State == GameState.RedTurn ? PieceColor.Red : PieceColor.Black;
            if (piece.Color != currentColor)
                return false;

            // Check if there are mandatory jumps
            if (validator.HasAvailableJumps(currentColor))
            {
                List<Move> jumps = piece.Type == PieceType.King ?
                    validator.GetValidKingJumps(piece) :
                    validator.GetValidJumps(piece);

                if (jumps.Count == 0)
                    return false; // Can't select a piece without jumps
            }

            selectedPiece = piece;
            return true;
        }

        public bool MovePiece(Position to)
        {
            if (selectedPiece == null)
                return false;

            List<Move> validMoves = validator.GetValidMoves(selectedPiece);
            Move selectedMove = validMoves.Find(m => m.To.Equals(to));
            if (selectedMove == null)
                return false;

            // Track position before move for king check
            Position fromPos = new Position(selectedPiece.Position.Row, selectedPiece.Position.Col);
            bool wasKing = selectedPiece.Type == PieceType.King;

            // Execute move
            Board.RemovePiece(selectedPiece.Position);
            selectedPiece.Position = to;
            Board.SetPiece(to, selectedPiece);

            // Handle jump
            bool capturedPiece = false;
            if (selectedMove.IsJump)
            {
                foreach (var jumpedPosition in selectedMove.JumpedPositions)
                {
                    Board.RemovePiece(jumpedPosition);
                    capturedPiece = true;
                }

                movesSinceCapture = 0;

                // Check for additional jumps (multi-jump)
                List<Move> additionalJumps = selectedPiece.Type == PieceType.King ?
                    validator.GetValidKingJumps(selectedPiece) :
                    validator.GetValidJumps(selectedPiece);

                if (additionalJumps.Count > 0)
                {
                    mustContinueJumping = true;
                    return true; // Player must continue jumping
                }
            }
            else
            {
                movesSinceCapture++;
            }

            // King promotion
            if (!wasKing && selectedPiece.Type == PieceType.Regular)
            {
                if ((selectedPiece.Color == PieceColor.Red && to.Row == 0) ||
                    (selectedPiece.Color == PieceColor.Black && to.Row == Board.GetBoardSize() - 1))
                {
                    selectedPiece.Type = PieceType.King;
                }
            }

            // End turn
            mustContinueJumping = false;
            selectedPiece = null;
            SwitchTurn();
            CheckWinCondition();

            return true;
        }

        public List<Position> GetValidMovePositions()
        {
            if (selectedPiece == null)
                return new List<Position>();

            List<Move> moves = validator.GetValidMoves(selectedPiece);
            List<Position> positions = new List<Position>();
            foreach (Move move in moves)
                positions.Add(move.To);

            return positions;
        }

        private void SwitchTurn()
        {
            if (State == GameState.RedTurn) State = GameState.BlackTurn;
            else if (State == GameState.BlackTurn) State = GameState.RedTurn;
        }

        private void CheckWinCondition()
        {
            // Determine current player's color (the player whose turn it is now)
            PieceColor currentColor = State == GameState.RedTurn ? PieceColor.Red : PieceColor.Black;
            List<Piece> pieces = Board.GetAllPieces(currentColor);

            // No pieces left
            if (pieces.Count == 0)
            {
                State = currentColor == PieceColor.Red ? GameState.BlackWins : GameState.RedWins;
                return;
            }

            // Check if any valid moves
            bool hasValidMoves = false;
            foreach (Piece piece in pieces)
            {
                if (validator.GetValidMoves(piece).Count > 0)
                {
                    hasValidMoves = true;
                    break;
                }
            }

            if (!hasValidMoves)
            {
                State = currentColor == PieceColor.Red ? GameState.BlackWins : GameState.RedWins;
                return;
            }

            // Draw condition: max moves without capture
            if (movesSinceCapture >= MaxMovesWithoutCapture)
            {
                State = GameState.Draw;
            }
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
            selectedPiece = null;
            mustContinueJumping = false;
            movesSinceCapture = 0;
        }

        public bool MustContinueJumping() => mustContinueJumping;

        public int GetMovesSinceCapture() => movesSinceCapture;
    }
}
