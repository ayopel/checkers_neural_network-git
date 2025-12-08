using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
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
        private Button hintButton;
        private CheckBox chkAnalysisMode;
        private ComboBox cmbDifficulty;

        private readonly GameMode mode;
        private readonly AIPlayer aiPlayer;

        private const int SquareSize = 70;
        private Position? lastMoveFrom;
        private Position? lastMoveTo;
        private Position? hintMove;
        private int moveCount = 0;
        private int redPiecesCaptured = 0;
        private int blackPiecesCaptured = 0;
        private bool analysisMode = false;

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
            ClientSize = new Size(SquareSize * 8 + 260, SquareSize * 8 + 100);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(240, 240, 245);

            // Status label
            statusLabel = new Label
            {
                Location = new Point(10, SquareSize * 8 + 10),
                Size = new Size(400, 30),
                Font = new Font("Arial", 14, FontStyle.Bold),
                Text = mode == GameMode.HumanVsAI ? "Your Turn (Red)" : "Red's Turn"
            };
            Controls.Add(statusLabel);

            // Move history label
            moveHistoryLabel = new Label
            {
                Location = new Point(10, SquareSize * 8 + 50),
                Size = new Size(500, 25),
                Font = new Font("Arial", 9),
                Text = "Move #0 - Game Start",
                ForeColor = Color.Gray
            };
            Controls.Add(moveHistoryLabel);

            // Side panel
            Panel sidePanel = new Panel
            {
                Location = new Point(SquareSize * 8 + 10, 10),
                Size = new Size(240, SquareSize * 8 + 80),
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

            // Hint button (רק במצב מול AI)
            if (mode == GameMode.HumanVsAI && aiPlayer != null)
            {
                hintButton = new Button
                {
                    Location = new Point(10, 300),
                    Size = new Size(220, 40),
                    Text = "💡 Get Hint",
                    Font = new Font("Arial", 10, FontStyle.Bold),
                    BackColor = Color.FromArgb(23, 162, 184),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Enabled = true
                };
                hintButton.FlatAppearance.BorderSize = 0;
                hintButton.Click += HintButton_Click;
                sidePanel.Controls.Add(hintButton);

                // Analysis mode checkbox
                chkAnalysisMode = new CheckBox
                {
                    Text = "Analysis Mode",
                    Location = new Point(10, 350),
                    Size = new Size(220, 20),
                    Checked = false,
                    Font = new Font("Arial", 9)
                };
                chkAnalysisMode.CheckedChanged += (s, e) => analysisMode = chkAnalysisMode.Checked;
                sidePanel.Controls.Add(chkAnalysisMode);

                // Difficulty selector
                Label lblDifficulty = new Label
                {
                    Text = "AI Difficulty:",
                    Location = new Point(10, 380),
                    Size = new Size(220, 18),
                    Font = new Font("Arial", 9, FontStyle.Bold)
                };
                sidePanel.Controls.Add(lblDifficulty);

                cmbDifficulty = new ComboBox
                {
                    Location = new Point(10, 400),
                    Size = new Size(220, 25),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new Font("Arial", 9)
                };
                cmbDifficulty.Items.AddRange(new object[] { "Easy", "Medium", "Hard", "Expert" });
                cmbDifficulty.SelectedIndex = 2; // Default to Hard
                cmbDifficulty.SelectedIndexChanged += CmbDifficulty_SelectedIndexChanged;
                sidePanel.Controls.Add(cmbDifficulty);
            }

            // Game info
            int infoY = mode == GameMode.HumanVsAI ? 435 : 300;
            Label infoLabel = new Label
            {
                Location = new Point(10, infoY),
                Size = new Size(220, 140),
                Font = new Font("Arial", 8),
                Text = mode == GameMode.HumanVsAI ?
                    "🎮 Playing vs AI\n\n" +
                    "• You are Red (●)\n" +
                    "• AI is Black (●)\n" +
                    "• Click to select piece\n" +
                    "• Click again to move\n" +
                    "• Must jump when\n  possible\n" +
                    "• Use hints for help" :
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
            int legendY = mode == GameMode.HumanVsAI ? 580 : 450;
            Panel legendPanel = new Panel
            {
                Location = new Point(10, legendY),
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

        private void CmbDifficulty_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (aiPlayer == null) return;

            switch (cmbDifficulty.SelectedIndex)
            {
                case 0: aiPlayer.Difficulty = DifficultyLevel.Easy; break;
                case 1: aiPlayer.Difficulty = DifficultyLevel.Medium; break;
                case 2: aiPlayer.Difficulty = DifficultyLevel.Hard; break;
                case 3: aiPlayer.Difficulty = DifficultyLevel.Expert; break;
            }
        }

        private void HintButton_Click(object sender, EventArgs e)
        {
            if (game.IsGameOver() || game.State != GameState.RedTurn) return;

            var moves = game.GetAllValidMovesForCurrentPlayer();
            if (moves.Count > 0 && aiPlayer != null)
            {
                // זמנית שנה את הקושי ל-Expert כדי לקבל רמז טוב
                var originalDifficulty = aiPlayer.Difficulty;
                aiPlayer.Difficulty = DifficultyLevel.Expert;

                var bestMove = aiPlayer.ChooseMove(game.Board, moves, PieceColor.Red);

                aiPlayer.Difficulty = originalDifficulty;

                if (bestMove != null)
                {
                    hintMove = bestMove.From;
                    UpdateBoard();
                    MessageBox.Show(
                        $"Hint: Consider moving the piece at {GetCoordinate(bestMove.From)} to {GetCoordinate(bestMove.To)}",
                        "AI Suggestion",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }

        private void UndoButton_Click(object sender, EventArgs e)
        {
            // In AI mode, undo the last two moves (AI's move and your move)
            if (mode == GameMode.HumanVsAI)
            {
                if (game.CanUndo())
                {
                    game.UndoMove(); // Undo AI's move
                    if (game.CanUndo())
                    {
                        game.UndoMove(); // Undo your move
                        moveCount = Math.Max(0, moveCount - 2);
                    }
                }
            }
            else
            {
                // In human vs human mode, undo just one move
                if (game.UndoMove())
                {
                    moveCount = Math.Max(0, moveCount - 1);
                }
            }

            lastMoveFrom = null;
            lastMoveTo = null;
            hintMove = null;
            moveHistoryLabel.Text = $"Move #{moveCount} - Undone";
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
                hintMove = null;
                moveCount = 0;
                redPiecesCaptured = 0;
                blackPiecesCaptured = 0;
                undoButton.Enabled = false;
                moveHistoryLabel.Text = "Move #0 - Game Start";
                UpdateBoard();
            }
        }

        private string GetStatsText()
        {
            int redPieces = game.Board.GetAllPieces(PieceColor.Red).Count;
            int blackPieces = game.Board.GetAllPieces(PieceColor.Black).Count;
            int redKings = game.Board.GetAllPieces(PieceColor.Red).FindAll(p => p.Type == PieceType.King).Count;
            int blackKings = game.Board.GetAllPieces(PieceColor.Black).FindAll(p => p.Type == PieceType.King).Count;

            return $"📊 Game Statistics\n" +
                   $"━━━━━━━━━━━━━━\n" +
                   $"Move #{moveCount}\n\n" +
                   $"🔴 Red Pieces: {redPieces}\n" +
                   $"    Kings: {redKings}\n" +
                   $"    Captured: {blackPiecesCaptured}\n\n" +
                   $"⚫ Black Pieces: {blackPieces}\n" +
                   $"    Kings: {blackKings}\n" +
                   $"    Captured: {redPiecesCaptured}";
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

                    // Show last move
                    if (lastMoveFrom.HasValue && lastMoveFrom.Value == pos)
                    {
                        btn.BackColor = Color.FromArgb(255, 200, 150);
                    }

                    if (lastMoveTo.HasValue && lastMoveTo.Value == pos)
                    {
                        btn.BackColor = Color.FromArgb(255, 165, 100);
                    }

                    // Highlight hint
                    if (hintMove.HasValue && hintMove.Value == pos)
                    {
                        btn.BackColor = Color.FromArgb(135, 206, 250);
                        btn.FlatAppearance.BorderSize = 3;
                        btn.FlatAppearance.BorderColor = Color.Blue;
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
                    if (mode == GameMode.HumanVsAI)
                        MakeAIMove();
                    break;
                case GameState.RedWins:
                    statusLabel.Text = mode == GameMode.HumanVsAI ? "🎉 You Win!" : "🏆 Red Wins!";
                    statusLabel.ForeColor = Color.FromArgb(200, 0, 0);
                    undoButton.Enabled = false;
                    ShowGameOverDialog("Red Wins!", $"Congratulations! Red won in {moveCount} moves.");
                    break;
                case GameState.BlackWins:
                    statusLabel.Text = mode == GameMode.HumanVsAI ? "😞 AI Wins!" : "🏆 Black Wins!";
                    statusLabel.ForeColor = Color.FromArgb(50, 50, 50);
                    undoButton.Enabled = false;
                    ShowGameOverDialog("Black Wins!", $"Game Over! Black won in {moveCount} moves.");
                    break;
            }

            if (!game.IsGameOver())
            {
                undoButton.Enabled = game.CanUndo();
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
                hintMove = null;
                moveCount = 0;
                redPiecesCaptured = 0;
                blackPiecesCaptured = 0;
                undoButton.Enabled = false;
                moveHistoryLabel.Text = "Move #0 - Game Start";
                UpdateBoard();
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
                {
                    hintMove = null; // נקה רמז אחרי בחירת כלי
                    UpdateBoard();
                }
            }
            else
            {
                Position selectedFrom = game.GetSelectedPiece().Position;

                if (game.MovePiece(pos))
                {
                    lastMoveFrom = selectedFrom;
                    lastMoveTo = pos;
                    hintMove = null;
                    moveCount++;

                    string moveText = $"Move #{moveCount} - ";
                    moveText += game.State == GameState.BlackTurn ? "Red" : "Black";
                    moveText += $" moved {GetMoveDescription(selectedFrom, pos)}";
                    moveHistoryLabel.Text = moveText;

                    undoButton.Enabled = true;

                    UpdateBoard();

                    // מצב ניתוח
                    if (analysisMode && aiPlayer != null && !game.IsGameOver())
                    {
                        System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                        {
                            if (InvokeRequired)
                                Invoke((Action)ShowMoveAnalysis);
                            else
                                ShowMoveAnalysis();
                        });
                    }
                }
                else
                {
                    game.DeselectPiece();
                    if (game.SelectPiece(pos))
                    {
                        hintMove = null;
                        UpdateBoard();
                    }
                }
            }
        }

        private void ShowMoveAnalysis()
        {
            if (game.IsGameOver()) return;

            var moves = game.GetAllValidMovesForCurrentPlayer();
            if (moves.Count == 0) return;

            PieceColor currentColor = game.GetCurrentTurnColor();

            var evaluations = new List<Tuple<Move, double>>();
            foreach (var move in moves)
            {
                double score = aiPlayer.EvaluateMove(game.Board, move, currentColor);
                evaluations.Add(new Tuple<Move, double>(move, score));
            }

            evaluations = evaluations.OrderByDescending(e => e.Item2).ToList();

            string analysis = "Top 5 Moves:\n\n";
            for (int i = 0; i < Math.Min(5, evaluations.Count); i++)
            {
                var eval = evaluations[i];
                analysis += $"{i + 1}. {GetMoveDescription(eval.Item1.From, eval.Item1.To)}\n";
                analysis += $"   Score: {eval.Item2:F2}\n\n";
            }

            MessageBox.Show(analysis, "Move Analysis", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string GetMoveDescription(Position from, Position to)
        {
            string fromCoord = GetCoordinate(from);
            string toCoord = GetCoordinate(to);

            int distance = Math.Abs(to.Row - from.Row);
            return distance > 1 ? $"{fromCoord}→{toCoord} (Jump!)" : $"{fromCoord}→{toCoord}";
        }

        private string GetCoordinate(Position pos)
        {
            return $"{(char)('A' + pos.Col)}{8 - pos.Row}";
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
                game.SelectPiece(bestMove.From);
                game.MovePiece(bestMove.To);

                lastMoveFrom = bestMove.From;
                lastMoveTo = bestMove.To;
                moveCount++;

                int redPiecesAfter = game.Board.GetAllPieces(PieceColor.Red).Count;
                if (redPiecesAfter < 12 - redPiecesCaptured)
                {
                    redPiecesCaptured++;
                }

                string moveText = $"Move #{moveCount} - AI moved {GetMoveDescription(bestMove.From, bestMove.To)}";
                moveHistoryLabel.Text = moveText;

                UpdateBoard();
            }
        }
    }
}