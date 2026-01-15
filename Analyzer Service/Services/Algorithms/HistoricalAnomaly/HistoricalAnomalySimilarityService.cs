using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Enums;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Ro.Algorithms;
using Analyzer_Service.Models.Schema;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Threading.Tasks;
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
            SegmentAnalysisResult classification =
                await segmentClassifier.ClassifyWithAnomaliesAsync(masterIndex, parameterName, 0, 0, flightStatus.FullFlight);

            List<HistoricalSimilarityResult> finalResults = new List<HistoricalSimilarityResult>();

            for (int indexAnomalyIndexes = 0; indexAnomalyIndexes < classification.AnomalyIndexes.Count; indexAnomalyIndexes++)
            {
                int sampleIndex = classification.AnomalyIndexes[indexAnomalyIndexes];

                int segmentIndex =
                    MapSampleIndexToSegmentIndex(sampleIndex, classification.SegmentBoundaries);

                if (segmentIndex == -1)
                {
                    continue;
                }

                await ProcessSingleAnomalyAsync(
                    segmentIndex,
                    masterIndex,
                    parameterName,
                    classification.Segments,
                    finalResults);

            }

            return finalResults;
        }
        private async Task ProcessSingleAnomalyAsync(
           int anomalyIndex,
           int masterIndex,
           string parameterName,
           List<SegmentClassificationResult> segments,
           List<HistoricalSimilarityResult> finalResults)
        {
            SegmentClassificationResult current = segments[anomalyIndex];

            IAsyncCursor<HistoricalAnomalyRecord> cursor =
                await mongoProxy.GetHistoricalCandidatesAsync(
                    parameterName,
                    current.Label,
                    masterIndex);

            while (await cursor.MoveNextAsync())
            {
                foreach (HistoricalAnomalyRecord record in cursor.Current)
                {
                    if (record.MasterIndex == masterIndex)
                    {
                        continue;
                    }

                    SimilarityScores similarity = ComputeSimilarity(record, current);

                    if (similarity.FinalScore >= ConstantAnomalyDetection.FINAL_SCORE)
                    {
                        HistoricalSimilarityResult result =
                            CreateResult(record, current, similarity);

                        finalResults.Add(result);
                    }
                }
            }
        }


        private SimilarityScores ComputeSimilarity(HistoricalAnomalyRecord record,SegmentClassificationResult current)
        {
            double[] existingHash = ParseHash(record.PatternHash);
            double[] newHash = current.HashVector;

            double hashSim = logic.CompareHashesFuzzy(existingHash, newHash);
            double featureSim = logic.CompareFeatureVectors(record.FeatureValues, current.FeatureValues);

            double existingDuration = record.EndIndex - record.StartIndex;
            double newDuration = current.Segment.EndIndex - current.Segment.StartIndex;

            double durationSim = logic.CompareDurationSimilarity(existingDuration, newDuration);

            double final = logic.ComputeWeightedScore(hashSim, featureSim, durationSim);

            return new SimilarityScores
            {
                FinalScore = final,
                HashSimilarity = hashSim,
                FeatureSimilarity = featureSim,
                DurationSimilarity = durationSim
            };
        }


        private HistoricalSimilarityResult CreateResult(HistoricalAnomalyRecord record,SegmentClassificationResult current,SimilarityScores similarityScores)
        {
            return new HistoricalSimilarityResult
            {
                MasterIndex = record.MasterIndex,

                HistoricalStartIndex = record.StartIndex,
                HistoricalEndIndex = record.EndIndex,
                HistoricalLabel = record.Label,

                NewStartIndex = current.Segment.StartIndex,
                NewEndIndex = current.Segment.EndIndex,
                NewLabel = current.Label,

                FinalScore = similarityScores.FinalScore,
                HashSimilarity = similarityScores.HashSimilarity,
                FeatureSimilarity = similarityScores.FeatureSimilarity,
                DurationSimilarity = similarityScores.DurationSimilarity
            };
        }


        private double[] ParseHash(string hash)
        {
            string[] parts = hash.Split(ConstantAnomalyDetection.HASH_SPLIT);
            int length = parts.Length;

            double[] values = new double[length];

            for (int index = 0; index < length; index++)
            {
                values[index] = double.Parse(parts[index]);
            }

            return values;
        }

        private static int MapSampleIndexToSegmentIndex(int sampleIndex,List<SegmentBoundary> segmentBoundaries)
        {
            for (int segmentIndex = 0; segmentIndex < segmentBoundaries.Count; segmentIndex++)
            {
                SegmentBoundary boundary = segmentBoundaries[segmentIndex];

                if (sampleIndex >= boundary.StartIndex && sampleIndex <= boundary.EndIndex)
                {
                    return segmentIndex;
                }
            }

            return -1;
        }

    }
}
