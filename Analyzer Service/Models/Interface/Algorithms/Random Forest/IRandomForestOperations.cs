using Analyzer_Service.Models.Dto;
using System.Text.Json;

namespace Analyzer_Service.Models.Interface.Algorithms.Random_Forest
{
    public interface IRandomForestOperations
    {
        public double[] ScaleFeatures(double[] featureVector, double[] meanArray, double[] scaleArray);

        public int PredictTree(JsonElement tree, double[] scaledFeatures);
        public string PredictLabel(RandomForestModel model, double[] rawFeatures);
    }
}
