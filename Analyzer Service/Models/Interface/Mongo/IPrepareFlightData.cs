using Analyzer_Service.Models.Schema;

namespace Analyzer_Service.Models.Interface.Mongo
{
    public interface IPrepareFlightData
    {
        Task<double[]> GetParameterValuesAsync(int masterIndex, string parameterName);

        Task<double[]> PrepareYAsync(int masterIndex, string fieldName);

        Task<List<HistoricalAnomalyRecord>> GetFlightPointsByParameterAsync(
            int masterIndex,
            string parameterName);

        Task<long> GetFlightStartEpochSecondsAsync(int masterIndex);
    }
}
