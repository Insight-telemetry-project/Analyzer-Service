using System.Text.Json;

namespace Analyzer_Service.Models.Interface.Algorithms.Random_Forest
{
    public interface IRandomForestModelProvider
    {
        JsonDocument ModelDocument { get; }

        List<string> FeatureNames { get; }
        List<string> Labels { get; }

        double[] ScalerMean { get; }
        double[] ScalerScale { get; }

        Dictionary<string, double> BuildFeatureDictionary(double[] featureVector);
    }
}
