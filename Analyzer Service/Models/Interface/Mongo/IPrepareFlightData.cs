using Analyzer_Service.Models.Dto;

namespace Analyzer_Service.Models.Interface.Mongo
{
    public interface IPrepareFlightData
    {
        public Task<SignalSeries> PrepareFlightDataAsync(int masterIndex, string xField, string yField);
        public Task<List<double>> PrepareYAsync(int masterIndex, string fieldName);

        public Task<List<double>> GetParameterValuesAsync(int masterIndex, string parameterName);

        public Task<List<double>> GetParameterValuesCopyAsync(int masterIndex, string parameterName);


    }
}
