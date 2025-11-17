using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace checkersclaude
{
    public class Population
    {
        public List<Player> Players { get; private set; }
        public Player BestPlayer { get; private set; }
        private Random rng = new Random();

        public Population(int size, double mutationRate)
        {
            Players = new List<Player>();
            for (int i = 0; i < size; i++)
                Players.Add(new Player());
        }

        // Multithreaded tournament
        public void RunTournament(int gamesPerPair = 2, Action<Board, Move> onMove = null)
        {
            var players = new List<Player>(Players); // use your actual player list

            // Reset stats before tournament
            foreach (var p in players) p.ResetStats();

            // Prepare all pairs
            var pairs = new List<(Player, Player)>();
            for (int i = 0; i < players.Count; i++)
            {
                for (int j = i + 1; j < players.Count; j++)
                {
                    pairs.Add((players[i], players[j]));
                }
            }

            // Run matches in parallel
            Parallel.ForEach(pairs, pair =>
            {
                Player p1 = pair.Item1;
                Player p2 = pair.Item2;

                for (int g = 0; g < gamesPerPair; g++)
                {
                    var engine = new GameEngine();
                    Player current = (g % 2 == 0) ? p1 : p2; // alternate starting player
                    Player opponent = (g % 2 == 0) ? p2 : p1;

                    while (engine.State != GameState.RedWins && engine.State != GameState.BlackWins && engine.State != GameState.Draw)
                    {
                        var validMoves = engine.GetValidMoves(current == p1 ? PieceColor.Red : PieceColor.Black);
                        if (validMoves.Count == 0) break;

                        Move move = current.ChooseMove(engine.Board, validMoves, current == p1 ? PieceColor.Red : PieceColor.Black);
                        engine.MovePiece(move.To); // use your GameEngine method

                        onMove?.Invoke(engine.Board, move); // optional UI callback

                        // Swap players
                        var tmp = current; current = opponent; opponent = tmp;
                    }

                    // Update wins/draws based on engine state
                    if (engine.State == GameState.RedWins) p1.Wins++;
                    else if (engine.State == GameState.BlackWins) p2.Wins++;
                    else p1.Draws++; p2.Draws++;
                }
            });

            // Calculate fitness after all matches
            foreach (var p in players)
                p.CalculateFitness();
        }




        public void Evolve()
        {
            // Example: simple top 50% selection + crossover + mutation
            Players.Sort((a, b) => b.Fitness.CompareTo(a.Fitness));
            int survivors = Players.Count / 2;
            List<Player> newGen = new List<Player>();

            // Keep top half
            for (int i = 0; i < survivors; i++)
                newGen.Add(Players[i].Clone());

            // Fill the rest by crossover
            while (newGen.Count < Players.Count)
            {
                Player parent1 = Players[rng.Next(survivors)];
                Player parent2 = Players[rng.Next(survivors)];
                Player child = parent1.Crossover(parent2, rng);
                child.Mutate(0.05); // Example mutation rate
                newGen.Add(child);
            }

            Players = newGen;
        }

        public string GetGenerationStats()
        {
            double avgFitness = 0;
            foreach (var p in Players) avgFitness += p.Fitness;
            avgFitness /= Players.Count;
            return $"Best: {BestPlayer.Fitness:F2}, Avg: {avgFitness:F2}";
        }
    }
}
