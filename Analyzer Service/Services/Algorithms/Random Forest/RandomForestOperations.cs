using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.Random_Forest;
using System;
using System.Linq;
using System.Text.Json;

namespace Analyzer_Service.Services.Algorithms.Random_Forest
{
    public class RandomForestOperations : IRandomForestOperations
    {
        public double[] ScaleFeatures(double[] featureVector, double[] meanArray, double[] scaleArray)
        {
            int featureCount = featureVector.Length;
            double[] scaledFeatureVector = new double[featureCount];

            for (int featureIndex = 0; featureIndex < featureCount; featureIndex++)
            {
                double mean = meanArray[featureIndex];
                double scale = scaleArray[featureIndex];

                if (Math.Abs(scale) < ConstantAlgorithm.EPSILON)
                {
                    scale = 1.0;
                }

                scaledFeatureVector[featureIndex] =
                    (featureVector[featureIndex] - mean) / scale;
            }

            return scaledFeatureVector;
        }

        public int PredictTree(JsonElement treeElement, double[] scaledFeatures)
        {
            int[] featureIndexArray =
                treeElement
                    .GetProperty(ConstantRandomForest.FEATURE_JSON)
                    .EnumerateArray()
                    .Select(value => value.GetInt32())
                    .ToArray();

            double[] thresholdArray =
                treeElement
                    .GetProperty(ConstantRandomForest.THRESHOLD_JSON)
                    .EnumerateArray()
                    .Select(value => value.GetDouble())
                    .ToArray();

            int[] leftChildArray =
                treeElement
                    .GetProperty(ConstantRandomForest.CHILDREN_LEFT_JSON)
                    .EnumerateArray()
                    .Select(value => value.GetInt32())
                    .ToArray();

            int[] rightChildArray =
                treeElement
                    .GetProperty(ConstantRandomForest.CHILDREN_RIGHT_JSON)
                    .EnumerateArray()
                    .Select(value => value.GetInt32())
                    .ToArray();

            double[][] valueMatrix =
                treeElement
                    .GetProperty(ConstantRandomForest.VALUE_JSON)
                    .EnumerateArray()
                    .Select(node =>
                        node.EnumerateArray()
                            .Select(value => value.GetDouble())
                            .ToArray())
                    .ToArray();

            int currentNodeIndex = 0;

            while (leftChildArray[currentNodeIndex] != ConstantRandomForest.LEAF_NODE)
            {
                int featureIndex = featureIndexArray[currentNodeIndex];
                double threshold = thresholdArray[currentNodeIndex];

                if (scaledFeatures[featureIndex] <= threshold)
                {
                    currentNodeIndex = leftChildArray[currentNodeIndex];
                }
                else
                {
                    currentNodeIndex = rightChildArray[currentNodeIndex];
                }
            }

            double[] classVotes = valueMatrix[currentNodeIndex];
            double maxVote = classVotes.Max();
            int bestClassIndex = Array.IndexOf(classVotes, maxVote);

            return bestClassIndex;
        }

        public string PredictLabel(RandomForestModel model, SegmentFeatures features)
        {
            double[] rawFeatureVector = new double[]
            {
                features.DurationSeconds,
                features.MeanZ,
                features.StdZ,
                features.MinZ,
                features.MaxZ,
                features.RangeZ,
                features.EnergyZ,
                features.Slope,
                Convert.ToDouble(features.PeakCount),
                Convert.ToDouble(features.TroughCount),
                features.MeanPrev,
                features.MeanNext
            };

            double[] scaledFeatures =
                ScaleFeatures(rawFeatureVector, model.ScalerMean, model.ScalerScale);

            string[] labels = model.Labels;
            double[] votesPerLabel = new double[labels.Length];

            JsonElement forestElement =
                model.Forest.GetProperty(ConstantRandomForest.FOREST_JSON);

            JsonElement treesElement =
                forestElement.GetProperty(ConstantRandomForest.TREES_JSON);

            foreach (JsonElement treeElement in treesElement.EnumerateArray())
            {
                int predictedClassIndex = PredictTree(treeElement, scaledFeatures);
                votesPerLabel[predictedClassIndex] += 1.0;
            }

            double highestVote = votesPerLabel.Max();
            int bestLabelIndex = Array.IndexOf(votesPerLabel, highestVote);

            return labels[bestLabelIndex];
        }
    }
}
