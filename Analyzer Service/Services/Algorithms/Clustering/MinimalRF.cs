using System;
using System.Linq;
using System.Text.Json;

namespace Analyzer_Service.Services.Algorithms.Clustering
{
    public static class MinimalRF
    {
        // -----------------------------
        //  Scaling (StandardScaler)
        // -----------------------------
        public static double[] Scale(double[] features, JsonElement meanElement, JsonElement scaleElement)
        {
            int featureCount = features.Length;
            double[] scaled = new double[featureCount];

            using (JsonElement.ArrayEnumerator meanEnumerator = meanElement.EnumerateArray())
            using (JsonElement.ArrayEnumerator scaleEnumerator = scaleElement.EnumerateArray())
            {
                for (int featureIndex = 0; featureIndex < featureCount; featureIndex++)
                {
                    if (!meanEnumerator.MoveNext() || !scaleEnumerator.MoveNext())
                    {
                        throw new InvalidOperationException("Scaler arrays length does not match feature vector length.");
                    }

                    double mean = meanEnumerator.Current.GetDouble();
                    double scale = scaleEnumerator.Current.GetDouble();
                    if (Math.Abs(scale) < 1e-12)
                    {
                        scale = 1.0;
                    }

                    scaled[featureIndex] = (features[featureIndex] - mean) / scale;
                }
            }

            return scaled;
        }

        // -----------------------------
        //  Predict one decision tree
        // -----------------------------
        public static int PredictTree(JsonElement treeElement, double[] scaledFeatures)
        {
            int[] featureIndexPerNode = treeElement
                .GetProperty("feature")
                .EnumerateArray()
                .Select(element => element.GetInt32())
                .ToArray();

            double[] thresholdPerNode = treeElement
                .GetProperty("threshold")
                .EnumerateArray()
                .Select(element => element.GetDouble())
                .ToArray();

            int[] leftChildPerNode = treeElement
                .GetProperty("children_left")
                .EnumerateArray()
                .Select(element => element.GetInt32())
                .ToArray();

            int[] rightChildPerNode = treeElement
                .GetProperty("children_right")
                .EnumerateArray()
                .Select(element => element.GetInt32())
                .ToArray();

            // value is shape: [n_nodes][n_classes]
            double[][] valueMatrix = treeElement
                .GetProperty("value")
                .EnumerateArray()
                .Select(nodeElement =>
                    nodeElement.EnumerateArray().Select(v => v.GetDouble()).ToArray())
                .ToArray();

            int nodeIndex = 0;

            while (leftChildPerNode[nodeIndex] != -1)
            {
                int featureIndex = featureIndexPerNode[nodeIndex];

                if (featureIndex < 0 || featureIndex >= scaledFeatures.Length)
                {
                    throw new InvalidOperationException(
                        $"Tree expects feature index {featureIndex}, but feature vector length is {scaledFeatures.Length}.");
                }

                double value = scaledFeatures[featureIndex];
                double threshold = thresholdPerNode[nodeIndex];

                if (value <= threshold)
                {
                    nodeIndex = leftChildPerNode[nodeIndex];
                }
                else
                {
                    nodeIndex = rightChildPerNode[nodeIndex];
                }
            }

            double[] leafClassValues = valueMatrix[nodeIndex];
            int bestClassIndex = Array.IndexOf(leafClassValues, leafClassValues.Max());
            return bestClassIndex;
        }

        // -----------------------------
        //  Predict whole forest
        // -----------------------------
        public static string PredictLabel(JsonDocument modelDocument, double[] rawFeatures)
        {
            JsonElement root = modelDocument.RootElement;

            double[] scaledFeatures = Scale(
                rawFeatures,
                root.GetProperty("scaler").GetProperty("mean"),
                root.GetProperty("scaler").GetProperty("scale"));

            string[] labels = root
                .GetProperty("labels")
                .EnumerateArray()
                .Select(element => element.GetString())
                .ToArray();

            double[] votesPerClass = new double[labels.Length];

            foreach (JsonElement treeElement in root.GetProperty("forest").GetProperty("trees").EnumerateArray())
            {
                int predictedClassIndex = PredictTree(treeElement, scaledFeatures);
                votesPerClass[predictedClassIndex] += 1.0;
            }

            int bestClassIndex = Array.IndexOf(votesPerClass, votesPerClass.Max());
            return labels[bestClassIndex];
        }
    }
}
