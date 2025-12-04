using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Interface.Algorithms.Random_Forest;
using System.Text.Json;

namespace Analyzer_Service.Services.Algorithms.Random_Forest
{
    public class RandomForestModelProvider : IRandomForestModelProvider
    {
       public JsonDocument ModelDocument { get; }

        public List<string> FeatureNames { get; }
        public List<string> Labels { get; }

        public double[] ScalerMean { get; }
        public double[] ScalerScale { get; }

        public RandomForestModelProvider()
        {
            string jsonContent = File.ReadAllText(ConstantRandomForest.ML_FILE_PATH);

            ModelDocument = JsonDocument.Parse(jsonContent);

            JsonElement root = ModelDocument.RootElement;

            FeatureNames =
                root
                .GetProperty(ConstantRandomForest.FEATURE_NAMES_JSON)
                .EnumerateArray()
                .Select(field => field.GetString())
                .ToList();

            Labels =
                root
                .GetProperty(ConstantRandomForest.LABELS_JSON)
                .EnumerateArray()
                .Select(field => field.GetString())
                .ToList();

            JsonElement scaler = root.GetProperty(ConstantRandomForest.SCALER_JSON);

            ScalerMean =
                scaler
                .GetProperty(ConstantRandomForest.MEAN_JSON)
                .EnumerateArray()
                .Select(field => field.GetDouble())
                .ToArray();

            ScalerScale =
                scaler
                .GetProperty(ConstantRandomForest.SCALE_JSON)
                .EnumerateArray()
                .Select(field => field.GetDouble())
                .ToArray();
        }

        public Dictionary<string, double> BuildFeatureDictionary(double[] featureVector)
        {
            Dictionary<string, double> dictionary = new Dictionary<string, double>();

            for (int index = 0; index < FeatureNames.Count; index++)
            {
                dictionary[FeatureNames[index]] = featureVector[index];
            }

            return dictionary;
        }
    }
}
