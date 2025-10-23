namespace Analyzer_Service.Models.Interface.Mongo
{
    public interface IPrepareFlightData
    {
        Task<(List<double> X, List<double> Y)> PrepareFlightDataAsync(int masterIndex, string xField, string yField);
    }
}
