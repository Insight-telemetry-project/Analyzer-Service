using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Enums;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Analyze;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Ro.Algorithms;

namespace Analyzer_Service.Services.Analyze
{
    public class AnalyzeServices: IAnalyzeServices
    {
        private readonly IFlightCausality _flightCausality;
        private readonly ISegmentClassificationService _segmentClassifier;
        private readonly IHistoricalAnomalySimilarityService _historicalSimilarityService;
        private readonly IFlightPhaseAnalysisService _phaseAnalysis;
        private readonly IFlightPhaseDetector _flightPhaseDetector;
        private readonly IFlightTelemetryMongoProxy _flightTelemetryMongoProxy;


        public AnalyzeServices(
            IFlightCausality flightCausalityService,
            ISegmentClassificationService segmentClassifier,
            IHistoricalAnomalySimilarityService historicalSimilarityService,
            IFlightPhaseDetector flightPhaseDetector,
            IFlightPhaseAnalysisService phaseAnalysis,
            IFlightTelemetryMongoProxy flightTelemetryMongoProxy
)
        {
            _flightCausality = flightCausalityService;
            _segmentClassifier = segmentClassifier;
            _historicalSimilarityService = historicalSimilarityService;
            _flightPhaseDetector = flightPhaseDetector;
            _phaseAnalysis = phaseAnalysis;
            _flightTelemetryMongoProxy = flightTelemetryMongoProxy;

        }

        public async Task Analyze(int flightId)
        {

            List<Task> segmentTasks = ParamterList.ParameterNames
                .Select(async fieldName =>
                {
                    FlightPhaseIndexes phaseIndexes =
                        await _phaseAnalysis.GetPhaseIndexesAsync(flightId, fieldName);

                    int takeoffEndIndex = phaseIndexes.TakeoffEndIndex;
                    int landingStartIndex = phaseIndexes.LandingStartIndex;

                    Task takeoffTask =
                        _segmentClassifier.ClassifyWithAnomaliesAsync(
                            flightId, fieldName, 0, takeoffEndIndex, flightStatus.TakeOf_Landing);

                    Task cruiseTask =
                        _segmentClassifier.ClassifyWithAnomaliesAsync(
                            flightId, fieldName, takeoffEndIndex, landingStartIndex, flightStatus.Cruising);

                    Task landingTask =
                        _segmentClassifier.ClassifyWithAnomaliesAsync(
                            flightId, fieldName, landingStartIndex, int.MaxValue, flightStatus.TakeOf_Landing);

                    await Task.WhenAll(takeoffTask, cruiseTask, landingTask);

                }).ToList();



            Task causalityTask =
                _flightCausality.AnalyzeFlightAsync(flightId);



            List<int> flightNumbers =
                await _flightTelemetryMongoProxy.GetAllFlightNumbers();

            Task historyTask = Parallel.ForEachAsync(
                flightNumbers,
                async (flight, cancellationToken) =>
                {
                    await AnalyzeFlightHistoryById(flight);
                });



            await Task.WhenAll(
                Task.WhenAll(segmentTasks),
                causalityTask,
                historyTask
            );
        }

        public async Task AnalyzeFlightHistoryById(int flightId)
        {
            foreach (string fieldName in ParamterList.ParameterNames)
            {
                List<HistoricalSimilarityResult> results = await _historicalSimilarityService.FindSimilarAnomaliesAsync(flightId, fieldName, flightStatus.FullFlight);
            }
        }
    }
}
