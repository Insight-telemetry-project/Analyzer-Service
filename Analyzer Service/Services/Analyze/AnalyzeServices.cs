using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Enums;
using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Clustering;
using Analyzer_Service.Models.Interface.Algorithms.HistoricalAnomaly;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Analyze;
using Analyzer_Service.Models.Interface.Mongo;
using Analyzer_Service.Models.Ro.Algorithms;
using System.Diagnostics;

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

        public async Task<List<long>> Analyze(int flightId)
        {
            Stopwatch totalSw = Stopwatch.StartNew();

            // ---------- 1️⃣ Segment ----------
            Stopwatch segmentSw = Stopwatch.StartNew();
            Task segmentTask = Task.WhenAll(
                ParamterList.ParameterNames.Select(async fieldName =>
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
                })
            );

            // ---------- 2️⃣ Causality ----------
            Stopwatch causalitySw = Stopwatch.StartNew();
            Task causalityTask = _flightCausality.AnalyzeFlightAsync(flightId);

            // ---------- 3️⃣ History ----------
            List<int> flightNumbers =
                await _flightTelemetryMongoProxy.GetAllFlightNumbers();

            Stopwatch historySw = Stopwatch.StartNew();
            Task historyTask = Parallel.ForEachAsync(
                flightNumbers,
                async (flight, cancellationToken) =>
                {
                    await AnalyzeFlightHistoryById(flight);
                });

            // ---------- מחכים לכל אחד ומודדים בנפרד ----------
            await segmentTask;
            segmentSw.Stop();

            await causalityTask;
            causalitySw.Stop();

            await historyTask;
            historySw.Stop();

            totalSw.Stop();

            Console.WriteLine("====== FULL PARALLEL RESULTS ======");
            Console.WriteLine($"Segment time: {segmentSw.ElapsedMilliseconds} ms");
            Console.WriteLine($"Causality time: {causalitySw.ElapsedMilliseconds} ms");
            Console.WriteLine($"History time: {historySw.ElapsedMilliseconds} ms");
            Console.WriteLine($"TOTAL execution time: {totalSw.ElapsedMilliseconds} ms");

            return new List<long>
        {
            segmentSw.ElapsedMilliseconds,
            causalitySw.ElapsedMilliseconds,
            historySw.ElapsedMilliseconds,
            totalSw.ElapsedMilliseconds
        };
        }


        //    public async Task<List<long>> Analyze(int flightId)
        //    {
        //        Stopwatch totalSw = Stopwatch.StartNew();

        //        // ---------------- 1️⃣ Segment - סדרתי ----------------
        //        Stopwatch segmentSw = Stopwatch.StartNew();

        //        foreach (string fieldName in ParamterList.ParameterNames)
        //        {
        //            FlightPhaseIndexes phaseIndexes =
        //                await _phaseAnalysis.GetPhaseIndexesAsync(flightId, fieldName);

        //            int takeoffEndIndex = phaseIndexes.TakeoffEndIndex;
        //            int landingStartIndex = phaseIndexes.LandingStartIndex;

        //            await _segmentClassifier.ClassifyWithAnomaliesAsync(
        //                flightId, fieldName, 0, takeoffEndIndex, flightStatus.TakeOf_Landing);

        //            await _segmentClassifier.ClassifyWithAnomaliesAsync(
        //                flightId, fieldName, takeoffEndIndex, landingStartIndex, flightStatus.Cruising);

        //            await _segmentClassifier.ClassifyWithAnomaliesAsync(
        //                flightId, fieldName, landingStartIndex, int.MaxValue, flightStatus.TakeOf_Landing);
        //        }

        //        segmentSw.Stop();


        //        // ---------------- 2️⃣ Causality - לבד ----------------
        //        Stopwatch causalitySw = Stopwatch.StartNew();

        //        await _flightCausality.AnalyzeFlightAsync(flightId);

        //        causalitySw.Stop();


        //        // ---------------- 3️⃣ History - סדרתי ----------------
        //        Stopwatch historySw = Stopwatch.StartNew();

        //        List<int> flightNumbers =
        //            await _flightTelemetryMongoProxy.GetAllFlightNumbers();

        //        foreach (int flight in flightNumbers)
        //        {
        //            foreach (string fieldName in ParamterList.ParameterNames)
        //            {
        //                await _historicalSimilarityService
        //                    .FindSimilarAnomaliesAsync(flight, fieldName, flightStatus.FullFlight);
        //            }
        //        }

        //        historySw.Stop();

        //        totalSw.Stop();

        //        Console.WriteLine("====== SEQUENTIAL BENCHMARK ======");
        //        Console.WriteLine($"Segment time: {segmentSw.ElapsedMilliseconds} ms");
        //        Console.WriteLine($"Causality time: {causalitySw.ElapsedMilliseconds} ms");
        //        Console.WriteLine($"History time: {historySw.ElapsedMilliseconds} ms");
        //        Console.WriteLine($"TOTAL execution time: {totalSw.ElapsedMilliseconds} ms");

        //        return new List<long>
        //{
        //    segmentSw.ElapsedMilliseconds,
        //    causalitySw.ElapsedMilliseconds,
        //    historySw.ElapsedMilliseconds,
        //    totalSw.ElapsedMilliseconds
        //};
        //    }

        public async Task AnalyzeFlightHistoryById(int flightId)
        {
            foreach (string fieldName in ParamterList.ParameterNames)
            {
                List<HistoricalSimilarityResult> results = await _historicalSimilarityService.FindSimilarAnomaliesAsync(flightId, fieldName, flightStatus.FullFlight);
            }
        }
    }
}
