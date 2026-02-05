using Analyzer_Service.Models.Dto;
using Analyzer_Service.Models.Schema;

namespace Analyzer_Service.Models.Interface.Mongo
{
    public interface IPrepareFlightData
    {
         Task<SignalSeries> PrepareFlightDataAsync(int masterIndex, string xField, string yField);
         Task<List<double>> PrepareYAsync(int masterIndex, string fieldName);

        Task<IReadOnlyList<double>> GetParameterValuesAsync(int masterIndex, string parameterName);

         Task<List<double>> GetParameterValuesCopyAsync(int masterIndex, string parameterName);

        Task<List<HistoricalAnomalyRecord>> GetFlightPointsByParameterAsync(int masterIndex, string parameterName);

        Task<long> GetFlightStartEpochSecondsAsync(int masterIndex);

    }
}
