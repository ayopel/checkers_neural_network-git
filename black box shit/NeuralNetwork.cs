using System;
using System.Linq;

namespace checkersclaude
{
    public class NeuralNetwork
    {
        private int inputSize;
        private int hiddenSize1;
        private int hiddenSize2;
        private int outputSize;

        private double[,] weightsInputHidden1;
        private double[] biasHidden1;
        private double[,] weightsHidden1Hidden2;
        private double[] biasHidden2;
        private double[,] weightsHidden2Output;
        private double[] biasOutput;

        private Random random;

        // Learning parameters
        private const double LearningRate = 0.01;
        private const double Momentum = 0.9;

        // Momentum terms
        private double[,] momentumInputHidden1;
        private double[] momentumBiasHidden1;
        private double[,] momentumHidden1Hidden2;
        private double[] momentumBiasHidden2;
        private double[,] momentumHidden2Output;
        private double[] momentumBiasOutput;

        public NeuralNetwork(int inputSize, int hiddenSize, int outputSize, Random random = null)
        {
            this.inputSize = inputSize;
            this.hiddenSize1 = hiddenSize;
            this.hiddenSize2 = hiddenSize / 2; // Second hidden layer
            this.outputSize = outputSize;
            this.random = random ?? new Random();

            InitializeWeights();
            InitializeMomentum();
        }

        private void InitializeWeights()
        {
            // Xavier/Glorot initialization for better learning
            double stdInput = Math.Sqrt(2.0 / inputSize);
            double stdHidden1 = Math.Sqrt(2.0 / hiddenSize1);
            double stdHidden2 = Math.Sqrt(2.0 / hiddenSize2);

            weightsInputHidden1 = new double[inputSize, hiddenSize1];
            biasHidden1 = new double[hiddenSize1];

            for (int i = 0; i < inputSize; i++)
                for (int j = 0; j < hiddenSize1; j++)
                    weightsInputHidden1[i, j] = NextGaussian() * stdInput;

            weightsHidden1Hidden2 = new double[hiddenSize1, hiddenSize2];
            biasHidden2 = new double[hiddenSize2];

            for (int i = 0; i < hiddenSize1; i++)
                for (int j = 0; j < hiddenSize2; j++)
                    weightsHidden1Hidden2[i, j] = NextGaussian() * stdHidden1;

            weightsHidden2Output = new double[hiddenSize2, outputSize];
            biasOutput = new double[outputSize];

            for (int i = 0; i < hiddenSize2; i++)
                for (int j = 0; j < outputSize; j++)
                    weightsHidden2Output[i, j] = NextGaussian() * stdHidden2;
        }

        private void InitializeMomentum()
        {
            momentumInputHidden1 = new double[inputSize, hiddenSize1];
            momentumBiasHidden1 = new double[hiddenSize1];
            momentumHidden1Hidden2 = new double[hiddenSize1, hiddenSize2];
            momentumBiasHidden2 = new double[hiddenSize2];
            momentumHidden2Output = new double[hiddenSize2, outputSize];
            momentumBiasOutput = new double[outputSize];
        }

        public double[] FeedForward(double[] inputs)
        {
            if (inputs.Length != inputSize)
                throw new ArgumentException($"Expected {inputSize} inputs, got {inputs.Length}");

            // Input to Hidden Layer 1
            double[] hidden1 = new double[hiddenSize1];
            for (int j = 0; j < hiddenSize1; j++)
            {
                double sum = biasHidden1[j];
                for (int i = 0; i < inputSize; i++)
                    sum += inputs[i] * weightsInputHidden1[i, j];
                hidden1[j] = LeakyReLU(sum);
            }

            // Hidden Layer 1 to Hidden Layer 2
            double[] hidden2 = new double[hiddenSize2];
            for (int j = 0; j < hiddenSize2; j++)
            {
                double sum = biasHidden2[j];
                for (int i = 0; i < hiddenSize1; i++)
                    sum += hidden1[i] * weightsHidden1Hidden2[i, j];
                hidden2[j] = LeakyReLU(sum);
            }

            // Hidden Layer 2 to Output
            double[] output = new double[outputSize];
            for (int j = 0; j < outputSize; j++)
            {
                double sum = biasOutput[j];
                for (int i = 0; i < hiddenSize2; i++)
                    sum += hidden2[i] * weightsHidden2Output[i, j];
                output[j] = Tanh(sum); // Output in range [-1, 1]
            }

            return output;
        }

        // LeakyReLU activation for hidden layers (better than sigmoid for deep networks)
        private double LeakyReLU(double x)
        {
            return x > 0 ? x : 0.01 * x;
        }

        private double LeakyReLUDerivative(double x)
        {
            return x > 0 ? 1.0 : 0.01;
        }

        // Tanh activation for output
        private double Tanh(double x)
        {
            return Math.Tanh(x);
        }

        private double TanhDerivative(double x)
        {
            double t = Math.Tanh(x);
            return 1 - t * t;
        }

        public void Mutate(double mutationRate)
        {
            // Adaptive mutation with Gaussian noise
            MutateWeights(weightsInputHidden1, mutationRate);
            MutateArray(biasHidden1, mutationRate);
            MutateWeights(weightsHidden1Hidden2, mutationRate);
            MutateArray(biasHidden2, mutationRate);
            MutateWeights(weightsHidden2Output, mutationRate);
            MutateArray(biasOutput, mutationRate);
        }

        private void MutateWeights(double[,] weights, double rate)
        {
            int rows = weights.GetLength(0);
            int cols = weights.GetLength(1);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    if (random.NextDouble() < rate)
                    {
                        // Add Gaussian noise scaled by mutation rate
                        weights[i, j] += NextGaussian() * rate * 0.5;
                        // Clip to prevent extreme values
                        weights[i, j] = Math.Max(-5, Math.Min(5, weights[i, j]));
                    }
                }
            }
        }

        private void MutateArray(double[] array, double rate)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (random.NextDouble() < rate)
                {
                    array[i] += NextGaussian() * rate * 0.5;
                    array[i] = Math.Max(-5, Math.Min(5, array[i]));
                }
            }
        }

        public NeuralNetwork Crossover(NeuralNetwork partner)
        {
            NeuralNetwork child = new NeuralNetwork(inputSize, hiddenSize1, outputSize, random);

            // Uniform crossover - randomly inherit from each parent
            CrossoverWeights(weightsInputHidden1, partner.weightsInputHidden1, child.weightsInputHidden1);
            CrossoverArray(biasHidden1, partner.biasHidden1, child.biasHidden1);
            CrossoverWeights(weightsHidden1Hidden2, partner.weightsHidden1Hidden2, child.weightsHidden1Hidden2);
            CrossoverArray(biasHidden2, partner.biasHidden2, child.biasHidden2);
            CrossoverWeights(weightsHidden2Output, partner.weightsHidden2Output, child.weightsHidden2Output);
            CrossoverArray(biasOutput, partner.biasOutput, child.biasOutput);

            return child;
        }

        private void CrossoverWeights(double[,] parent1, double[,] parent2, double[,] child)
        {
            int rows = parent1.GetLength(0);
            int cols = parent1.GetLength(1);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    // 50% chance to inherit from each parent, with slight blending
                    if (random.NextDouble() < 0.5)
                        child[i, j] = parent1[i, j];
                    else
                        child[i, j] = parent2[i, j];

                    // 10% chance to blend both parents
                    if (random.NextDouble() < 0.1)
                        child[i, j] = (parent1[i, j] + parent2[i, j]) / 2.0;
                }
            }
        }

        private void CrossoverArray(double[] parent1, double[] parent2, double[] child)
        {
            for (int i = 0; i < parent1.Length; i++)
            {
                if (random.NextDouble() < 0.5)
                    child[i] = parent1[i];
                else
                    child[i] = parent2[i];

                if (random.NextDouble() < 0.1)
                    child[i] = (parent1[i] + parent2[i]) / 2.0;
            }
        }

        public NeuralNetwork Clone()
        {
            NeuralNetwork clone = new NeuralNetwork(inputSize, hiddenSize1, outputSize, random);

            Array.Copy(weightsInputHidden1, clone.weightsInputHidden1, weightsInputHidden1.Length);
            Array.Copy(biasHidden1, clone.biasHidden1, biasHidden1.Length);
            Array.Copy(weightsHidden1Hidden2, clone.weightsHidden1Hidden2, weightsHidden1Hidden2.Length);
            Array.Copy(biasHidden2, clone.biasHidden2, biasHidden2.Length);
            Array.Copy(weightsHidden2Output, clone.weightsHidden2Output, weightsHidden2Output.Length);
            Array.Copy(biasOutput, clone.biasOutput, biasOutput.Length);

            return clone;
        }

        // Box-Muller transform for Gaussian random numbers
        private double NextGaussian()
        {
            double u1 = 1.0 - random.NextDouble();
            double u2 = 1.0 - random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }

        // Serialize weights for saving
        public double[] GetWeights()
        {
            int totalSize = weightsInputHidden1.Length + biasHidden1.Length +
                          weightsHidden1Hidden2.Length + biasHidden2.Length +
                          weightsHidden2Output.Length + biasOutput.Length;

            double[] weights = new double[totalSize];
            int index = 0;

            Buffer.BlockCopy(weightsInputHidden1, 0, weights, index * sizeof(double), weightsInputHidden1.Length * sizeof(double));
            index += weightsInputHidden1.Length;
            Array.Copy(biasHidden1, 0, weights, index, biasHidden1.Length);
            index += biasHidden1.Length;
            Buffer.BlockCopy(weightsHidden1Hidden2, 0, weights, index * sizeof(double), weightsHidden1Hidden2.Length * sizeof(double));
            index += weightsHidden1Hidden2.Length;
            Array.Copy(biasHidden2, 0, weights, index, biasHidden2.Length);
            index += biasHidden2.Length;
            Buffer.BlockCopy(weightsHidden2Output, 0, weights, index * sizeof(double), weightsHidden2Output.Length * sizeof(double));
            index += weightsHidden2Output.Length;
            Array.Copy(biasOutput, 0, weights, index, biasOutput.Length);

            return weights;
        }
    }
}