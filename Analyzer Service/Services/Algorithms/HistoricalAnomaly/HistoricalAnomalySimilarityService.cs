using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
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
        private readonly ISegmentClassificationService segmentClassifier;
        public HistoricalAnomalySimilarityService(
            IFlightTelemetryMongoProxy mongoProxy,
            IHistoricalAnomalySimilarityLogic logic,
            ISegmentClassificationService segmentClassification)
        {
            this.mongoProxy = mongoProxy;
            this.logic = logic;
            this.segmentClassifier = segmentClassification;
        }

        public async Task<List<HistoricalSimilarityResult>> FindSimilarAnomaliesAsync(
    int masterIndex,
    string parameterName)
        {
            var classification = await segmentClassifier.ClassifyWithAnomaliesAsync(
                masterIndex,
                parameterName,
                0,
                0);

            List<SegmentClassificationResult> segments = classification.Segments;
            List<int> anomalyIndices = classification.Anomalies;

            List<HistoricalSimilarityResult> finalResults = new List<HistoricalSimilarityResult>();

            foreach (int anomalyIndex in anomalyIndices)
            {
                SegmentClassificationResult segment = segments[anomalyIndex];

                string label = segment.Label;
                double[] newHashVector = segment.HashVector;
                Dictionary<string, double> newFeatureVector = segment.FeatureValues;
                SegmentBoundary boundary = segment.Segment;
                double newDuration = boundary.EndIndex - boundary.StartIndex;
                double threshold = 0.75;

                IAsyncCursor<HistoricalAnomalyRecord> cursor =
                    await mongoProxy.GetHistoricalCandidatesAsync(
                        parameterName,
                        label,
                        masterIndex);

                while (await cursor.MoveNextAsync())
                {
                    foreach (HistoricalAnomalyRecord record in cursor.Current)
                    {
                        if (record.MasterIndex == masterIndex)
                            continue;
                        double[] existingHash = ParseHash(record.PatternHash);
                        Dictionary<string, double> existingFeatures = record.FeatureValues;
                        double existingDuration = record.EndIndex - record.StartIndex;

                        double hashSimilarity = logic.CompareHashesFuzzy(existingHash, newHashVector);
                        double featureSimilarity = logic.CompareFeatureVectors(existingFeatures, newFeatureVector);
                        double durationSimilarity = logic.CompareDurationSimilarity(existingDuration, newDuration);

                        double finalScore = logic.ComputeWeightedScore(
                            hashSimilarity,
                            featureSimilarity,
                            durationSimilarity);

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

                            finalResults.Add(result);
                        }
                    }
                }
            }

            return finalResults;
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
