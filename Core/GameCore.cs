using System;
using System.Collections.Generic;
using System.Linq;

namespace checkersclaude
{
    // ==================== ENUMS ====================
    public enum PieceColor { Red, Black }
    public enum PieceType { Regular, King }
    public enum GameState { RedTurn, BlackTurn, RedWins, BlackWins }
    public enum GameMode { HumanVsHuman, HumanVsAI }
    public enum GameResult { Win, Loss, Draw }

    // ==================== POSITION ====================
    public struct Position : IEquatable<Position>
    {
        public int Row { get; }
        public int Col { get; }

        public Position(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public bool Equals(Position other) => Row == other.Row && Col == other.Col;
        public override bool Equals(object obj) => obj is Position other && Equals(other);
        public override int GetHashCode() => Row.GetHashCode() ^ Col.GetHashCode();
        public static bool operator ==(Position left, Position right) => left.Equals(right);
        public static bool operator !=(Position left, Position right) => !left.Equals(right);
        public override string ToString() => $"({Row},{Col})";
    }

    // ==================== PIECE ====================
    public class Piece
    {
        public PieceColor Color { get; }
        public PieceType Type { get; private set; }
        public Position Position { get; set; }

        public Piece(PieceColor color, Position position)
        {
            Color = color;
            Type = PieceType.Regular;
            Position = position;
        }

        public void PromoteToKing() => Type = PieceType.King;

        public bool CanMoveInDirection(int rowDirection)
        {
            if (Type == PieceType.King) return true;
            return (Color == PieceColor.Red && rowDirection < 0) ||
                   (Color == PieceColor.Black && rowDirection > 0);
        }

        public Piece Clone() => new Piece(Color, Position) { Type = Type };
    }

    // ==================== MOVE ====================
    public class Move
    {
        public Position From { get; }
        public Position To { get; }
        public bool IsJump { get; }
        public List<Position> JumpedPositions { get; }

        public Move(Position from, Position to, bool isJump = false, List<Position> jumpedPositions = null)
        {
            From = from;
            To = to;
            IsJump = isJump;
            JumpedPositions = jumpedPositions ?? new List<Position>();
        }

        public Move(Position from, Position to, Position jumpedPosition)
            : this(from, to, true, new List<Position> { jumpedPosition }) { }

        public void AddJumped(Position pos) => JumpedPositions.Add(pos);
        public override string ToString() => $"{From} → {To}" + (IsJump ? $" (jumps {JumpedPositions.Count})" : "");
    }

    // ==================== BOARD ====================
    public class Board
    {
        private const int BoardSize = 8;
        private readonly Piece[,] squares;

        public Board() : this(true) { }

        private Board(bool initialize)
        {
            squares = new Piece[BoardSize, BoardSize];
            if (initialize) InitializeBoard();
        }

        private void InitializeBoard()
        {
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    if ((row + col) % 2 == 1)
                        squares[row, col] = new Piece(PieceColor.Black, new Position(row, col));
                }
            }

            for (int row = 5; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    if ((row + col) % 2 == 1)
                        squares[row, col] = new Piece(PieceColor.Red, new Position(row, col));
                }
            }
        }

        public Piece GetPiece(Position pos) => IsValidPosition(pos) ? squares[pos.Row, pos.Col] : null;

        public void SetPiece(Position pos, Piece piece)
        {
            if (IsValidPosition(pos))
            {
                squares[pos.Row, pos.Col] = piece;
                if (piece != null) piece.Position = pos;
            }
        }

        public void RemovePiece(Position pos)
        {
            if (IsValidPosition(pos)) squares[pos.Row, pos.Col] = null;
        }

        public bool IsValidPosition(Position pos) =>
            pos.Row >= 0 && pos.Row < BoardSize && pos.Col >= 0 && pos.Col < BoardSize;

        public bool IsPlayableSquare(Position pos) =>
            IsValidPosition(pos) && (pos.Row + pos.Col) % 2 == 1;

        public List<Piece> GetAllPieces(PieceColor color)
        {
            var pieces = new List<Piece>();
            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    var piece = squares[row, col];
                    if (piece != null && piece.Color == color)
                        pieces.Add(piece);
                }
            }
            return pieces;
        }

        public int GetBoardSize() => BoardSize;

        public void ApplyMove(Move move)
        {
            var piece = GetPiece(move.From);
            if (piece == null) return;

            RemovePiece(move.From);

            if (move.IsJump)
            {
                foreach (var jumped in move.JumpedPositions)
                    RemovePiece(jumped);
            }

            SetPiece(move.To, piece);

            if (piece.Type != PieceType.King)
            {
                if ((piece.Color == PieceColor.Red && move.To.Row == 0) ||
                    (piece.Color == PieceColor.Black && move.To.Row == BoardSize - 1))
                {
                    piece.PromoteToKing();
                }
            }
        }

        public Board Clone()
        {
            var clonedBoard = new Board(false);
            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    var piece = squares[row, col];
                    if (piece != null)
                    {
                        var clonedPiece = new Piece(piece.Color, new Position(row, col));
                        if (piece.Type == PieceType.King)
                            clonedPiece.PromoteToKing();
                        clonedBoard.squares[row, col] = clonedPiece;
                    }
                }
            }
            return clonedBoard;
        }

        public string GetStateString()
        {
            var chars = new char[64];
            int idx = 0;
            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    var piece = squares[row, col];
                    chars[idx++] = piece == null ? '.' :
                                   piece.Color == PieceColor.Red ?
                                   (piece.Type == PieceType.King ? 'R' : 'r') :
                                   (piece.Type == PieceType.King ? 'B' : 'b');
                }
            }
            return new string(chars);
        }
    }

    // ==================== MOVE VALIDATOR ====================
    public class MoveValidator
    {
        private readonly Board board;
        private Dictionary<string, List<Move>> moveCache;

        public MoveValidator(Board board)
        {
            this.board = board;
            this.moveCache = new Dictionary<string, List<Move>>();
        }

        public void ClearCache()
        {
            moveCache.Clear();
        }

        public List<Move> GetValidMoves(Piece piece)
        {
            string cacheKey = $"{piece.Position}_{piece.Color}_{piece.Type}_{board.GetStateString()}";

            if (moveCache.ContainsKey(cacheKey))
                return new List<Move>(moveCache[cacheKey]);

            List<Move> moves;
            if (HasAvailableJumps(piece.Color))
            {
                moves = piece.Type == PieceType.King ?
                    GetValidKingJumps(piece) :
                    GetValidJumps(piece);
            }
            else
            {
                moves = piece.Type == PieceType.King ?
                    GetValidKingMoves(piece) :
                    GetValidRegularMoves(piece);
            }

            moveCache[cacheKey] = moves;
            return new List<Move>(moves);
        }

        private List<Move> GetValidRegularMoves(Piece piece)
        {
            var moves = new List<Move>();
            int rowDir = piece.Color == PieceColor.Red ? -1 : 1;

            foreach (int colDir in new[] { -1, 1 })
            {
                var newPos = new Position(piece.Position.Row + rowDir, piece.Position.Col + colDir);
                if (board.IsPlayableSquare(newPos) && board.GetPiece(newPos) == null)
                    moves.Add(new Move(piece.Position, newPos));
            }

            return moves;
        }

        private List<Move> GetValidKingMoves(Piece piece)
        {
            var moves = new List<Move>();

            foreach (int rowDir in new[] { -1, 1 })
            {
                foreach (int colDir in new[] { -1, 1 })
                {
                    int step = 1;
                    while (true)
                    {
                        var newPos = new Position(
                            piece.Position.Row + rowDir * step,
                            piece.Position.Col + colDir * step);

                        if (!board.IsValidPosition(newPos) || !board.IsPlayableSquare(newPos))
                            break;

                        if (board.GetPiece(newPos) == null)
                        {
                            moves.Add(new Move(piece.Position, newPos));
                            step++;
                        }
                        else
                            break;
                    }
                }
            }

            return moves;
        }

        public List<Move> GetValidJumps(Piece piece)
        {
            var jumps = new List<Move>();
            int[] directions = piece.Type == PieceType.King ?
                new[] { -1, 1 } :
                new[] { piece.Color == PieceColor.Red ? -1 : 1 };

            foreach (int rowDir in directions)
            {
                foreach (int colDir in new[] { -1, 1 })
                {
                    var jumpedPos = new Position(
                        piece.Position.Row + rowDir,
                        piece.Position.Col + colDir);

                    var landingPos = new Position(
                        piece.Position.Row + rowDir * 2,
                        piece.Position.Col + colDir * 2);

                    var jumpedPiece = board.GetPiece(jumpedPos);

                    if (board.IsValidPosition(landingPos) &&
                        board.IsPlayableSquare(landingPos) &&
                        board.GetPiece(landingPos) == null &&
                        jumpedPiece != null &&
                        jumpedPiece.Color != piece.Color)
                    {
                        jumps.Add(new Move(piece.Position, landingPos, jumpedPos));
                    }
                }
            }

            return jumps;
        }

        public List<Move> GetValidKingJumps(Piece piece)
        {
            var jumps = new List<Move>();

            foreach (int rowDir in new[] { -1, 1 })
            {
                foreach (int colDir in new[] { -1, 1 })
                {
                    int r = piece.Position.Row + rowDir;
                    int c = piece.Position.Col + colDir;

                    while (board.IsValidPosition(new Position(r, c)) &&
                           board.IsPlayableSquare(new Position(r, c)))
                    {
                        var p = board.GetPiece(new Position(r, c));

                        if (p == null)
                        {
                            r += rowDir;
                            c += colDir;
                            continue;
                        }

                        if (p.Color == piece.Color)
                            break;

                        var landingPos = new Position(r + rowDir, c + colDir);

                        if (board.IsValidPosition(landingPos) &&
                            board.IsPlayableSquare(landingPos) &&
                            board.GetPiece(landingPos) == null)
                        {
                            jumps.Add(new Move(piece.Position, landingPos, new Position(r, c)));
                        }

                        break;
                    }
                }
            }

            return jumps;
        }

        public bool HasAvailableJumps(PieceColor color)
        {
            var pieces = board.GetAllPieces(color);
            foreach (var piece in pieces)
            {
                var jumps = piece.Type == PieceType.King ?
                    GetValidKingJumps(piece) :
                    GetValidJumps(piece);
                if (jumps.Count > 0) return true;
            }
            return false;
        }
    }
}   