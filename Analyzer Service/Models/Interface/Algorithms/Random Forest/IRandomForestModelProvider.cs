using System.Text.Json;

namespace Analyzer_Service.Models.Interface.Algorithms.Random_Forest
{
    public interface IRandomForestModelProvider
    {
        public JsonDocument ModelDocument { get; }

        public List<string> FeatureNames { get; }
        public List<string> Labels { get; }

        public double[] ScalerMean { get; }
        public double[] ScalerScale { get; }

        public Dictionary<string, double> BuildFeatureDictionary(double[] featureVector);
    }
}
