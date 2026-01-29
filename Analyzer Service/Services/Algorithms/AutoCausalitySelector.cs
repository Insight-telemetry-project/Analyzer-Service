using Analyzer_Service.Models.Algorithms;
using Analyzer_Service.Models.Algorithms.Type;
using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Ro.Algorithms;
using static Analyzer_Service.Models.Algorithms.Type.Types;

namespace Analyzer_Service.Services.Algorithms
{
    public class AutoCausalitySelector : IAutoCausalitySelector
    {
        public CausalitySelectionResult SelectAlgorithm(
            List<double> sourceSeries,
            List<double> targetSeries)
        {
            double pearsonCorrelation = ComputePearsonCorrelation(sourceSeries, targetSeries);

            double derivativeCorrelation =
                ComputePearsonCorrelation(
                    ComputeDifferences(sourceSeries),
                    ComputeDifferences(targetSeries));

            CausalityAlgorithm selectedAlgorithm;

            if (Math.Abs(pearsonCorrelation) >= ConstantAlgorithm.PEARSON_LINEAR_THRESHOLD)
            {
                selectedAlgorithm = CausalityAlgorithm.Granger;
            }
            else
            {
                selectedAlgorithm = CausalityAlgorithm.Ccm;
            }

            return new CausalitySelectionResult(
                selectedAlgorithm,
                pearsonCorrelation,
                derivativeCorrelation);
        }

        private List<double> ComputeDifferences(List<double> series)
        {
            List<double> differences = new List<double>();

            for (int indexSeries = 1; indexSeries < series.Count; indexSeries++)
            {
                double delta = series[indexSeries] - series[indexSeries - 1];
                differences.Add(delta);
            }

            return differences;
        }

        private double ComputePearsonCorrelation(
            List<double> firstSeries,
            List<double> secondSeries)
        {
            int sampleCount = Math.Min(firstSeries.Count, secondSeries.Count);


            double meanFirst = firstSeries.Average();
            double meanSecond = secondSeries.Average();

            double covariance = 0.0;
            double varianceFirst = 0.0;
            double varianceSecond = 0.0;

            for (int indexSeries = 0; indexSeries < sampleCount; indexSeries++)
            {
                double deviationFirst = firstSeries[indexSeries] - meanFirst;
                double deviationSecond = secondSeries[indexSeries] - meanSecond;

                covariance += deviationFirst * deviationSecond;
                varianceFirst += deviationFirst * deviationFirst;
                varianceSecond += deviationSecond * deviationSecond;
            }

            double denominator = Math.Sqrt(varianceFirst * varianceSecond);

            return covariance / denominator;
        }
    }
}
