using System;
using System.Linq;
using System.Text.Json;
using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms.Random_Forest;

namespace Analyzer_Service.Services.Algorithms.Random_Forest
{
    public class RandomForestOperations : IRandomForestOperations
    {
        public double[] ScaleFeatures(double[] featureVector, JsonElement meanElement, JsonElement scaleElement)
        {
            int featureCount = featureVector.Length;
            double[] scaled = new double[featureCount];

            JsonElement.ArrayEnumerator meanEnum = meanElement.EnumerateArray();
            JsonElement.ArrayEnumerator scaleEnum = scaleElement.EnumerateArray();

            for (int index = 0; index < featureCount; index++)
            {
                meanEnum.MoveNext();
                scaleEnum.MoveNext();

                double mean = meanEnum.Current.GetDouble();
                double scale = scaleEnum.Current.GetDouble();

                if (Math.Abs(scale) < ConstantAlgorithm.Epsilon)
                    scale = 1.0;

                scaled[index] = (featureVector[index] - mean) / scale;
            }

            return scaled;
        }

        public int PredictTree(JsonElement tree, double[] scaledFeatures)
        {
            int[] featureIndex = tree.GetProperty(ConstantRandomForest.FEATURE_JSON)
                                      .EnumerateArray().Select(field => field.GetInt32()).ToArray();

            double[] threshold = tree.GetProperty(ConstantRandomForest.THRESHOLD_JSON)
                                      .EnumerateArray().Select(field => field.GetDouble()).ToArray();

            int[] left = tree.GetProperty(ConstantRandomForest.CHILDREN_LEFT_JSON)
                               .EnumerateArray().Select(field => field.GetInt32()).ToArray();

            int[] right = tree.GetProperty(ConstantRandomForest.CHILDREN_RIGHT_JSON)
                                .EnumerateArray().Select(field => field.GetInt32()).ToArray();

            double[][] valueMatrix =
                tree.GetProperty(ConstantRandomForest.VALUE_JSON)
                    .EnumerateArray()
                    .Select(node => node.EnumerateArray().Select(vector => vector.GetDouble()).ToArray())
                    .ToArray();

            int node = 0;

            while (left[node] != ConstantRandomForest.LEAF_NODE)
            {
                int feature = featureIndex[node];
                node = (scaledFeatures[feature] <= threshold[node]) ? left[node] : right[node];
            }

            double[] classVotes = valueMatrix[node];
            return Array.IndexOf(classVotes, classVotes.Max());
        }

        public string PredictLabel(IRandomForestModelProvider provider, double[] rawFeatures)
        {
            JsonElement root = provider.ModelDocument.RootElement;

            double[] scaled =
                ScaleFeatures(
                    rawFeatures,
                    root.GetProperty(ConstantRandomForest.SCALER_JSON).GetProperty(ConstantRandomForest.MEAN_JSON),
                    root.GetProperty(ConstantRandomForest.SCALER_JSON).GetProperty(ConstantRandomForest.SCALE_JSON));

            string[] labels = provider.Labels.ToArray();

            double[] votes = new double[labels.Length];

            foreach (JsonElement tree in root
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
