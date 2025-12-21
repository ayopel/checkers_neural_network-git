using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace checkers_neural_network
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
        private const int BoardSize = 8;
        private Position? lastMoveFrom;
        private Position? lastMoveTo;
        private bool isAIThinking = false;

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
            ClientSize = new Size(SquareSize * BoardSize + 260, SquareSize * BoardSize + 100);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(240, 240, 245);

            // Status label
            statusLabel = new Label
            {
                Location = new Point(10, SquareSize * BoardSize + 10),
                Size = new Size(400, 30),
                Font = new Font("Arial", 14, FontStyle.Bold),
                Text = mode == GameMode.HumanVsAI ? "Your Turn (Red)" : "Red's Turn"
            };
            Controls.Add(statusLabel);

            // Move history label
            moveHistoryLabel = new Label
            {
                Location = new Point(10, SquareSize * BoardSize + 50),
                Size = new Size(500, 25),
                Font = new Font("Arial", 9),
                Text = "Move #0 - Game Start",
                ForeColor = Color.Gray
            };
            Controls.Add(moveHistoryLabel);

            // Side panel
            Panel sidePanel = new Panel
            {
                Location = new Point(SquareSize * BoardSize + 10, 10),
                Size = new Size(240, SquareSize * BoardSize + 80),
                BackColor = Color.FromArgb(250, 250, 250),
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true
            };
            Controls.Add(sidePanel);

            // Stats label
            statsLabel = new Label
            {
                Location = new Point(10, 10),
                Size = new Size(220, 180),
                Font = new Font("Arial", 9),
                Text = GetStatsText(),
                BackColor = Color.Transparent
            };
            sidePanel.Controls.Add(statsLabel);

            // Reset button
            resetButton = new Button
            {
                Location = new Point(10, 200),
                Size = new Size(220, 40),
                Text = "🔄 New Game",
                Font = new Font("Arial", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            resetButton.FlatAppearance.BorderSize = 0;
            resetButton.Click += ResetButton_Click;
            sidePanel.Controls.Add(resetButton);

            // Undo button
            undoButton = new Button
            {
                Location = new Point(10, 250),
                Size = new Size(220, 40),
                Text = "↶ Undo Move",
                Font = new Font("Arial", 10, FontStyle.Bold),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            undoButton.FlatAppearance.BorderSize = 0;
            undoButton.Click += UndoButton_Click;
            sidePanel.Controls.Add(undoButton);

            // Game info
            Label infoLabel = new Label
            {
                Location = new Point(10, 300),
                Size = new Size(220, 140),
                Font = new Font("Arial", 8),
                Text = mode == GameMode.HumanVsAI ?
                    "🎮 Playing vs AI\n\n" +
                    "• You are Red (●)\n" +
                    "• AI is Black (●)\n" +
                    "• Click to select piece\n" +
                    "• Click again to move\n" +
                    "• Must jump when\n  possible\n" +
                    "• Undo only works\n  on your moves" :
                    "👥 Two Player Mode\n\n" +
                    "• Red goes first\n" +
                    "• Click to select piece\n" +
                    "• Click again to move\n" +
                    "• Must jump when\n  possible\n" +
                    "• Press Undo to take\n  back last move",
                ForeColor = Color.DarkSlateGray,
                BackColor = Color.Transparent
            };
            sidePanel.Controls.Add(infoLabel);

            // Legend
            Panel legendPanel = new Panel
            {
                Location = new Point(10, 450),
                Size = new Size(220, 110),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            sidePanel.Controls.Add(legendPanel);

            Label legendTitle = new Label
            {
                Location = new Point(5, 5),
                Size = new Size(210, 18),
                Text = "Legend:",
                Font = new Font("Arial", 9, FontStyle.Bold)
            };
            legendPanel.Controls.Add(legendTitle);

            CreateLegendItem(legendPanel, "● = Regular", Color.Black, 28);
            CreateLegendItem(legendPanel, "♔ = King", Color.Black, 48);
            CreateLegendItem(legendPanel, "🟡 = Selected", Color.FromArgb(255, 215, 0), 68);
            CreateLegendItem(legendPanel, "🟢 = Valid Move", Color.FromArgb(50, 205, 50), 88);
        }

        private void UndoButton_Click(object sender, EventArgs e)
        {
            if (isAIThinking) return;

            if (mode == GameMode.HumanVsAI)
            {
                // Undo AI's move and your move
                if (game.CanUndo())
                {
                    game.UndoMove(); // Undo AI's move
                    if (game.CanUndo())
                    {
                        game.UndoMove(); // Undo your move
                    }
                }
            }
            else
            {
                // In human vs human mode, undo just one move
                game.UndoMove();
            }

            lastMoveFrom = null;
            lastMoveTo = null;

            var stats = game.GetGameStats();
            moveHistoryLabel.Text = $"Move #{stats.MoveCount} - Undone";

            UpdateBoard();
        }

        private void CreateLegendItem(Panel parent, string text, Color color, int y)
        {
            Label item = new Label
            {
                Location = new Point(10, y),
                Size = new Size(200, 18),
                Text = text,
                Font = new Font("Arial", 8),
                ForeColor = color,
                AutoSize = false
            };
            parent.Controls.Add(item);
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            if (isAIThinking) return;

            var result = MessageBox.Show(
                "Are you sure you want to start a new game?\nCurrent progress will be lost.",
                "New Game",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                game.ResetGame();
                lastMoveFrom = null;
                lastMoveTo = null;
                undoButton.Enabled = false;

                moveHistoryLabel.Text = "Move #0 - Game Start";
                UpdateBoard();
            }
        }

        private string GetStatsText()
        {
            var stats = game.GetGameStats();

            return $"📊 Game Statistics\n" +
                   $"━━━━━━━━━━━━━━\n" +
                   $"Move #{stats.MoveCount}\n\n" +
                   $"🔴 Red Pieces: {stats.RedPieces}\n" +
                   $"    Kings: {stats.RedKings}\n" +
                   $"    Captured: {12 - stats.RedPieces}\n\n" +
                   $"⚫ Black Pieces: {stats.BlackPieces}\n" +
                   $"    Kings: {stats.BlackKings}\n" +
                   $"    Captured: {12 - stats.BlackPieces}";
        }

        private void CreateBoard()
        {
            boardButtons = new Button[BoardSize, BoardSize];

            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
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
            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
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

                    // Show last move with colored squares
                    if (lastMoveFrom.HasValue && lastMoveFrom.Value == pos)
                    {
                        btn.BackColor = Color.FromArgb(255, 200, 150); // Light orange
                    }

                    if (lastMoveTo.HasValue && lastMoveTo.Value == pos)
                    {
                        btn.BackColor = Color.FromArgb(255, 165, 100); // Darker orange
                    }
                }
            }

            UpdateStatus();
            statsLabel.Text = GetStatsText();
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
                    if (mode == GameMode.HumanVsAI && !isAIThinking)
                        MakeAIMove();
                    break;
                case GameState.RedWins:
                    statusLabel.Text = mode == GameMode.HumanVsAI ? "🎉 You Win!" : "🏆 Red Wins!";
                    statusLabel.ForeColor = Color.FromArgb(200, 0, 0);
                    undoButton.Enabled = false;
                    ShowGameOverDialog("Red Wins!", $"Congratulations! Red won in {game.GetGameStats().MoveCount} moves.");
                    break;
                case GameState.BlackWins:
                    statusLabel.Text = mode == GameMode.HumanVsAI ? "😞 AI Wins!" : "🏆 Black Wins!";
                    statusLabel.ForeColor = Color.FromArgb(50, 50, 50);
                    undoButton.Enabled = false;
                    ShowGameOverDialog("Black Wins!", $"Game Over! Black won in {game.GetGameStats().MoveCount} moves.");
                    break;
            }

            // Update undo button state
            if (!game.IsGameOver())
            {
                undoButton.Enabled = game.CanUndo() && !isAIThinking;
            }
        }

        private void ShowGameOverDialog(string title, string message)
        {
            var result = MessageBox.Show(
                message + "\n\nWould you like to play again?",
                title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                game.ResetGame();
                lastMoveFrom = null;
                lastMoveTo = null;
                undoButton.Enabled = false;

                moveHistoryLabel.Text = "Move #0 - Game Start";
                UpdateBoard();
            }
        }

        private void Square_Click(object sender, EventArgs e)
        {
            if (game.IsGameOver()) return;
            if (isAIThinking) return;
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

                    // Update move history
                    var stats = game.GetGameStats();
                    string moveText = $"Move #{stats.MoveCount} - ";
                    moveText += game.State == GameState.BlackTurn ? "Red" : "Black";
                    moveText += $" moved {GetMoveDescription(selectedFrom, pos)}";
                    moveHistoryLabel.Text = moveText;

                    // Enable undo button after first move
                    undoButton.Enabled = true;

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
            string fromCoord = $"{(char)('A' + from.Col)}{BoardSize - from.Row}";
            string toCoord = $"{(char)('A' + to.Col)}{BoardSize - to.Row}";

            int distance = Math.Abs(to.Row - from.Row);
            return distance > 1 ? $"{fromCoord}→{toCoord} (Jump!)" : $"{fromCoord}→{toCoord}";
        }

        private async void MakeAIMove()
        {
            if (aiPlayer == null) return;
            if (isAIThinking) return;

            isAIThinking = true;
            undoButton.Enabled = false;

            // Disable board during AI turn
            SetBoardEnabled(false);

            try
            {
                await System.Threading.Tasks.Task.Delay(300);

                // AI may need to make multiple jumps
                bool continueTurn = true;
                Position? firstMoveFrom = null;
                Position? lastTo = null;
                int jumpCount = 0;

                while (continueTurn && !game.IsGameOver())
                {
                    var moves = game.GetAllValidMovesForCurrentPlayer();
                    if (moves.Count == 0) break;

                    var bestMove = aiPlayer.ChooseMove(game.Board, moves, PieceColor.Black);
                    if (bestMove == null) break;

                    // Track the first move's from position
                    if (!firstMoveFrom.HasValue)
                        firstMoveFrom = bestMove.From;

                    lastTo = bestMove.To;

                    game.SelectPiece(bestMove.From);
                    game.MovePiece(bestMove.To);

                    if (bestMove.IsJump)
                        jumpCount++;

                    // Update board to show intermediate jumps
                    UpdateBoard();
                    await System.Threading.Tasks.Task.Delay(300);

                    // Check if AI must continue jumping (still its turn and has jumps)
                    if (game.State == GameState.BlackTurn)
                    {
                        var nextMoves = game.GetAllValidMovesForCurrentPlayer();
                        // Check if there are any jump moves available for the same piece
                        bool hasMoreJumps = false;
                        foreach (var move in nextMoves)
                        {
                            if (move.IsJump && move.From == lastTo)
                            {
                                hasMoreJumps = true;
                                break;
                            }
                        }
                        continueTurn = hasMoreJumps;
                    }
                    else
                    {
                        continueTurn = false;
                    }
                }

                // Track last move
                if (firstMoveFrom.HasValue && lastTo.HasValue)
                {
                    lastMoveFrom = firstMoveFrom;
                    lastMoveTo = lastTo;

                    // Update move history
                    var stats = game.GetGameStats();
                    string moveText = $"Move #{stats.MoveCount} - AI moved {GetMoveDescription(firstMoveFrom.Value, lastTo.Value)}";
                    if (jumpCount > 1)
                        moveText += $" ({jumpCount} jumps!)";
                    moveHistoryLabel.Text = moveText;
                }

                UpdateBoard();
            }
            finally
            {
                isAIThinking = false;
                SetBoardEnabled(true);

                // Re-enable undo if game is not over
                if (!game.IsGameOver() && game.CanUndo())
                {
                    undoButton.Enabled = true;
                }
            }
        }

        private void SetBoardEnabled(bool enabled)
        {
            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    boardButtons[row, col].Enabled = enabled;
                }
            }
        }
    }
}