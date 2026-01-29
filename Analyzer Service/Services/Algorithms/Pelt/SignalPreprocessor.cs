using System.Collections.Generic;
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
            List<double> inputSignalValues,
            int hampelWindowSize,
            double hampelSigmaThreshold)
        {
            double[] initialValues = inputSignalValues.ToArray();

            double[] filteredValues =
                signalProcessingUtility.ApplyHampel(initialValues, hampelWindowSize, hampelSigmaThreshold);

            double[] normalizedValues =
                signalProcessingUtility.ApplyZScore(filteredValues);

            return normalizedValues;
        }
    }
}
