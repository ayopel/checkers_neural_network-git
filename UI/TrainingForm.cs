using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace checkersclaude
{
    public class TrainingForm : Form
    {
        private NumericUpDown numPopulation, numGenerations, numMutationRate, numElitePercent, numOpponentsPerPlayer;
        private CheckBox chkParallel;
        private Button btnStart, btnPause, btnStop, btnBack;
        private ProgressBar progressBar;
        private Label lblProgress;
        private TextBox txtLog;
        private Panel chartPanel;

        private bool isTraining = false;
        private bool isPaused = false;
        private TrainingSystem trainingSystem;
        private List<double> fitnessHistory = new List<double>();
        private List<double> avgFitnessHistory = new List<double>();

        public TrainingForm()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            Text = "AI Training - Deep Learning System";
            Size = new Size(900, 710);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Color.FromArgb(240, 240, 245);

            Label lblTitle = new Label
            {
                Text = "Neural Network Training",
                Font = new Font("Arial", 16, FontStyle.Bold),
                Location = new Point(20, 10),
                Size = new Size(860, 30),
                ForeColor = Color.FromArgb(0, 120, 215)
            };
            Controls.Add(lblTitle);

            CreateConfigurationPanel();
            CreateControlPanel();
            CreateVisualizationPanel();
        }

        private void CreateConfigurationPanel()
        {
            GroupBox configGroup = new GroupBox
            {
                Text = "Training Configuration",
                Location = new Point(20, 50),
                Size = new Size(420, 280),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            AddConfigControl(configGroup, "Population Size:", out numPopulation, 15, 30, 10, 200, 50, 10);
            AddConfigControl(configGroup, "Generations:", out numGenerations, 15, 60, 1, 1000, 100, 10);
            AddConfigControl(configGroup, "Mutation Rate:", out numMutationRate, 15, 90, 0.01M, 0.5M, 0.1M, 0.01M, 2);
            AddConfigControl(configGroup, "Elite %:", out numElitePercent, 15, 120, 0.05M, 0.3M, 0.1M, 0.05M, 2);
            AddConfigControl(configGroup, "Opponents Per Player:", out numOpponentsPerPlayer, 15, 150, 3, 20, 5, 1);

            chkParallel = new CheckBox
            {
                Text = "Use Parallel Processing (Faster)",
                Location = new Point(15, 180),
                Size = new Size(250, 20),
                Checked = true
            };
            configGroup.Controls.Add(chkParallel);

            Label lblInfo = new Label
            {
                Location = new Point(15, 210),
                Size = new Size(390, 60),
                Font = new Font("Arial", 8),
                ForeColor = Color.Gray,
                Text = "• Higher population = better diversity\n" +
                       "• More generations = better training\n" +
                       "• Parallel processing uses multiple CPU cores\n" +
                       "• Recommended: Pop=50, Gen=100 for good AI"
            };
            configGroup.Controls.Add(lblInfo);

            Controls.Add(configGroup);
        }

        private void AddConfigControl(GroupBox parent, string label, out NumericUpDown control,
            int labelX, int y, decimal min, decimal max, decimal value, decimal increment, int decimals = 0)
        {
            Label lbl = new Label { Text = label, Location = new Point(labelX, y), Size = new Size(150, 20) };
            control = new NumericUpDown
            {
                Location = new Point(170, y - 2),
                Size = new Size(100, 20),
                Minimum = min,
                Maximum = max,
                Value = value,
                Increment = increment,
                DecimalPlaces = decimals
            };
            parent.Controls.Add(lbl);
            parent.Controls.Add(control);
        }

        private void CreateControlPanel()
        {
            GroupBox controlGroup = new GroupBox
            {
                Text = "Controls",
                Location = new Point(460, 50),
                Size = new Size(400, 280),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            btnStart = CreateControlButton("Start Training", 20, 30, 160, 40, Color.FromArgb(0, 150, 0), BtnStart_Click);
            btnPause = CreateControlButton("Pause", 200, 30, 160, 40, Color.FromArgb(255, 165, 0), BtnPause_Click, false);
            btnStop = CreateControlButton("Stop & Save", 20, 80, 160, 40, Color.FromArgb(220, 53, 69), BtnStop_Click, false);
            btnBack = CreateControlButton("Back to Menu", 200, 80, 160, 40, Color.Gray, (s, e) => Close());

            controlGroup.Controls.AddRange(new Control[] { btnStart, btnPause, btnStop, btnBack });

            progressBar = new ProgressBar
            {
                Location = new Point(20, 140),
                Size = new Size(360, 25),
                Style = ProgressBarStyle.Continuous
            };
            controlGroup.Controls.Add(progressBar);

            lblProgress = new Label
            {
                Text = "Ready to train",
                Location = new Point(20, 170),
                Size = new Size(360, 20),
                Font = new Font("Arial", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 215)
            };
            controlGroup.Controls.Add(lblProgress);

            Label lblStats = new Label
            {
                Location = new Point(20, 200),
                Size = new Size(360, 70),
                Font = new Font("Arial", 8),
                ForeColor = Color.DarkSlateGray,
                Text = "Neural Network Architecture:\n" +
                       "• Input Layer: 64 neurons (board state)\n" +
                       "• Hidden Layers: 128 → 64 → 32 neurons\n" +
                       "• Output Layer: 1 neuron (evaluation)\n\n" +
                       "Training: Genetic Algorithm + Self-Play"
            };
            controlGroup.Controls.Add(lblStats);

            Controls.Add(controlGroup);
        }

        private Button CreateControlButton(string text, int x, int y, int width, int height, Color color, EventHandler click, bool enabled = true)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = color,
                ForeColor = Color.White,
                Font = new Font("Arial", 11, FontStyle.Bold),
                Enabled = enabled,
                FlatStyle = FlatStyle.Flat
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += click;
            return btn;
        }

        private void CreateVisualizationPanel()
        {
            GroupBox logGroup = new GroupBox
            {
                Text = "Training Log",
                Location = new Point(20, 340),
                Size = new Size(420, 320),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            txtLog = new TextBox
            {
                Location = new Point(10, 25),
                Size = new Size(400, 285),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.LimeGreen
            };
            logGroup.Controls.Add(txtLog);
            Controls.Add(logGroup);

            GroupBox chartGroup = new GroupBox
            {
                Text = "Fitness Progress",
                Location = new Point(460, 340),
                Size = new Size(400, 320),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };

            chartPanel = new Panel
            {
                Location = new Point(10, 25),
                Size = new Size(380, 285),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            chartPanel.Paint += ChartPanel_Paint;
            chartGroup.Controls.Add(chartPanel);
            Controls.Add(chartGroup);
        }

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            if (isPaused)
            {
                isPaused = false;
                btnPause.Text = "Pause";
                return;
            }

            var config = new TrainingConfig
            {
                PopulationSize = (int)numPopulation.Value,
                MutationRate = (double)numMutationRate.Value,
                ElitePercentage = (double)numElitePercent.Value,
                OpponentsPerPlayer = (int)numOpponentsPerPlayer.Value,
                UseParallelProcessing = chkParallel.Checked
            };

            isTraining = true;
            isPaused = false;
            fitnessHistory.Clear();
            avgFitnessHistory.Clear();

            SetControlsEnabled(false);
            progressBar.Maximum = (int)numGenerations.Value;
            progressBar.Value = 0;
            txtLog.Clear();

            AppendLog("=== Training Started ===");
            AppendLog($"Configuration: Pop={config.PopulationSize}, Mut={config.MutationRate:F2}, Elite={config.ElitePercentage:P0}");
            AppendLog($"Parallel Processing: {(config.UseParallelProcessing ? "Enabled" : "Disabled")}");
            AppendLog("");

            await Task.Run(() => RunTraining(config, (int)numGenerations.Value));

            SetControlsEnabled(true);
        }

        private void SetControlsEnabled(bool training)
        {
            btnStart.Enabled = !training;
            btnPause.Enabled = training;
            btnStop.Enabled = training;
            numPopulation.Enabled = !training;
            numGenerations.Enabled = !training;
            numMutationRate.Enabled = !training;
            numElitePercent.Enabled = !training;
            numOpponentsPerPlayer.Enabled = !training;
            chkParallel.Enabled = !training;
        }

        private void BtnPause_Click(object sender, EventArgs e)
        {
            isPaused = !isPaused;
            btnPause.Text = isPaused ? "Resume" : "Pause";
            AppendLog(isPaused ? "Training paused" : "Training resumed");
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            isTraining = false;
            AppendLog("Training stopped by user");

            if (trainingSystem?.BestPlayer != null)
                SaveBestPlayer();
        }

        private void RunTraining(TrainingConfig config, int generations)
        {
            trainingSystem = new TrainingSystem(config);

            for (int gen = 0; gen < generations && isTraining; gen++)
            {
                while (isPaused && isTraining)
                    System.Threading.Thread.Sleep(100);

                if (!isTraining) break;

                UpdateProgress($"Generation {gen + 1}/{generations} - Running tournament...", gen);

                trainingSystem.RunGeneration();

                string report = trainingSystem.GetGenerationReport();
                AppendLog(report);

                fitnessHistory.Add(trainingSystem.CurrentStats.BestFitness);
                avgFitnessHistory.Add(trainingSystem.CurrentStats.AverageFitness);

                UpdateChart();
            }

            if (isTraining)
            {
                UpdateProgress("Training complete!", generations);
                SaveBestPlayer();
                AppendLog("");
                AppendLog("=== Training Complete ===");
                AppendLog($"Best Fitness: {trainingSystem.BestPlayer.Brain.Fitness:F2}");
                AppendLog($"Best Win Rate: {trainingSystem.BestPlayer.Stats.WinRate:P1}");
                AppendLog("AI saved successfully!");

                Invoke((Action)(() =>
                {
                    MessageBox.Show(
                        $"Training Complete!\n\n" +
                        $"Generations: {trainingSystem.Generation}\n" +
                        $"Best Fitness: {trainingSystem.BestPlayer.Brain.Fitness:F2}\n" +
                        $"Best Win Rate: {trainingSystem.BestPlayer.Stats.WinRate:P1}\n\n" +
                        $"AI saved and ready to play!",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }));
            }
        }

        private void SaveBestPlayer()
        {
            try
            {
                string filename = "best_checkers_ai.dat";
                trainingSystem.BestPlayer.Brain.SaveToFile(filename);
                AppendLog($"Best AI saved to {filename}");
            }
            catch (Exception ex)
            {
                AppendLog($"Error saving AI: {ex.Message}");
            }
        }

        private void UpdateProgress(string message, int value)
        {
            if (InvokeRequired)
            {
                Invoke((Action)(() => UpdateProgress(message, value)));
                return;
            }

            lblProgress.Text = message;
            progressBar.Value = Math.Min(value, progressBar.Maximum);
        }

        private void AppendLog(string message)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke((Action)(() => AppendLog(message)));
                return;
            }

            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        private void UpdateChart()
        {
            if (chartPanel.InvokeRequired)
            {
                chartPanel.Invoke((Action)UpdateChart);
                return;
            }

            chartPanel.Invalidate();
        }

        private void ChartPanel_Paint(object sender, PaintEventArgs e)
        {
            if (fitnessHistory.Count == 0) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int width = chartPanel.Width - 40;
            int height = chartPanel.Height - 40;
            int offsetX = 30;
            int offsetY = 20;

            g.DrawLine(Pens.Black, offsetX, height + offsetY, width + offsetX, height + offsetY);
            g.DrawLine(Pens.Black, offsetX, offsetY, offsetX, height + offsetY);

            if (fitnessHistory.Count < 2) return;

            double maxFitness = Math.Max(fitnessHistory.Max(), avgFitnessHistory.Max());
            double minFitness = Math.Min(fitnessHistory.Min(), avgFitnessHistory.Min());
            double range = maxFitness - minFitness;
            if (range < 1) range = 1;

            Pen bestPen = new Pen(Color.Blue, 2);
            for (int i = 0; i < fitnessHistory.Count - 1; i++)
            {
                float x1 = offsetX + (i * width / (float)fitnessHistory.Count);
                float y1 = offsetY + height - (float)((fitnessHistory[i] - minFitness) / range * height);
                float x2 = offsetX + ((i + 1) * width / (float)fitnessHistory.Count);
                float y2 = offsetY + height - (float)((fitnessHistory[i + 1] - minFitness) / range * height);

                g.DrawLine(bestPen, x1, y1, x2, y2);
            }

            Pen avgPen = new Pen(Color.Red, 2);
            for (int i = 0; i < avgFitnessHistory.Count - 1; i++)
            {
                float x1 = offsetX + (i * width / (float)avgFitnessHistory.Count);
                float y1 = offsetY + height - (float)((avgFitnessHistory[i] - minFitness) / range * height);
                float x2 = offsetX + ((i + 1) * width / (float)avgFitnessHistory.Count);
                float y2 = offsetY + height - (float)((avgFitnessHistory[i + 1] - minFitness) / range * height);

                g.DrawLine(avgPen, x1, y1, x2, y2);
            }

            g.FillRectangle(Brushes.Blue, offsetX + width - 100, offsetY, 15, 3);
            g.DrawString("Best", Font, Brushes.Black, offsetX + width - 80, offsetY - 5);
            g.FillRectangle(Brushes.Red, offsetX + width - 100, offsetY + 15, 15, 3);
            g.DrawString("Average", Font, Brushes.Black, offsetX + width - 80, offsetY + 10);

            g.DrawString($"Max: {maxFitness:F1}", Font, Brushes.Black, offsetX - 25, offsetY - 5);
            g.DrawString($"Min: {minFitness:F1}", Font, Brushes.Black, offsetX - 25, height + offsetY - 10);
        }
    }
}