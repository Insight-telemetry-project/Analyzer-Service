using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Enums;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Ro.Algorithms;
using Analyzer_Service.Models.Schema;
using Analyzer_Service.Services.Algorithms.Pelt;
using MongoDB.Driver;
namespace Analyzer_Service.Services.Algorithms.HistoricalAnomaly
{
    public class HistoricalAnomalySimilarityService : IHistoricalAnomalySimilarityService
    {
        private readonly IFlightTelemetryMongoProxy mongoProxy;
        private readonly IHistoricalAnomalySimilarityLogic logic;
        private readonly ITuningSettingsFactory _tuningSettingsFactory;
        private readonly IPrepareFlightData _prepareFlightData;

        public HistoricalAnomalySimilarityService(
            IFlightTelemetryMongoProxy mongoProxy,
            IHistoricalAnomalySimilarityLogic logic,
            ITuningSettingsFactory tuningSettingsFactory,
            IPrepareFlightData prepareFlightData)
        {
            this.mongoProxy = mongoProxy;
            this.logic = logic;
            this._tuningSettingsFactory = tuningSettingsFactory;
            this._prepareFlightData = prepareFlightData;
        }

        public async Task<List<HistoricalSimilarityResult>> FindSimilarAnomaliesAsync(
            int masterIndex,string parameterName,flightStatus status)
        {
            PeltTuningSettings settings = _tuningSettingsFactory.Get(status);

            List<HistoricalAnomalyRecord> flightPoints =
                await _prepareFlightData.GetFlightPointsByParameterAsync(masterIndex, parameterName);

            List<HistoricalSimilarityResult> finalResults = new List<HistoricalSimilarityResult>();
            List<HistoricalSimilarityPoint> pointsToStore = new List<HistoricalSimilarityPoint>();

            List<HistoricalAnomalyRecord> allCandidates =
                await mongoProxy.GetHistoricalCandidatesByParameterAsync(parameterName, masterIndex);

            Dictionary<string, List<HistoricalAnomalyRecord>> candidatesByLabel =
                new Dictionary<string, List<HistoricalAnomalyRecord>>();

            for (int indexCandidate = 0; indexCandidate < allCandidates.Count; indexCandidate++)
            {
                HistoricalAnomalyRecord candidate = allCandidates[indexCandidate];

                if (!candidatesByLabel.TryGetValue(candidate.Label, out List<HistoricalAnomalyRecord> labelList))
                {
                    labelList = new List<HistoricalAnomalyRecord>();
                    candidatesByLabel.Add(candidate.Label, labelList);
                }

                labelList.Add(candidate);
            }

            for (int indexPoint = 0; indexPoint < flightPoints.Count; indexPoint++)
            {
                HistoricalAnomalyRecord current = flightPoints[indexPoint];

                if (!candidatesByLabel.TryGetValue(current.Label, out List<HistoricalAnomalyRecord> candidatesForLabel))
                {
                    continue;
                }

                for (int indexCandidate = 0; indexCandidate < candidatesForLabel.Count; indexCandidate++)
                {
                    HistoricalAnomalyRecord candidate = candidatesForLabel[indexCandidate];

                    SimilarityScores similarity = ComputeSimilarity(candidate, current, status);

                    if (similarity.FinalScore >= settings.FINAL_SCORE)
                    {
                        HistoricalSimilarityResult result =
                            CreateResult(candidate, current, similarity);

                        finalResults.Add(result);

                        HistoricalSimilarityPoint point = new HistoricalSimilarityPoint
                        {
                            RecordId = candidate.Id.ToString(),
                            ComparedFlightIndex = candidate.MasterIndex,
                            StartIndex = candidate.StartIndex,
                            EndIndex = candidate.EndIndex,
                            Label = candidate.Label,
                            FinalScore = similarity.FinalScore
                        };

                        pointsToStore.Add(point);
                    }
                }
            }

            await mongoProxy.StoreHistoricalSimilarityAsync(masterIndex, parameterName, pointsToStore);

            return finalResults;
        }




        private SimilarityScores ComputeSimilarity(
            HistoricalAnomalyRecord record,HistoricalAnomalyRecord current,flightStatus status)
        {
            double[] existingHash = ParseHash(record.PatternHash);
            double[] newHash = ParseHash(current.PatternHash);

            double hashSim = logic.CompareHashesFuzzy(existingHash, newHash, status);
            double featureSim = logic.CompareFeatureVectors(record.FeatureValues, current.FeatureValues);

            double existingDuration = record.EndIndex - record.StartIndex;
            double newDuration = current.EndIndex - current.StartIndex;

            double durationSim = logic.CompareDurationSimilarity(existingDuration, newDuration);

            double final = logic.ComputeWeightedScore(hashSim, featureSim, durationSim, status);

            return new SimilarityScores
            {
                FinalScore = final,
                HashSimilarity = hashSim,
                FeatureSimilarity = featureSim,
                DurationSimilarity = durationSim
            };
        }



        private HistoricalSimilarityResult CreateResult(
            HistoricalAnomalyRecord record,HistoricalAnomalyRecord current,SimilarityScores similarityScores)
        {
            return new HistoricalSimilarityResult
            {
                MasterIndex = record.MasterIndex,

                HistoricalStartIndex = record.StartIndex,
                HistoricalEndIndex = record.EndIndex,
                HistoricalLabel = record.Label,

                NewStartIndex = current.StartIndex,
                NewEndIndex = current.EndIndex,
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
        

    }
}
