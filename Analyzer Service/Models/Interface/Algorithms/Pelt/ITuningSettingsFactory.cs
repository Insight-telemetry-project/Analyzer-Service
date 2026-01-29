using Analyzer_Service.Models.Enums;
using Analyzer_Service.Services.Algorithms.Pelt;

namespace Analyzer_Service.Models.Interface.Algorithms.Pelt
{
    public interface ITuningSettingsFactory
    {
        PeltTuningSettings Get(flightStatus status);
    }
}
