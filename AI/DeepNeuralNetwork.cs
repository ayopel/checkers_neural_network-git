using System;
using System.Collections.Generic;
using System.IO;

namespace checkersclaude.AI
{
    public class DeepNeuralNetwork
    {
        private readonly int inputSize;
        private readonly int[] hiddenSizes;
        private readonly int outputSize;
        private readonly Random random;

        private List<double[][]> weights;
        private List<double[]> biases;

        public double Fitness { get; set; }
        public int GamesPlayed { get; set; }

        public DeepNeuralNetwork(int inputSize, int[] hiddenSizes, int outputSize, Random random = null)
        {
            this.inputSize = inputSize;
            this.hiddenSizes = hiddenSizes;
            this.outputSize = outputSize;
            this.random = random ?? new Random();

            InitializeNetwork();
        }

        private void InitializeNetwork()
        {
            weights = new List<double[][]>();
            biases = new List<double[]>();

            int prevSize = inputSize;
            foreach (int hiddenSize in hiddenSizes)
            {
                weights.Add(InitializeWeightMatrix(prevSize, hiddenSize));
                biases.Add(InitializeBiasVector(hiddenSize));
                prevSize = hiddenSize;
            }

            weights.Add(InitializeWeightMatrix(prevSize, outputSize));
            biases.Add(InitializeBiasVector(outputSize));
        }

        private double[][] InitializeWeightMatrix(int rows, int cols)
        {
            double[][] matrix = new double[rows][];
            double stdDev = Math.Sqrt(2.0 / rows);

            for (int i = 0; i < rows; i++)
            {
                matrix[i] = new double[cols];
                for (int j = 0; j < cols; j++)
                {
                    matrix[i][j] = NextGaussian() * stdDev;
                }
            }
            return matrix;
        }

        private double[] InitializeBiasVector(int size)
        {
            double[] vector = new double[size];
            for (int i = 0; i < size; i++)
                vector[i] = 0.01;
            return vector;
        }

        private double NextGaussian()
        {
            double u1 = 1.0 - random.NextDouble();
            double u2 = 1.0 - random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }

        public double[] FeedForward(double[] inputs)
        {
            if (inputs.Length != inputSize)
                throw new ArgumentException($"Expected {inputSize} inputs, got {inputs.Length}");

            double[] current = inputs;

            for (int layer = 0; layer < weights.Count - 1; layer++)
            {
                current = ApplyLayer(current, weights[layer], biases[layer], true);
            }

            current = ApplyLayer(current, weights[weights.Count - 1], biases[biases.Count - 1], false);

            return current;
        }

        private double[] ApplyLayer(double[] inputs, double[][] layerWeights, double[] layerBiases, bool useActivation)
        {
            int outputSize = layerWeights[0].Length;
            double[] output = new double[outputSize];

            for (int j = 0; j < outputSize; j++)
            {
                output[j] = layerBiases[j];
                for (int i = 0; i < inputs.Length; i++)
                {
                    output[j] += inputs[i] * layerWeights[i][j];
                }

                if (useActivation)
                    output[j] = LeakyReLU(output[j]);
            }

            return output;
        }

        private double LeakyReLU(double x) => x > 0 ? x : 0.01 * x;

        public DeepNeuralNetwork Clone()
        {
            var clone = new DeepNeuralNetwork(inputSize, hiddenSizes, outputSize, random);

            for (int layer = 0; layer < weights.Count; layer++)
            {
                for (int i = 0; i < weights[layer].Length; i++)
                {
                    for (int j = 0; j < weights[layer][i].Length; j++)
                    {
                        clone.weights[layer][i][j] = weights[layer][i][j];
                    }
                }

                for (int i = 0; i < biases[layer].Length; i++)
                {
                    clone.biases[layer][i] = biases[layer][i];
                }
            }

            clone.Fitness = Fitness;
            return clone;
        }

        public void Mutate(double mutationRate, double mutationStrength = 0.2)
        {
            for (int layer = 0; layer < weights.Count; layer++)
            {
                for (int i = 0; i < weights[layer].Length; i++)
                {
                    for (int j = 0; j < weights[layer][i].Length; j++)
                    {
                        if (random.NextDouble() < mutationRate)
                        {
                            double mutation = NextGaussian() * mutationStrength;
                            weights[layer][i][j] += mutation;
                            // Clamp between -5.0 and 5.0
                            if (weights[layer][i][j] > 5.0) weights[layer][i][j] = 5.0;
                            if (weights[layer][i][j] < -5.0) weights[layer][i][j] = -5.0;
                        }
                    }
                }

                for (int i = 0; i < biases[layer].Length; i++)
                {
                    if (random.NextDouble() < mutationRate)
                    {
                        double mutation = NextGaussian() * mutationStrength * 0.5;
                        biases[layer][i] += mutation;
                        // Clamp between -2.0 and 2.0
                        if (biases[layer][i] > 2.0) biases[layer][i] = 2.0;
                        if (biases[layer][i] < -2.0) biases[layer][i] = -2.0;
                    }
                }
            }
        }

        public DeepNeuralNetwork Crossover(DeepNeuralNetwork partner, double crossoverRate = 0.5)
        {
            var child = new DeepNeuralNetwork(inputSize, hiddenSizes, outputSize, random);

            for (int layer = 0; layer < weights.Count; layer++)
            {
                for (int i = 0; i < weights[layer].Length; i++)
                {
                    for (int j = 0; j < weights[layer][i].Length; j++)
                    {
                        child.weights[layer][i][j] = random.NextDouble() < crossoverRate
                            ? weights[layer][i][j]
                            : partner.weights[layer][i][j];
                    }
                }

                for (int i = 0; i < biases[layer].Length; i++)
                {
                    child.biases[layer][i] = random.NextDouble() < crossoverRate
                        ? biases[layer][i]
                        : partner.biases[layer][i];
                }
            }

            return child;
        }

        public void SaveToFile(string filepath)
        {
            using (var writer = new BinaryWriter(File.Open(filepath, FileMode.Create)))
            {
                writer.Write(inputSize);
                writer.Write(hiddenSizes.Length);
                foreach (int size in hiddenSizes)
                    writer.Write(size);
                writer.Write(outputSize);

                foreach (var layerWeights in weights)
                {
                    writer.Write(layerWeights.Length);
                    writer.Write(layerWeights[0].Length);
                    foreach (var row in layerWeights)
                        foreach (double val in row)
                            writer.Write(val);
                }

                foreach (var layerBiases in biases)
                {
                    writer.Write(layerBiases.Length);
                    foreach (double val in layerBiases)
                        writer.Write(val);
                }

                writer.Write(Fitness);
                writer.Write(GamesPlayed);
            }
        }

        public static DeepNeuralNetwork LoadFromFile(string filepath)
        {
            using (var reader = new BinaryReader(File.Open(filepath, FileMode.Open)))
            {
                int inputSize = reader.ReadInt32();
                int hiddenLayerCount = reader.ReadInt32();
                int[] hiddenSizes = new int[hiddenLayerCount];
                for (int i = 0; i < hiddenLayerCount; i++)
                    hiddenSizes[i] = reader.ReadInt32();
                int outputSize = reader.ReadInt32();

                var network = new DeepNeuralNetwork(inputSize, hiddenSizes, outputSize);

                for (int layer = 0; layer < network.weights.Count; layer++)
                {
                    int rows = reader.ReadInt32();
                    int cols = reader.ReadInt32();
                    for (int i = 0; i < rows; i++)
                        for (int j = 0; j < cols; j++)
                            network.weights[layer][i][j] = reader.ReadDouble();
                }

                for (int layer = 0; layer < network.biases.Count; layer++)
                {
                    int size = reader.ReadInt32();
                    for (int i = 0; i < size; i++)
                        network.biases[layer][i] = reader.ReadDouble();
                }

                network.Fitness = reader.ReadDouble();
                network.GamesPlayed = reader.ReadInt32();

                return network;
            }
        }
    }
}