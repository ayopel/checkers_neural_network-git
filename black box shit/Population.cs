using System;
using System.Collections.Generic;
using System.Linq;

namespace checkersclaude
{
    public class Population
    {
        public List<Player> Players { get; private set; }
        public Player BestPlayer { get; private set; }
        public int Generation { get; private set; }

        private double mutationRate;
        private Random random;
        private const int EliteCount = 3; // Top performers to keep unchanged
        private const int TournamentSize = 3; // For tournament selection

        public Population(int populationSize, double mutationRate)
        {
            this.mutationRate = mutationRate;
            this.random = new Random(Guid.NewGuid().GetHashCode()); // Better randomization
            this.Generation = 0;

            Players = new List<Player>();

            // Create diverse initial population with different random seeds
            for (int i = 0; i < populationSize; i++)
            {
                Random playerRandom = new Random(Guid.NewGuid().GetHashCode() + i);
                Players.Add(new Player(playerRandom));
            }

            BestPlayer = null;
        }

        public void RunTournament(int gamesPerPair = 2)
        {
            // Reset all player stats
            foreach (Player player in Players)
            {
                player.ResetStats();
            }

            // Round-robin tournament - each player plays against several others
            int matchesPerPlayer = Math.Min(8, Players.Count - 1);

            for (int i = 0; i < Players.Count; i++)
            {
                // Select different opponents for variety
                List<int> opponents = GetRandomOpponents(i, matchesPerPlayer);

                foreach (int opponentIndex in opponents)
                {
                    // Play games with alternating colors
                    PlayGame(Players[i], Players[opponentIndex], PieceColor.Red);
                    PlayGame(Players[i], Players[opponentIndex], PieceColor.Black);
                }
            }

            // Calculate fitness for all players
            foreach (Player player in Players)
            {
                player.CalculateFitness();
            }

            // Update best player
            Player currentBest = Players.OrderByDescending(p => p.Fitness).First();
            if (BestPlayer == null || currentBest.Fitness > BestPlayer.Fitness)
            {
                BestPlayer = currentBest.Clone();
            }
        }

        private List<int> GetRandomOpponents(int playerIndex, int count)
        {
            List<int> opponents = new List<int>();
            List<int> available = new List<int>();

            for (int i = 0; i < Players.Count; i++)
            {
                if (i != playerIndex)
                    available.Add(i);
            }

            // Shuffle available opponents
            for (int i = available.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int temp = available[i];
                available[i] = available[j];
                available[j] = temp;
            }

            // Take first 'count' opponents
            for (int i = 0; i < Math.Min(count, available.Count); i++)
            {
                opponents.Add(available[i]);
            }

            return opponents;
        }

        private void PlayGame(Player player1, Player player2, PieceColor player1Color)
        {
            Board board = new Board();
            PieceColor player2Color = player1Color == PieceColor.Red ? PieceColor.Black : PieceColor.Red;
            PieceColor currentColor = PieceColor.Red; // Red always starts

            int maxMoves = 200; // Prevent infinite games
            int moveCount = 0;
            int movesWithoutCapture = 0;
            const int maxMovesWithoutCapture = 40; // Draw condition


            while (moveCount < maxMoves)
            {
                Player currentPlayer = currentColor == player1Color ? player1 : player2;
                Player otherPlayer = currentColor == player1Color ? player2 : player1;

                // Get all valid moves for current player
                List<Move> allMoves = GetAllValidMoves(board, currentColor);

                if (allMoves.Count == 0)
                {
                    // Current player has no moves - they lose
                    if (currentColor == player1Color)
                    {
                        player1.Losses++;
                        player2.Wins++;
                    }
                    else
                    {
                        player1.Wins++;
                        player2.Losses++;
                    }
                    return;
                }

                // Choose and execute move
                Move selectedMove = currentPlayer.ChooseMove(board, allMoves, currentColor);

                if (selectedMove == null)
                {
                    // Error in move selection - current player loses
                    if (currentColor == player1Color)
                    {
                        player1.Losses++;
                        player2.Wins++;
                    }
                    else
                    {
                        player1.Wins++;
                        player2.Losses++;
                    }
                    return;
                }

                // Count opponent pieces before move
                PieceColor opponentColor = currentColor == PieceColor.Red ? PieceColor.Black : PieceColor.Red;
                int opponentPiecesBefore = board.GetAllPieces(opponentColor).Count;

                // Check if moving piece will become a king
                Piece movingPiece = board.GetPiece(selectedMove.From);
                bool willBecomeKing = false;
                if (movingPiece != null && movingPiece.Type != PieceType.King)
                {
                    willBecomeKing = (movingPiece.Color == PieceColor.Red && selectedMove.To.Row == 0) ||
                                    (movingPiece.Color == PieceColor.Black && selectedMove.To.Row == 7);
                }

                // Execute the move
                board.ApplyMove(selectedMove);

                // Count opponent pieces after move
                int opponentPiecesAfter = board.GetAllPieces(opponentColor).Count;
                int piecesCaptured = opponentPiecesBefore - opponentPiecesAfter;

                // Update stats
                if (piecesCaptured > 0)
                {
                    otherPlayer.PiecesLost += piecesCaptured;

                    // Check if any kings were captured
                    if (selectedMove.IsJump && selectedMove.JumpedPositions != null)
                    {
                        foreach (var jumpedPos in selectedMove.JumpedPositions)
                        {
                            // We can't check anymore since pieces are removed, but we tracked it in player stats
                        }
                    }

                    movesWithoutCapture = 0;
                }
                else
                {
                    movesWithoutCapture++;
                }

                if (willBecomeKing)
                {
                    currentPlayer.KingsMade++;
                }

                // Check for draws (no captures for too long)
                if (movesWithoutCapture >= maxMovesWithoutCapture)
                {
                    player1.Draws++;
                    player2.Draws++;
                    return;
                }

                // Check win conditions
                List<Piece> redPieces = board.GetAllPieces(PieceColor.Red);
                List<Piece> blackPieces = board.GetAllPieces(PieceColor.Black);

                if (redPieces.Count == 0)
                {
                    if (player1Color == PieceColor.Black)
                    {
                        player1.Wins++;
                        player2.Losses++;
                    }
                    else
                    {
                        player1.Losses++;
                        player2.Wins++;
                    }
                    return;
                }

                if (blackPieces.Count == 0)
                {
                    if (player1Color == PieceColor.Red)
                    {
                        player1.Wins++;
                        player2.Losses++;
                    }
                    else
                    {
                        player1.Losses++;
                        player2.Wins++;
                    }
                    return;
                }

                // Switch turns
                currentColor = currentColor == PieceColor.Red ? PieceColor.Black : PieceColor.Red;
                moveCount++;
            }

            // Max moves reached - draw
            player1.Draws++;
            player2.Draws++;
        }

        private List<Move> GetAllValidMoves(Board board, PieceColor color)
        {
            List<Move> allMoves = new List<Move>();
            MoveValidator validator = new MoveValidator(board);

            List<Piece> pieces = board.GetAllPieces(color);

            // Check for mandatory jumps first
            bool hasJumps = false;
            foreach (Piece piece in pieces)
            {
                List<Move> jumps = piece.Type == PieceType.King ?
                    validator.GetValidKingJumps(piece) :
                    validator.GetValidJumps(piece);

                if (jumps.Count > 0)
                {
                    hasJumps = true;
                    allMoves.AddRange(jumps);
                }
            }

            // If there are jumps, only return jumps (mandatory in checkers)
            if (hasJumps)
                return allMoves;

            // Otherwise, get all normal moves
            foreach (Piece piece in pieces)
            {
                allMoves.AddRange(validator.GetValidMoves(piece));
            }

            return allMoves;
        }

        public void Evolve()
        {
            Generation++;

            // Sort players by fitness (descending)
            List<Player> sortedPlayers = Players.OrderByDescending(p => p.Fitness).ToList();

            List<Player> newPopulation = new List<Player>();

            // Keep elite players (exact copies)
            for (int i = 0; i < EliteCount && i < sortedPlayers.Count; i++)
            {
                newPopulation.Add(sortedPlayers[i].Clone());
            }

            // Create offspring to fill the rest of the population
            while (newPopulation.Count < Players.Count)
            {
                // Select parents using tournament selection
                Player parent1 = TournamentSelection(sortedPlayers);
                Player parent2 = TournamentSelection(sortedPlayers);

                // Ensure parents are different
                int attempts = 0;
                while (parent1 == parent2 && attempts < 5)
                {
                    parent2 = TournamentSelection(sortedPlayers);
                    attempts++;
                }

                // Create offspring through crossover
                Player child = parent1.Crossover(parent2, random);

                // Apply mutation with some randomness
                double adaptiveMutationRate = mutationRate * (0.5 + random.NextDouble());
                child.Mutate(adaptiveMutationRate);

                newPopulation.Add(child);
            }

            // Replace 10% of population with completely random players for diversity
            int randomCount = Math.Max(2, Players.Count / 10);
            for (int i = 0; i < randomCount; i++)
            {
                if (newPopulation.Count > EliteCount + i)
                {
                    Random newRandom = new Random(Guid.NewGuid().GetHashCode() + Generation * 1000 + i);
                    newPopulation[EliteCount + i] = new Player(newRandom);
                }
            }

            Players = newPopulation;
        }

        private Player TournamentSelection(List<Player> sortedPlayers)
        {
            List<Player> tournament = new List<Player>();

            for (int i = 0; i < TournamentSize; i++)
            {
                int randomIndex = random.Next(sortedPlayers.Count);
                tournament.Add(sortedPlayers[randomIndex]);
            }

            return tournament.OrderByDescending(p => p.Fitness).First();
        }

        public string GetGenerationStats()
        {
            if (Players.Count == 0)
                return "No players";

            double avgFitness = Players.Average(p => p.Fitness);
            double maxFitness = Players.Max(p => p.Fitness);
            double minFitness = Players.Min(p => p.Fitness);
            double avgWins = Players.Average(p => p.Wins);
            double avgLosses = Players.Average(p => p.Losses);
            double avgDraws = Players.Average(p => p.Draws);
            double avgMoves = Players.Average(p => p.TotalMoves);
            double avgCaptures = Players.Average(p => p.PiecesCaptured);
            double avgKings = Players.Average(p => p.KingsMade);

            Player best = Players.OrderByDescending(p => p.Fitness).First();
            Player worst = Players.OrderBy(p => p.Fitness).First();

            return $"Generation {Generation} | Best Fitness: {maxFitness:F2} | Avg Fitness: {avgFitness:F2} | " +
                   $"Avg Wins: {avgWins:F1} | Avg Moves: {avgMoves:F0} | " +
                   $"Range: {minFitness:F0}-{maxFitness:F0}";
        }
    }
}