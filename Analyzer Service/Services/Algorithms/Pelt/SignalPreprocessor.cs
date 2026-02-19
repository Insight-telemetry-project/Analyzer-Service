using Analyzer_Service.Models.Interface.Algorithms;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;

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
            double[] inputSignalValues,
            int hampelWindowSize,
            double hampelSigmaThreshold)
        {
            double[] filteredValues =
                signalProcessingUtility.ApplyHampel(
                    inputSignalValues,
                    hampelWindowSize,
                    hampelSigmaThreshold);

            double[] normalizedValues =
                signalProcessingUtility.ApplyZScore(filteredValues);

            return normalizedValues;
        }
    }
}
