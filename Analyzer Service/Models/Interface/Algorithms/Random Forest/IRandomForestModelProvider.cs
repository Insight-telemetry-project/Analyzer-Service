using Analyzer_Service.Models.Dto;
using System.Text.Json;

namespace Analyzer_Service.Models.Interface.Algorithms.Random_Forest
{
    public interface IRandomForestModelProvider
    {
        public RandomForestModel GetModel();


    }
}
