using System;
using System.Drawing;
using System.Windows.Forms;

namespace checkersclaude
{
    public class CheckersForm : Form
    {
        private readonly GameEngine game;
        private Button[,] boardButtons;
        private Label statusLabel;
        private Label moveHistoryLabel;
        private Label statsLabel;
        private Button resetButton;
        private Button undoButton;
        private readonly GameMode mode;
        private readonly AIPlayer aiPlayer;

        private const int SquareSize = 70;
        private Position? lastMoveFrom;
        private Position? lastMoveTo;
        private int moveCount = 0;
        private int redPiecesCaptured = 0;
        private int blackPiecesCaptured = 0;

        public CheckersForm(GameMode mode, AIPlayer aiPlayer)
        {
            this.mode = mode;
            this.aiPlayer = aiPlayer;
            game = new GameEngine();
            InitializeUI();
            CreateBoard();
            UpdateBoard();
        }

        private void InitializeUI()
        {
            Text = mode == GameMode.HumanVsAI ? "Checkers - You vs AI" : "Checkers - Two Players";
            ClientSize = new Size(SquareSize * 8 + 250, SquareSize * 8 + 80);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(240, 240, 245);

            // Status label
            statusLabel = new Label
            {
                Location = new Point(10, SquareSize * 8 + 10),
                Size = new Size(300, 30),
                Font = new Font("Arial", 14, FontStyle.Bold),
                Text = mode == GameMode.HumanVsAI ? "Your Turn (Red)" : "Red's Turn"
            };
            Controls.Add(statusLabel);

            // Move history label
            moveHistoryLabel = new Label
            {
                Location = new Point(10, SquareSize * 8 + 45),
                Size = new Size(300, 20),
                Font = new Font("Arial", 9),
                Text = "Move #0 - Game Start",
                ForeColor = Color.Gray
            };
            Controls.Add(moveHistoryLabel);

            // Side panel
            Panel sidePanel = new Panel
            {
                Location = new Point(SquareSize * 8 + 10, 10),
                Size = new Size(230, SquareSize * 8),
                BackColor = Color.FromArgb(250, 250, 250),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(sidePanel);

            // Stats label
            statsLabel = new Label
            {
                Location = new Point(10, 10),
                Size = new Size(210, 150),
                Font = new Font("Arial", 10),
                Text = GetStatsText(),
                BackColor = Color.Transparent
            };
            sidePanel.Controls.Add(statsLabel);

            // Reset button
            resetButton = new Button
            {
                Location = new Point(10, 170),
                Size = new Size(210, 40),
                Text = "🔄 New Game",
                Font = new Font("Arial", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            resetButton.FlatAppearance.BorderSize = 0;
            resetButton.Click += ResetButton_Click;
            sidePanel.Controls.Add(resetButton);

            // Undo button (disabled for AI mode)
            undoButton = new Button
            {
                Location = new Point(10, 220),
                Size = new Size(210, 40),
                Text = "↶ Undo Move",
                Font = new Font("Arial", 11, FontStyle.Bold),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = false // We'll implement undo later if needed
            };
            undoButton.FlatAppearance.BorderSize = 0;
            sidePanel.Controls.Add(undoButton);

            // Game info
            Label infoLabel = new Label
            {
                Location = new Point(10, 280),
                Size = new Size(210, 100),
                Font = new Font("Arial", 9),
                Text = mode == GameMode.HumanVsAI ?
                    "🎮 Playing vs AI\n\n" +
                    "• You are Red (●)\n" +
                    "• AI is Black (●)\n" +
                    "• Click to select\n" +
                    "• Click again to move\n" +
                    "• Must jump if possible" :
                    "👥 Two Player Mode\n\n" +
                    "• Red goes first\n" +
                    "• Click to select\n" +
                    "• Click again to move\n" +
                    "• Must jump if possible",
                ForeColor = Color.DarkSlateGray,
                BackColor = Color.Transparent
            };
            sidePanel.Controls.Add(infoLabel);

            // Legend
            Panel legendPanel = new Panel
            {
                Location = new Point(10, SquareSize * 8 - 120),
                Size = new Size(210, 110),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            sidePanel.Controls.Add(legendPanel);

            Label legendTitle = new Label
            {
                Location = new Point(5, 5),
                Size = new Size(200, 20),
                Text = "Legend:",
                Font = new Font("Arial", 9, FontStyle.Bold)
            };
            legendPanel.Controls.Add(legendTitle);

            CreateLegendItem(legendPanel, "● = Regular Piece", Color.Black, 30);
            CreateLegendItem(legendPanel, "♔ = King", Color.Black, 50);
            CreateLegendItem(legendPanel, "🟡 = Selected", Color.Gold, 70);
            CreateLegendItem(legendPanel, "🟢 = Valid Move", Color.LimeGreen, 90);
        }

        private void CreateLegendItem(Panel parent, string text, Color color, int y)
        {
            Label item = new Label
            {
                Location = new Point(10, y),
                Size = new Size(190, 18),
                Text = text,
                Font = new Font("Arial", 8),
                ForeColor = color
            };
            parent.Controls.Add(item);
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            game.ResetGame();
            lastMoveFrom = null;
            lastMoveTo = null;
            moveCount = 0;
            redPiecesCaptured = 0;
            blackPiecesCaptured = 0;
            UpdateBoard();
        }

        private string GetStatsText()
        {
            int redPieces = game.Board.GetAllPieces(PieceColor.Red).Count;
            int blackPieces = game.Board.GetAllPieces(PieceColor.Black).Count;
            int redKings = game.Board.GetAllPieces(PieceColor.Red).FindAll(p => p.Type == PieceType.King).Count;
            int blackKings = game.Board.GetAllPieces(PieceColor.Black).FindAll(p => p.Type == PieceType.King).Count;

            return $"📊 Game Statistics\n\n" +
                   $"Move #{moveCount}\n\n" +
                   $"🔴 Red Pieces: {redPieces}\n" +
                   $"   Kings: {redKings}\n" +
                   $"   Captured: {blackPiecesCaptured}\n\n" +
                   $"⚫ Black Pieces: {blackPieces}\n" +
                   $"   Kings: {blackKings}\n" +
                   $"   Captured: {redPiecesCaptured}";
        }

        private void CreateBoard()
        {
            boardButtons = new Button[8, 8];

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var btn = new Button
                    {
                        Size = new Size(SquareSize, SquareSize),
                        Location = new Point(col * SquareSize, row * SquareSize),
                        FlatStyle = FlatStyle.Flat,
                        Font = new Font("Arial", 32, FontStyle.Bold),
                        Tag = new Position(row, col)
                    };

                    btn.BackColor = (row + col) % 2 == 0 ?
                        Color.FromArgb(240, 217, 181) :
                        Color.FromArgb(181, 136, 99);

                    btn.Click += Square_Click;
                    boardButtons[row, col] = btn;
                    Controls.Add(btn);
                }
            }
        }

        private void UpdateBoard()
        {
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    var pos = new Position(row, col);
                    var piece = game.Board.GetPiece(pos);
                    var btn = boardButtons[row, col];

                    btn.Text = "";
                    btn.ForeColor = Color.Black;
                    btn.FlatAppearance.BorderSize = 0;
                    btn.BackColor = (row + col) % 2 == 0 ?
                        Color.FromArgb(240, 217, 181) :
                        Color.FromArgb(181, 136, 99);

                    // Draw piece
                    if (piece != null)
                    {
                        btn.Text = piece.Type == PieceType.King ? "♔" : "●";
                        btn.ForeColor = piece.Color == PieceColor.Red ?
                            Color.FromArgb(200, 0, 0) : Color.FromArgb(50, 50, 50);
                    }

                    // Highlight selected piece
                    if (game.GetSelectedPiece()?.Position == pos)
                    {
                        btn.FlatAppearance.BorderSize = 5;
                        btn.FlatAppearance.BorderColor = Color.Gold;
                        btn.BackColor = Color.FromArgb(255, 255, 200);
                    }

                    // Highlight valid moves
                    if (game.GetValidMovePositions().Contains(pos))
                    {
                        btn.BackColor = Color.FromArgb(144, 238, 144);
                        btn.FlatAppearance.BorderSize = 2;
                        btn.FlatAppearance.BorderColor = Color.Green;
                    }

                    // Show last move with arrow
                    if (lastMoveFrom.HasValue && lastMoveFrom.Value == pos)
                    {
                        btn.FlatAppearance.BorderSize = 3;
                        btn.FlatAppearance.BorderColor = Color.Orange;
                    }

                    if (lastMoveTo.HasValue && lastMoveTo.Value == pos)
                    {
                        btn.FlatAppearance.BorderSize = 3;
                        btn.FlatAppearance.BorderColor = Color.DarkOrange;

                        // Add arrow indicator
                        if (piece != null)
                        {
                            string arrow = GetArrowDirection(lastMoveFrom.Value, lastMoveTo.Value);
                            btn.Text = arrow + " " + btn.Text;
                        }
                    }
                }
            }

            UpdateStatus();
            statsLabel.Text = GetStatsText();
        }

        private string GetArrowDirection(Position from, Position to)
        {
            int rowDiff = to.Row - from.Row;
            int colDiff = to.Col - from.Col;

            if (rowDiff < 0 && colDiff < 0) return "↖";
            if (rowDiff < 0 && colDiff > 0) return "↗";
            if (rowDiff > 0 && colDiff < 0) return "↙";
            if (rowDiff > 0 && colDiff > 0) return "↘";
            return "→";
        }

        private void UpdateStatus()
        {
            switch (game.State)
            {
                case GameState.RedTurn:
                    statusLabel.Text = mode == GameMode.HumanVsAI ? "Your Turn (Red)" : "Red's Turn";
                    statusLabel.ForeColor = Color.FromArgb(200, 0, 0);
                    break;
                case GameState.BlackTurn:
                    statusLabel.Text = mode == GameMode.HumanVsAI ? "AI's Turn (Black)" : "Black's Turn";
                    statusLabel.ForeColor = Color.FromArgb(50, 50, 50);
                    if (mode == GameMode.HumanVsAI)
                        MakeAIMove();
                    break;
                case GameState.RedWins:
                    statusLabel.Text = mode == GameMode.HumanVsAI ? "You Win!" : "Red Wins!";
                    statusLabel.ForeColor = Color.FromArgb(200, 0, 0);
                    MessageBox.Show(statusLabel.Text, "Game Over", MessageBoxButtons.OK);
                    break;
                case GameState.BlackWins:
                    statusLabel.Text = mode == GameMode.HumanVsAI ? "AI Wins!" : "Black Wins!";
                    statusLabel.ForeColor = Color.FromArgb(50, 50, 50);
                    MessageBox.Show(statusLabel.Text, "Game Over", MessageBoxButtons.OK);
                    break;
            }
        }

        private void Square_Click(object sender, EventArgs e)
        {
            if (game.IsGameOver()) return;
            if (mode == GameMode.HumanVsAI && game.State == GameState.BlackTurn) return;

            var btn = (Button)sender;
            var pos = (Position)btn.Tag;

            if (game.GetSelectedPiece() == null)
            {
                if (game.SelectPiece(pos))
                    UpdateBoard();
            }
            else
            {
                Position selectedFrom = game.GetSelectedPiece().Position;

                if (game.MovePiece(pos))
                {
                    // Track last move
                    lastMoveFrom = selectedFrom;
                    lastMoveTo = pos;
                    moveCount++;

                    // Update move history
                    string moveText = $"Move #{moveCount} - ";
                    moveText += game.State == GameState.BlackTurn ? "Red" : "Black";
                    moveText += $" moved {GetMoveDescription(selectedFrom, pos)}";
                    moveHistoryLabel.Text = moveText;

                    UpdateBoard();
                }
                else
                {
                    game.DeselectPiece();
                    if (game.SelectPiece(pos))
                        UpdateBoard();
                }
            }
        }

        private string GetMoveDescription(Position from, Position to)
        {
            string fromCoord = $"{(char)('A' + from.Col)}{8 - from.Row}";
            string toCoord = $"{(char)('A' + to.Col)}{8 - to.Row}";

            int distance = Math.Abs(to.Row - from.Row);
            return distance > 1 ? $"{fromCoord}→{toCoord} (Jump!)" : $"{fromCoord}→{toCoord}";
        }

        private async void MakeAIMove()
        {
            if (aiPlayer == null) return;

            await System.Threading.Tasks.Task.Delay(500);

            var moves = game.GetAllValidMovesForCurrentPlayer();
            if (moves.Count == 0) return;

            var bestMove = aiPlayer.ChooseMove(game.Board, moves, PieceColor.Black);
            if (bestMove != null)
            {
                // Track pieces before move for capture detection
                int blackPiecesBefore = game.Board.GetAllPieces(PieceColor.Black).Count;

                game.SelectPiece(bestMove.From);
                game.MovePiece(bestMove.To);

                // Track last move
                lastMoveFrom = bestMove.From;
                lastMoveTo = bestMove.To;
                moveCount++;

                // Check if red piece was captured
                int redPiecesAfter = game.Board.GetAllPieces(PieceColor.Red).Count;
                if (redPiecesAfter < 12 - redPiecesCaptured)
                {
                    redPiecesCaptured++;
                }

                // Update move history
                string moveText = $"Move #{moveCount} - AI moved {GetMoveDescription(bestMove.From, bestMove.To)}";
                moveHistoryLabel.Text = moveText;

                UpdateBoard();
            }
        }
    }
}