using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Algorithms;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class SignalPreprocessor : ISignalPreprocessor
    {
        private readonly ISignalProcessingUtility signalProcessingUtility;

        public SignalPreprocessor(ISignalProcessingUtility signalProcessingUtility)
        {
            this.signalProcessingUtility = signalProcessingUtility;
        }

        public double[] Apply(
            List<double> inputSignalValues,
            int hampelWindowSize,
            double hampelSigmaThreshold)
        {
            double[] initialValues = inputSignalValues.ToArray();

            double[] filteredValues =
                signalProcessingUtility.ApplyHampel(initialValues, hampelWindowSize, hampelSigmaThreshold);

            List<double> normalizedValues =
                signalProcessingUtility.ApplyZScore(filteredValues);

            return normalizedValues.ToArray();
        }
    }
}
