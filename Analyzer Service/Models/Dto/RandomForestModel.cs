using System.Text.Json;

namespace Analyzer_Service.Models.Dto
{
    public class RandomForestModel
    {
        public JsonElement Forest { get; }
        public string[] Labels { get; }
        public string[] FeatureNames { get; }
        public double[] ScalerMean { get; }
        public double[] ScalerScale { get; }

        public RandomForestModel(
            JsonElement forest,
            string[] labels,
            string[] featureNames,
            double[] scalerMean,
            double[] scalerScale)
        {
            Forest = forest;
            Labels = labels;
            FeatureNames = featureNames;
            ScalerMean = scalerMean;
            ScalerScale = scalerScale;
        }
    }
}

    
