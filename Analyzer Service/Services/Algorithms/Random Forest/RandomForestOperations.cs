using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.Random_Forest;
using System;
using System.Linq;
using System.Text.Json;
using static MongoDB.Driver.WriteConcern;

namespace Analyzer_Service.Services.Algorithms.Random_Forest
{
    public class RandomForestOperations : IRandomForestOperations
    {
        public double[] ScaleFeatures(double[] featureVector, double[] meanArray, double[] scaleArray)
        {
            int featureCount = featureVector.Length;
            double[] scaled = new double[featureCount];

            for (int index = 0; index < featureCount; index++)
            {
                double mean = meanArray[index];
                double scale = scaleArray[index];

                if (Math.Abs(scale) < ConstantAlgorithm.Epsilon)
                    scale = 1.0;

                scaled[index] = (featureVector[index] - mean) / scale;
            }

            return scaled;
        }

        public int PredictTree(JsonElement tree, double[] scaledFeatures)
        {
            int[] featureIndex = tree
                .GetProperty(ConstantRandomForest.FEATURE_JSON)
                .EnumerateArray()
                .Select(value => value.GetInt32())
                .ToArray();

            double[] threshold = tree
                .GetProperty(ConstantRandomForest.THRESHOLD_JSON)
                .EnumerateArray()
                .Select(value => value.GetDouble())
                .ToArray();

            int[] left = tree
                .GetProperty(ConstantRandomForest.CHILDREN_LEFT_JSON)
                .EnumerateArray()
                .Select(value => value.GetInt32())
                .ToArray();

            int[] right = tree
                .GetProperty(ConstantRandomForest.CHILDREN_RIGHT_JSON)
                .EnumerateArray()
                .Select(value => value.GetInt32())
                .ToArray();

            double[][] valueMatrix =
                tree.GetProperty(ConstantRandomForest.VALUE_JSON)
                    .EnumerateArray()
                    .Select(node =>
                        node.EnumerateArray()
                            .Select(value => value.GetDouble())
                            .ToArray())
                    .ToArray();

            int node = 0;

            while (left[node] != ConstantRandomForest.LEAF_NODE)
            {
                int feature = featureIndex[node];
                node = (scaledFeatures[feature] <= threshold[node]) ? left[node] : right[node];
            }

            double[] votes = valueMatrix[node];
            return Array.IndexOf(votes, votes.Max());
        }

        public string PredictLabel(RandomForestModel model, double[] rawFeatures)
        {
            double[] scaled = ScaleFeatures(rawFeatures, model.ScalerMean, model.ScalerScale);

            string[] labels = model.Labels;
            double[] votes = new double[labels.Length];

            foreach (JsonElement tree in model.Forest
                     .GetProperty(ConstantRandomForest.FOREST_JSON)
                     .GetProperty(ConstantRandomForest.TREES_JSON)
                     .EnumerateArray())
            {
                int pred = PredictTree(tree, scaled);
                votes[pred] += 1;
            }

            int bestIndex = Array.IndexOf(votes, votes.Max());
            return labels[bestIndex];
        }
    }
}
