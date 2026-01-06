using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;
using Analyzer_Service.Models.Interface.Algorithms;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class SignalPreprocessor : ISignalPreprocessor 
    {
        private readonly ISignalProcessingUtility signalProcessingUtility; // Discussion: use _ prefix for private fields
        // Discussion: why are you using a utility for this? the resposibility of this class is to preprocess the signal, so it should contain the logic itself
        public SignalPreprocessor(ISignalProcessingUtility signalProcessingUtility)
        {
            this.signalProcessingUtility = signalProcessingUtility;
        }

        public double[] Apply( // Discussion, fix formatting all along the project, also rename this to process, apply is too generic
            List<double> inputSignalValues, 
            int hampelWindowSize,
            double hampelSigmaThreshold)
        {
            // Discussion: overall in this method you duplicated your values 3 times and allocated a bunch of unnecessary memory, fix using IEnumerable and proper usage of the mongo cursor
            double[] initialValues = inputSignalValues.ToArray(); // Discussion: this is another case where you allocate a bunch of memory and duplicate your data unnecessarily

            double[] filteredValues =
                signalProcessingUtility.ApplyHampel(initialValues, hampelWindowSize, hampelSigmaThreshold); 

            List<double> normalizedValues =
                signalProcessingUtility.ApplyZScore(filteredValues); 

            return normalizedValues.ToArray();
        }
    }
}
