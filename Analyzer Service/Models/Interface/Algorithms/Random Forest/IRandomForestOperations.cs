using System.Text.Json;

namespace Analyzer_Service.Models.Interface.Algorithms.Random_Forest
{
    public interface IRandomForestOperations
    {
        public double[] ScaleFeatures(double[] featureVector, JsonElement meanElement, JsonElement scaleElement);

        public int PredictTree(JsonElement treeElement, double[] scaledFeatureVector);

        public string PredictLabel(IRandomForestModelProvider provider, double[] rawFeatureVector);
    }
}
