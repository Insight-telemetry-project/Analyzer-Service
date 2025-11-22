using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Mongo
{
    public interface IPrepareFlightData
    {
        Task<SignalSeries> PrepareFlightDataAsync(int masterIndex, string xField, string yField);
        Task<List<double>> PrepareYAsync(int masterIndex, string fieldName);

    }
}
