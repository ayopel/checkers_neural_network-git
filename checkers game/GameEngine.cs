using System.Collections.Generic;

namespace checkersclaude
{
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

            // Check if this piece can make mandatory jumps
            if (validator.HasAvailableJumps(currentColor))
            {
                List<Move> jumps = piece.Type == PieceType.King ?
                    validator.GetValidKingJumps(piece) :
                    validator.GetValidJumps(piece);

                if (jumps.Count == 0)
                    return false; // Can't select piece with no jumps when jumps are available
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
                    return true;
                }
            }
            else
            {
                movesSinceCapture++;
            }

            // Check for king promotion (only if wasn't already a king)
            if (!wasKing && selectedPiece.Type == PieceType.Regular)
            {
                if ((selectedPiece.Color == PieceColor.Red && to.Row == 0) ||
                    (selectedPiece.Color == PieceColor.Black && to.Row == Board.GetBoardSize() - 1))
                {
                    selectedPiece.PromoteToKing();
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
            State = State == GameState.RedTurn ? GameState.BlackTurn : GameState.RedTurn;
        }

        private void CheckWinCondition()
        {
            // Determine current player's color (the player whose turn it is now)
            PieceColor currentColor = State == GameState.RedTurn ? PieceColor.Red : PieceColor.Black;

            // Get all pieces for the current player
            List<Piece> pieces = Board.GetAllPieces(currentColor) ?? new List<Piece>();

            // Check if current player has no pieces
            if (pieces.Count == 0)
            {
                State = currentColor == PieceColor.Red ? GameState.BlackWins : GameState.RedWins;
                return;
            }

            // Check if current player has any valid moves
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
            }

            // Optional: check for draw by move limit (50 moves without capture)
         
        }

        public Piece GetSelectedPiece()
        {
            return selectedPiece;
        }

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

        public bool MustContinueJumping()
        {
            return mustContinueJumping;
        }

        public int GetMovesSinceCapture()
        {
            return movesSinceCapture;
        }
    }
}
