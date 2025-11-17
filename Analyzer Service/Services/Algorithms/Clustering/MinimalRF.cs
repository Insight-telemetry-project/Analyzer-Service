using System;
using System.Linq;
using System.Text.Json;
using Analyzer_Service.Models.Constant;
namespace Analyzer_Service.Services.Algorithms.Clustering
{
    public static class MinimalRF
    {
        public static double[] Scale(double[] featureVector, JsonElement meanElement, JsonElement scaleElement)
        {
            int featureCount = featureVector.Length;
            double[] scaledVector = new double[featureCount];

            JsonElement.ArrayEnumerator meanEnumerator = meanElement.EnumerateArray();
            JsonElement.ArrayEnumerator scaleEnumerator = scaleElement.EnumerateArray();

            for (int featureIndex = 0; featureIndex < featureCount; featureIndex++)
            {
                bool hasMean = meanEnumerator.MoveNext();
                bool hasScale = scaleEnumerator.MoveNext();
                double featureMean = meanEnumerator.Current.GetDouble();
                double featureScale = scaleEnumerator.Current.GetDouble();

                if (Math.Abs(featureScale) < 1e-12)
                {
                    featureScale = 1.0;
                }

                double rawFeatureValue = featureVector[featureIndex];
                double scaledValue = (rawFeatureValue - featureMean) / featureScale;
                scaledVector[featureIndex] = scaledValue;
            }

            return scaledVector;
        }

        public static int PredictTree(JsonElement treeElement, double[] scaledFeatureVector)
        {
            int[] featureIndexPerNode = treeElement
                .GetProperty(ConstantRandomForest.FEATURE_JSON)
                .EnumerateArray()
                .Select(element => element.GetInt32())
                .ToArray();

            double[] thresholdPerNode = treeElement
                .GetProperty(ConstantRandomForest.THRESHOLD_JSON)
                .EnumerateArray()
                .Select(element => element.GetDouble())
                .ToArray();

            int[] leftChildPerNode = treeElement
                .GetProperty(ConstantRandomForest.CHILDREN_LEFT_JSON)
                .EnumerateArray()
                .Select(element => element.GetInt32())
                .ToArray();

            int[] rightChildPerNode = treeElement
                .GetProperty(ConstantRandomForest.CHILDREN_RIGHT_JSON)
                .EnumerateArray()
                .Select(element => element.GetInt32())
                .ToArray();

            double[][] classValuesPerNode = treeElement
                .GetProperty(ConstantRandomForest.VALUE_JSON)
                .EnumerateArray()
                .Select(nodeElement =>
                    nodeElement.EnumerateArray().Select(classElement => classElement.GetDouble()).ToArray())
                .ToArray();

            int nodeIndex = 0;

            while (leftChildPerNode[nodeIndex] != -1)
            {
                int featureIndex = featureIndexPerNode[nodeIndex];
                double featureValue = scaledFeatureVector[featureIndex];
                double thresholdValue = thresholdPerNode[nodeIndex];

                if (featureValue <= thresholdValue)
                {
                    nodeIndex = leftChildPerNode[nodeIndex];
                }
                else
                {
                    nodeIndex = rightChildPerNode[nodeIndex];
                }
            }

            double[] leafClassProbabilities = classValuesPerNode[nodeIndex];
            int predictedClassIndex = Array.IndexOf(leafClassProbabilities, leafClassProbabilities.Max());

            return predictedClassIndex;
        }

        public static string PredictLabel(JsonDocument modelDocument, double[] rawFeatureVector)
        {
            JsonElement rootElement = modelDocument.RootElement;

            double[] scaledFeatureVector = Scale(
                rawFeatureVector,
                rootElement.GetProperty(ConstantRandomForest.SCALE_JSON).GetProperty(ConstantRandomForest.MEAN_JSON),
                rootElement.GetProperty(ConstantRandomForest.SCALE_JSON).GetProperty(ConstantRandomForest.SCALE_JSON));

            string[] classLabels = rootElement
                .GetProperty(ConstantRandomForest.LABELS_JSON)
                .EnumerateArray()
                .Select(element => element.GetString())
                .ToArray();

            double[] voteCountPerClass = new double[classLabels.Length];

            foreach (JsonElement treeElement in rootElement.GetProperty(ConstantRandomForest.FOREST_JSON).GetProperty(ConstantRandomForest.TREES_JSON).EnumerateArray())
            {
                int predictedClassIndex = PredictTree(treeElement, scaledFeatureVector);
                voteCountPerClass[predictedClassIndex] += 1.0;
            }

            int finalClassIndex = Array.IndexOf(voteCountPerClass, voteCountPerClass.Max());
            return classLabels[finalClassIndex];
        }
    }
}
