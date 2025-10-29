using Analyzer_Service.Models.Algorithms;
using Analyzer_Service.Models.Ro.Algorithms;

namespace Analyzer_Service.Services.Algorithms
{
    public class AutoCausalitySelector : IAutoCausalitySelector
    {
        public CausalitySelectionResult SelectAlgorithm(List<double> sourceSeries, List<double> targetSeries)
        {
            if (sourceSeries.Count == 0 || targetSeries.Count == 0)
                return new CausalitySelectionResult("None", "Empty data series provided.", 0.0, 0.0);

            double pearson = ComputePearsonCorrelation(sourceSeries, targetSeries);
            double derivativeCorrelation = ComputePearsonCorrelation(
                ComputeDifferences(sourceSeries),
                ComputeDifferences(targetSeries)
            );

            string selectedAlgorithm;
            string reasoning;

            if (pearson >= 0.65)
            {
                selectedAlgorithm = "Granger";
                reasoning = $"Strong or moderate linear correlation detected (Pearson={pearson:F2}) — using Granger causality.";
            }
            else
            {
                selectedAlgorithm = "CCM";
                reasoning = $"Weak linear correlation (Pearson={pearson:F2}) — nonlinear dependency suspected, using CCM.";
            }

            return new CausalitySelectionResult(selectedAlgorithm, reasoning, pearson, derivativeCorrelation);
        }


        private List<double> ComputeDifferences(List<double> series)
        {
            List<double> differences = new List<double>();
            for (int i = 1; i < series.Count; i++)
            {
                differences.Add(series[i] - series[i - 1]);
            }
            return differences;
        }

        private double ComputePearsonCorrelation(List<double> firstSeries, List<double> secondSeries)
        {
            int sampleCount = Math.Min(firstSeries.Count, secondSeries.Count);
            if (sampleCount == 0)
                return 0.0;

            double meanFirst = firstSeries.Average();
            double meanSecond = secondSeries.Average();

            double covariance = 0.0;
            double varianceFirst = 0.0;
            double varianceSecond = 0.0;

            for (int i = 0; i < sampleCount; i++)
            {
                double deviationFirst = firstSeries[i] - meanFirst;
                double deviationSecond = secondSeries[i] - meanSecond;
                covariance += deviationFirst * deviationSecond;
                varianceFirst += deviationFirst * deviationFirst;
                varianceSecond += deviationSecond * deviationSecond;
            }

            double denominator = Math.Sqrt(varianceFirst * varianceSecond);
            if (denominator == 0.0)
                return 0.0;

            return covariance / denominator;
        }
    }
}
