using System.Threading;

namespace checkers_neural_network
{
    /// <summary>
    /// Thread-safe player statistics for AI training.
    /// Uses Interlocked operations to prevent race conditions during parallel processing.
    /// </summary>
    public class PlayerStats
    {
        // Private backing fields for thread-safe operations
        private int _gamesPlayed;
        private int _wins;
        private int _losses;
        private int _draws;
        private int _totalMoves;
        private int _piecesCaptured;
        private int _piecesLost;
        private int _kingsMade;
        private int _kingsCaptured;
        private int _kingsLost;

        // Thread-safe properties using Interlocked
        public int GamesPlayed
        {
            get => Interlocked.CompareExchange(ref _gamesPlayed, 0, 0);
            set => Interlocked.Exchange(ref _gamesPlayed, value);
        }

        public int Wins
        {
            get => Interlocked.CompareExchange(ref _wins, 0, 0);
            set => Interlocked.Exchange(ref _wins, value);
        }

        public int Losses
        {
            get => Interlocked.CompareExchange(ref _losses, 0, 0);
            set => Interlocked.Exchange(ref _losses, value);
        }

        public int Draws
        {
            get => Interlocked.CompareExchange(ref _draws, 0, 0);
            set => Interlocked.Exchange(ref _draws, value);
        }

        public int TotalMoves
        {
            get => Interlocked.CompareExchange(ref _totalMoves, 0, 0);
            set => Interlocked.Exchange(ref _totalMoves, value);
        }

        public int PiecesCaptured
        {
            get => Interlocked.CompareExchange(ref _piecesCaptured, 0, 0);
            set => Interlocked.Exchange(ref _piecesCaptured, value);
        }

        public int PiecesLost
        {
            get => Interlocked.CompareExchange(ref _piecesLost, 0, 0);
            set => Interlocked.Exchange(ref _piecesLost, value);
        }

        public int KingsMade
        {
            get => Interlocked.CompareExchange(ref _kingsMade, 0, 0);
            set => Interlocked.Exchange(ref _kingsMade, value);
        }

        public int KingsCaptured
        {
            get => Interlocked.CompareExchange(ref _kingsCaptured, 0, 0);
            set => Interlocked.Exchange(ref _kingsCaptured, value);
        }

        public int KingsLost
        {
            get => Interlocked.CompareExchange(ref _kingsLost, 0, 0);
            set => Interlocked.Exchange(ref _kingsLost, value);
        }

        // Thread-safe increment methods
        public void IncrementGamesPlayed() => Interlocked.Increment(ref _gamesPlayed);
        public void IncrementWins() => Interlocked.Increment(ref _wins);
        public void IncrementLosses() => Interlocked.Increment(ref _losses);
        public void IncrementDraws() => Interlocked.Increment(ref _draws);
        public void IncrementTotalMoves() => Interlocked.Increment(ref _totalMoves);
        public void IncrementKingsMade() => Interlocked.Increment(ref _kingsMade);
        public void IncrementKingsCaptured() => Interlocked.Increment(ref _kingsCaptured);
        public void IncrementKingsLost() => Interlocked.Increment(ref _kingsLost);

        // Thread-safe add methods
        public void AddPiecesCaptured(int count) => Interlocked.Add(ref _piecesCaptured, count);
        public void AddPiecesLost(int count) => Interlocked.Add(ref _piecesLost, count);

        // Calculated property (read-only, thread-safe through property access)
        public double WinRate
        {
            get
            {
                int games = GamesPlayed;
                return games > 0 ? (double)Wins / games : 0.0;
            }
        }

        // Reset all stats (not thread-safe - call only when not in parallel section)
        public void Reset()
        {
            _gamesPlayed = 0;
            _wins = 0;
            _losses = 0;
            _draws = 0;
            _totalMoves = 0;
            _piecesCaptured = 0;
            _piecesLost = 0;
            _kingsMade = 0;
            _kingsCaptured = 0;
            _kingsLost = 0;
        }
    }
}