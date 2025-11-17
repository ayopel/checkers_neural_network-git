using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using checkersclaude;

namespace checkersclaude
{
    public class TrainingForm : Form
    {
        private TextBox txtPopulation;
        private TextBox txtGenerations;
        private TextBox txtMutationRate;
        private Button btnStart, btnStop, btnBack;
        private ProgressBar progressBar;
        private Label lblProgress;
        private TextBox txtLog;
        private CheckBox chkShowBattles;
        private Panel boardPanel;
        private bool isTraining = false;
        private Population currentPopulation;

        private const int BoardSize = 8;
        private const int SquareSize = 50;

        public TrainingForm()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Text = "AI Training";
            this.Size = new Size(700, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Population size
            this.Controls.Add(new Label { Text = "Population Size:", Location = new Point(20, 20), Size = new Size(120, 20) });
            txtPopulation = new TextBox { Text = "50", Location = new Point(150, 18), Size = new Size(100, 20) };
            this.Controls.Add(txtPopulation);

            // Generations
            this.Controls.Add(new Label { Text = "Generations:", Location = new Point(20, 50), Size = new Size(120, 20) });
            txtGenerations = new TextBox { Text = "100", Location = new Point(150, 48), Size = new Size(100, 20) };
            this.Controls.Add(txtGenerations);

            // Mutation Rate
            this.Controls.Add(new Label { Text = "Mutation Rate:", Location = new Point(20, 80), Size = new Size(120, 20) });
            txtMutationRate = new TextBox { Text = "0.1", Location = new Point(150, 78), Size = new Size(100, 20) };
            this.Controls.Add(txtMutationRate);

            // Show Battles Checkbox
            chkShowBattles = new CheckBox { Text = "Show AI Battles", Location = new Point(280, 80), Size = new Size(150, 20) };
            this.Controls.Add(chkShowBattles);

            // Buttons
            btnStart = new Button { Text = "Start Training", Location = new Point(280, 18), Size = new Size(120, 30), BackColor = Color.Green, ForeColor = Color.White };
            btnStart.Click += BtnStart_Click;
            this.Controls.Add(btnStart);

            btnStop = new Button { Text = "Stop", Location = new Point(280, 58), Size = new Size(120, 30), BackColor = Color.Red, ForeColor = Color.White, Enabled = false };
            btnStop.Click += BtnStop_Click;
            this.Controls.Add(btnStop);

            btnBack = new Button { Text = "Back to Menu", Location = new Point(420, 18), Size = new Size(120, 30) };
            btnBack.Click += (s, e) => this.Close();
            this.Controls.Add(btnBack);

            // Progress bar & label
            progressBar = new ProgressBar { Location = new Point(20, 120), Size = new Size(540, 25) };
            this.Controls.Add(progressBar);

            lblProgress = new Label { Text = "Ready to train", Location = new Point(20, 150), Size = new Size(540, 20), Font = new Font("Arial", 10, FontStyle.Bold) };
            this.Controls.Add(lblProgress);

            // Log
            txtLog = new TextBox { Location = new Point(20, 180), Size = new Size(540, 150), Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Font = new Font("Consolas", 9) };
            this.Controls.Add(txtLog);

            // Board Panel
            boardPanel = new Panel { Location = new Point(20, 340), Size = new Size(BoardSize * SquareSize, BoardSize * SquareSize) };
            boardPanel.Paint += BoardPanel_Paint;
            this.Controls.Add(boardPanel);
        }

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(txtPopulation.Text, out int popSize) || popSize < 10) { MessageBox.Show("Population size must be at least 10"); return; }
            if (!int.TryParse(txtGenerations.Text, out int generations) || generations < 1) { MessageBox.Show("Generations must be at least 1"); return; }
            if (!double.TryParse(txtMutationRate.Text, out double mutationRate) || mutationRate <= 0 || mutationRate > 1) { MessageBox.Show("Mutation rate must be between 0 and 1"); return; }

            isTraining = true;
            btnStart.Enabled = false; btnStop.Enabled = true; txtPopulation.Enabled = false; txtGenerations.Enabled = false; txtMutationRate.Enabled = false; btnBack.Enabled = false;

            progressBar.Maximum = generations; progressBar.Value = 0;
            txtLog.Clear();

            await Task.Run(() => TrainAI(popSize, generations, mutationRate));

            btnStart.Enabled = true; btnStop.Enabled = false; txtPopulation.Enabled = true; txtGenerations.Enabled = true; txtMutationRate.Enabled = true; btnBack.Enabled = true;
        }

        private void BtnStop_Click(object sender, EventArgs e) { isTraining = false; AppendLog("Training stopped by user."); }

        private void TrainAI(int popSize, int generations, double mutationRate)
        {
            currentPopulation = new Population(popSize, mutationRate);
            SafeAppendLog($"Starting training: Pop={popSize}, Gen={generations}, Mutation={mutationRate:F2}\n");

            for (int gen = 0; gen < generations && isTraining; gen++)
            {
                SafeUpdateProgress($"Generation {gen + 1}/{generations} - Running tournament...", gen);

                // Run tournament with optional UI updates
                currentPopulation.RunTournament(
                    gamesPerPair: 2,
                    onMove: chkShowBattles.Checked ? (Action<Board, Move>)((board, move) =>
                    {
                        // Update UI every few moves to prevent slowdowns
                        SafeInvoke(() =>
                        {
                            boardPanel.Tag = board;
                            boardPanel.Invalidate();
                        });
                    }) : null
                );

                string stats = currentPopulation.GetGenerationStats();
                SafeAppendLog($"Gen {gen + 1}: {stats}");

                currentPopulation.Evolve();
            }

            if (isTraining)
            {
                SafeUpdateProgress($"Training complete! Saving best AI...", generations);
                MainMenuForm.SaveAI(currentPopulation.BestPlayer);
                SafeAppendLog($"\n✓ Training complete! Best fitness: {currentPopulation.BestPlayer.Fitness:F2}");
            }
        }

        // =======================
        // Safe UI helpers
        // =======================
        private void SafeAppendLog(string message)
        {
            SafeInvoke(() => txtLog.AppendText(message + Environment.NewLine));
        }

        private void SafeUpdateProgress(string message, int value)
        {
            SafeInvoke(() =>
            {
                lblProgress.Text = message;
                progressBar.Value = Math.Min(value, progressBar.Maximum);
            });
        }

        private void SafeInvoke(Action action)
        {
            if (InvokeRequired)
            {
                try
                {
                    Invoke(action);
                }
                catch { /* Ignore if form is closing */ }
            }
            else
            {
                action();
            }
        }




        private void BoardPanel_Paint(object sender, PaintEventArgs e)
        {
            Board board = boardPanel.Tag as Board;
            if (board == null) return;

            Graphics g = e.Graphics;

            for (int r = 0; r < BoardSize; r++)
            {
                for (int c = 0; c < BoardSize; c++)
                {
                    bool isDark = (r + c) % 2 == 1;
                    Brush squareBrush = isDark ? Brushes.SaddleBrown : Brushes.Beige;
                    g.FillRectangle(squareBrush, c * SquareSize, r * SquareSize, SquareSize, SquareSize);

                    Piece piece = board.GetPiece(new Position(r, c));
                    if (piece != null)
                    {
                        Brush pieceBrush = piece.Color == PieceColor.Red ? Brushes.Red : Brushes.Black;
                        g.FillEllipse(pieceBrush, c * SquareSize + 5, r * SquareSize + 5, SquareSize - 10, SquareSize - 10);
                        if (piece.Type == PieceType.King)
                        {
                            g.DrawEllipse(Pens.Gold, c * SquareSize + 10, r * SquareSize + 10, SquareSize - 20, SquareSize - 20);
                        }
                    }
                }
            }
        }


        private void UpdateProgress(string message, int value) { this.Invoke((Action)(() => { lblProgress.Text = message; progressBar.Value = Math.Min(value, progressBar.Maximum); })); }
        private void AppendLog(string message)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke((Action)(() => txtLog.AppendText(message + Environment.NewLine)));
            }
            else
            {
                txtLog.AppendText(message + Environment.NewLine);
            }
        }

    }
}
