using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using checkersclaude.AI;

namespace checkersclaude
{
    public class MainMenuForm : Form
    {
        private const string AI_FILE = "best_checkers_ai.dat";

        public MainMenuForm()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            Text = "Checkers AI - Deep Learning Edition";
            Size = new Size(600, 450);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Color.FromArgb(240, 240, 245);

            var lblTitle = new Label
            {
                Text = "CHECKERS AI",
                Font = new Font("Arial", 36, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 0, 0),
                Location = new Point(0, 40),
                Size = new Size(600, 70),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(lblTitle);

            var lblSubtitle = new Label
            {
                Text = "Deep Neural Network Edition",
                Font = new Font("Arial", 12, FontStyle.Italic),
                ForeColor = Color.Gray,
                Location = new Point(0, 110),
                Size = new Size(600, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(lblSubtitle);

            CreateButton("Play vs Human", 160, Color.FromArgb(70, 130, 180), (s, e) => PlayHuman());
            CreateButton("Play vs AI", 230, Color.FromArgb(34, 139, 34), (s, e) => PlayAI());
            CreateButton("Train New AI", 300, Color.FromArgb(255, 140, 0), (s, e) => TrainAI());

            var lblStatus = new Label
            {
                Text = File.Exists(AI_FILE) ? "✓ Trained AI Available" : "⚠ No trained AI yet - Train first!",
                Font = new Font("Arial", 10, FontStyle.Italic),
                ForeColor = File.Exists(AI_FILE) ? Color.Green : Color.OrangeRed,
                Location = new Point(0, 370),
                Size = new Size(600, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(lblStatus);

            var lblInfo = new Label
            {
                Text = "Network: 64→128→64→32→1 | Algorithm: Genetic + Self-Play",
                Font = new Font("Arial", 8),
                ForeColor = Color.Gray,
                Location = new Point(0, 395),
                Size = new Size(600, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };
            Controls.Add(lblInfo);
        }

        private void CreateButton(string text, int y, Color color, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Arial", 14, FontStyle.Bold),
                Size = new Size(350, 50),
                Location = new Point(125, y),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            Controls.Add(btn);
        }

        private void PlayHuman()
        {
            var gameForm = new CheckersForm(GameMode.HumanVsHuman, null);
            gameForm.FormClosed += (s, e) => Show();
            gameForm.Show();
            Hide();
        }

        private void PlayAI()
        {
            if (!File.Exists(AI_FILE))
            {
                MessageBox.Show("No trained AI found!\n\nPlease train an AI first using the 'Train New AI' option.",
                    "No AI Available", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var brain = DeepNeuralNetwork.LoadFromFile(AI_FILE);
                var aiPlayer = new AIPlayer(brain);
                var gameForm = new CheckersForm(GameMode.HumanVsAI, aiPlayer);
                gameForm.FormClosed += (s, e) => Show();
                gameForm.Show();
                Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load AI:\n{ex.Message}\n\nPlease train a new AI.",
                    "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TrainAI()
        {
            var trainingForm = new TrainingForm();
            trainingForm.FormClosed += (s, e) => Show();
            trainingForm.Show();
            Hide();
        }
    }
}