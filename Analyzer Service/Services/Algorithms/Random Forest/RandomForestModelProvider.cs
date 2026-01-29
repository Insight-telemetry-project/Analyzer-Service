using Analyzer_Service.Models.Constant;
using Analyzer_Service.Models.Dto;
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

            JsonElement rootElement = ModelDocument.RootElement;

            FeatureNames =
                rootElement
                    .GetProperty(ConstantRandomForest.FEATURE_NAMES_JSON)
                    .EnumerateArray()
                    .Select(field => field.GetString())
                    .ToList();

            Labels =
                rootElement
                    .GetProperty(ConstantRandomForest.LABELS_JSON)
                    .EnumerateArray()
                    .Select(field => field.GetString())
                    .ToList();

            JsonElement scalerElement = rootElement.GetProperty(ConstantRandomForest.SCALER_JSON);

            ScalerMean =
                scalerElement
                    .GetProperty(ConstantRandomForest.MEAN_JSON)
                    .EnumerateArray()
                    .Select(field => field.GetDouble())
                    .ToArray();

            ScalerScale =
                scalerElement
                    .GetProperty(ConstantRandomForest.SCALE_JSON)
                    .EnumerateArray()
                    .Select(field => field.GetDouble())
                    .ToArray();
        }

        public RandomForestModel GetModel()
        {
            RandomForestModel model = new RandomForestModel(
                ModelDocument.RootElement,
                Labels.ToArray(),
                FeatureNames.ToArray(),
                ScalerMean,
                ScalerScale);

            return model;
        }
    }
}
