using Analyzer_Service.Models.Enums;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class TuningSettingsFactory : ITuningSettingsFactory
    {
        private readonly IConfiguration configuration;
        public PeltTuningSettings[] settingsArr = Enumerable.Repeat(new PeltTuningSettings(),3).ToArray();
        public TuningSettingsFactory(IConfiguration configuration)
        {
            this.configuration = configuration;

            configuration.GetSection("AnomalyTuningProfiles:TakeoffLanding").Bind(settingsArr[0]);
            configuration.GetSection("AnomalyTuningProfiles:Cruising").Bind(settingsArr[1]);
            configuration.GetSection("AnomalyTuningProfiles:FullFlight").Bind(settingsArr[2]);
        }

        public PeltTuningSettings Get(flightStatus status)
        {
            switch (status)
            {
                case flightStatus.TakeOf_Landing:
                    return this.settingsArr[0];

                case flightStatus.Cruising:

                    return this.settingsArr[1];

                default:
                    return this.settingsArr[2];
            }
        }
    }
}
