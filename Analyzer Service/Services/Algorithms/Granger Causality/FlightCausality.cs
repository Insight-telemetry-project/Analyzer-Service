using Analyzer_Service.Models.Algorithms;
using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Ccm;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Ro.Algorithms;
using Analyzer_Service.Models.Schema;
using MongoDB.Driver;
using System.Collections.Concurrent;
using static Analyzer_Service.Models.Algorithms.Type.Types;

namespace Analyzer_Service.Services.Algorithms
{
    public class FlightCausality : IFlightCausality
    {
        private readonly IAutoCausalitySelector autoSelector;
        private readonly IGrangerCausalityAnalyzer grangerCausalityAnalyzer;
        private readonly ICcmCausalityAnalyzer ccmCausalityAnalyzer;
        private readonly IFlightTelemetryMongoProxy mongoProxy;

        private readonly ConcurrentBag<ConnectionResult> pendingConnections =
            new ConcurrentBag<ConnectionResult>();

        public FlightCausality(
            IGrangerCausalityAnalyzer grangerAnalyzer,
            ICcmCausalityAnalyzer ccmAnalyzer,
            IAutoCausalitySelector autoSelectorService,
            IFlightTelemetryMongoProxy flightTelemetryMongoProxy)
        {
            grangerCausalityAnalyzer = grangerAnalyzer;
            ccmCausalityAnalyzer = ccmAnalyzer;
            autoSelector = autoSelectorService;
            mongoProxy = flightTelemetryMongoProxy;

        }
        public async Task<FlightCausalityAnalysisResult> AnalyzeFlightAsync(int masterIndex)
        {
            IAsyncCursor<TelemetrySensorFields> cursor =await mongoProxy.GetCursorFromFieldsAsync(masterIndex);

            int flightLength = await mongoProxy.GetFlightLengthAsync(masterIndex);

            Dictionary<string, ParameterSeries> telemetryByField =
                await BuildTelemetryDictionaryAsync(cursor);

            List<CausalityRelation> fieldPairs =
                CreateFieldPairs(telemetryByField);

            int lagCount = Math.Max(
                ConstantAlgorithm.MIN_LAG,
                flightLength / ConstantAlgorithm.LAG_DIVISOR);

            ConcurrentBag<PairCausalityResult> analysisResults =
                await ProcessAllPairsAsync(
                    masterIndex,
                    fieldPairs,
                    telemetryByField,
                    lagCount);

            await StorePendingConnectionsAsync();

            return new FlightCausalityAnalysisResult
            {
                Flight = masterIndex,
                TotalPairs = fieldPairs.Count,
                Results = analysisResults.ToList()
            };
        }

        private async Task<Dictionary<string, ParameterSeries>> BuildTelemetryDictionaryAsync(IAsyncCursor<TelemetrySensorFields> cursor)
        {
            Dictionary<string, List<double>> temp = new Dictionary<string, List<double>>();

            await foreach (TelemetrySensorFields record in cursor.ToAsyncEnumerable())
            {
                foreach (KeyValuePair<string,double> entry in record.Fields)
                {
                    if (!temp.ContainsKey(entry.Key))
                        temp[entry.Key] = new List<double>();

                    temp[entry.Key].Add(entry.Value);
                }
            }

            return temp.ToDictionary(
                kvp => kvp.Key,
                kvp => new ParameterSeries(kvp.Key, kvp.Value)
            );
        }


        private List<CausalityRelation> CreateFieldPairs(
            Dictionary<string, ParameterSeries> telemetryByField)
        {
            List<string> fieldNames = telemetryByField.Keys.ToList();

            return fieldNames
                .SelectMany(
                    source => fieldNames.Where(target => target != source),
                    (source, target) => new CausalityRelation(source, target))
                .ToList();
        }

        private async Task<ConcurrentBag<PairCausalityResult>> ProcessAllPairsAsync(
            int masterIndex,
            List<CausalityRelation> pairs,
            Dictionary<string, ParameterSeries> telemetryByField,
            int lagCount)
        {
            ConcurrentBag<PairCausalityResult> bag =
                new ConcurrentBag<PairCausalityResult>();

            await Parallel.ForEachAsync(pairs, async (pair, token) =>
            {
                PairCausalityResult result =
                    await AnalyzePairAsync(
                        masterIndex,
                        pair.CauseParameter,
                        pair.EffectParameter,
                        lagCount,
                        ConstantAlgorithm.CCM_EMBEDDING_DIM,
                        ConstantAlgorithm.CCM_DELAY,
                        telemetryByField);

                bag.Add(result);
            });

            return bag;
        }

        private async Task StorePendingConnectionsAsync()
        {
            List<ConnectionResult> allConnections = pendingConnections.ToList();

            if (allConnections.Count > 0)
            {
                await mongoProxy.StoreConnectionsBulkAsync(allConnections);
            }

            pendingConnections.Clear();
        }



        private async Task<PairCausalityResult> AnalyzePairAsync(
            int masterIndex,
            string sourceFieldName,
            string targetFieldName,
            int lagCount,
            int embeddingDimension,
            int embeddingDelay,
            Dictionary<string, ParameterSeries> telemetryByField)
        {
            List<double> sourceSeries =
                telemetryByField.ContainsKey(sourceFieldName)
                    ? telemetryByField[sourceFieldName].Values
                    : new List<double>();

            List<double> targetSeries =
                telemetryByField.ContainsKey(targetFieldName)
                    ? telemetryByField[targetFieldName].Values
                    : new List<double>();

            CausalitySelectionResult selection =
                autoSelector.SelectAlgorithm(sourceSeries, targetSeries);

            CausalityAlgorithm algorithm = selection.SelectedAlgorithm;
            double pearson = selection.PearsonCorrelation;

            double grangerScore = 0.0;
            double ccmScore = 0.0;

            if (algorithm == CausalityAlgorithm.Granger)
            {
                grangerScore = grangerCausalityAnalyzer
                    .ComputeCausality(sourceSeries, targetSeries, lagCount);
            }

            if (algorithm == CausalityAlgorithm.Ccm)
            {
                ccmScore = ccmCausalityAnalyzer
                    .ComputeCausality(
                        sourceSeries,
                        targetSeries,
                        embeddingDimension,
                        embeddingDelay);
            }

            bool shouldStore = (algorithm == CausalityAlgorithm.Granger &&grangerScore >= ConstantAlgorithm.GRANGER_CAUSALITY_THRESHOLD)||
                (algorithm == CausalityAlgorithm.Ccm &&ccmScore >= ConstantAlgorithm.CCM_CAUSALITY_THRESHOLD)||
                (Math.Abs(pearson) >= ConstantAlgorithm.PEARSON_STRONG_THRESHOLD);

            if (shouldStore)
            {
                pendingConnections.Add(
                    new ConnectionResult(
                        masterIndex,
                        sourceFieldName,
                        targetFieldName,
                        algorithm));
            }

            return new PairCausalityResult
            {
                MasterIndex = masterIndex,
                SourceField = sourceFieldName,
                TargetField = targetFieldName,
                SelectedAlgorithm = algorithm,
                GrangerValue = grangerScore,
                CcmValue = ccmScore
            };
        }
    }
}
