using Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly;

namespace Analyzer_Service.Services.Algorithms.HistoricalAnomaly
{
    public class HistoricalAnomalySimilarityLogic : IHistoricalAnomalySimilarityLogic
    {
        public double CompareHashesFuzzy(double[] hashA, double[] hashB)
        {
            int length = hashA.Length;
            int differenceCount = 0;

            for (int index = 0; index < length; index++)
            {
                if (Math.Abs(hashA[index] - hashB[index]) > 0.001)
                {
                    differenceCount++;
                }
            }

            double similarity = 1.0 - ((double)differenceCount / (double)length);
            return similarity;
        }

        public double CompareFeatureVectors(Dictionary<string, double> featuresA, Dictionary<string, double> featuresB)
        {
            List<double> vectorA = new List<double>();
            List<double> vectorB = new List<double>();

            foreach (KeyValuePair<string, double> entry in featuresA)
            {
                vectorA.Add(entry.Value);
                vectorB.Add(featuresB[entry.Key]);
            }

            double dotProduct = 0.0;
            double normA = 0.0;
            double normB = 0.0;

            for (int index = 0; index < vectorA.Count; index++)
            {
                dotProduct += vectorA[index] * vectorB[index];
                normA += vectorA[index] * vectorA[index];
                normB += vectorB[index] * vectorB[index];
            }

            double denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
            if (denominator <= 0.0)
            {
                return 0.0;
            }

            double cosineSimilarity = dotProduct / denominator;
            return cosineSimilarity;
        }

        public double CompareDurationSimilarity(double durationA, double durationB)
        {
            double maxDuration = Math.Max(durationA, durationB);
            if (maxDuration <= 0.0)
            {
                return 0.0;
            }

            double similarity = 1.0 - (Math.Abs(durationA - durationB) / maxDuration);
            return similarity;
        }

        public double ComputeWeightedScore(double hashSimilarity, double featureSimilarity, double durationSimilarity)
        {
            double finalScore =
                (0.75 * hashSimilarity) +
                (0.2 * featureSimilarity) +
                (0.05 * durationSimilarity);

            return finalScore;
        }
    }
}
