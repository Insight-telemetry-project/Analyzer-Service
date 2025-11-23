using Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Ro.Algorithms;
using Analyzer_Service.Models.Schema;
using MongoDB.Driver;

namespace Analyzer_Service.Services.Algorithms.HistoricalAnomaly
{
    public class HistoricalAnomalySimilarityService : IHistoricalAnomalySimilarityService
    {
        private readonly IFlightTelemetryMongoProxy mongoProxy;
        private readonly IHistoricalAnomalySimilarityLogic logic;

        public HistoricalAnomalySimilarityService(
            IFlightTelemetryMongoProxy mongoProxy,
            IHistoricalAnomalySimilarityLogic logic)
        {
            this.mongoProxy = mongoProxy;
            this.logic = logic;
        }

        public async Task<List<HistoricalSimilarityResult>> FindSimilarAnomaliesAsync(
            string parameterName,
            string label,
            double[] newHashVector,
            Dictionary<string, double> newFeatureVector,
            double newDuration,
            double threshold)
        {
            IAsyncCursor<HistoricalAnomalyRecord> cursor =
                await mongoProxy.GetHistoricalCandidatesAsync(parameterName, label);

            List<HistoricalSimilarityResult> results = new List<HistoricalSimilarityResult>();

            while (await cursor.MoveNextAsync())
            {
                foreach (HistoricalAnomalyRecord record in cursor.Current)
                {
                    double[] existingHash = ParseHash(record.PatternHash);
                    Dictionary<string, double> existingFeatures = record.FeatureValues;
                    double existingDuration = record.EndIndex - record.StartIndex;

                    double hashSimilarity =
                        logic.CompareHashesFuzzy(existingHash, newHashVector);

                    double featureSimilarity =
                        logic.CompareFeatureVectors(existingFeatures, newFeatureVector);

                    double durationSimilarity =
                        logic.CompareDurationSimilarity(existingDuration, newDuration);

                    double finalScore =
                        logic.ComputeWeightedScore(hashSimilarity, featureSimilarity, durationSimilarity);

                    if (finalScore >= threshold)
                    {
                        HistoricalSimilarityResult result = new HistoricalSimilarityResult
                        {
                            MasterIndex = record.MasterIndex,
                            SegmentIndex = record.StartIndex,
                            FinalScore = finalScore,
                            HashSimilarity = hashSimilarity,
                            FeatureSimilarity = featureSimilarity,
                            DurationSimilarity = durationSimilarity
                        };

                        results.Add(result);
                    }
                }
            }

            return results;
        }

        private double[] ParseHash(string hash)
        {
            string[] parts = hash.Split(',');
            int length = parts.Length;
            double[] values = new double[length];

            for (int index = 0; index < length; index++)
            {
                values[index] = double.Parse(parts[index]);
            }

            return values;
        }
    }
}
