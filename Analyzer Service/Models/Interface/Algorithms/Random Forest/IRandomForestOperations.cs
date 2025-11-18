using System.Text.Json;

namespace Analyzer_Service.Models.Interface.Algorithms.Random_Forest
{
    public interface IRandomForestOperations
    {
        double[] ScaleFeatures(double[] featureVector, JsonElement meanElement, JsonElement scaleElement);

        int PredictTree(JsonElement treeElement, double[] scaledFeatureVector);

        string PredictLabel(IRandomForestModelProvider provider, double[] rawFeatureVector);
    }
}
