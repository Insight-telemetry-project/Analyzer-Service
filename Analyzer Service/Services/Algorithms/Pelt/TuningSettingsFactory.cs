using Analyzer_Service.Models.Enums;
using Analyzer_Service.Models.Interface.Algorithms.Pelt;

namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class TuningSettingsFactory : ITuningSettingsFactory
    {
        private readonly IConfiguration configuration;

        public TuningSettingsFactory(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public PeltTuningSettings Get(flightStatus status)
        {
            string key = status switch
            {
                flightStatus.TakeOf_Landing => "AnomalyTuningProfiles:TakeoffLanding",
                flightStatus.Cruising => "AnomalyTuningProfiles:Cruising",
                _ => "AnomalyTuningProfiles:FullFlight"
            };


            //switch (status)
            //{
            //    case flightStatus.TakeOf_Landing:
            //        key = "AnomalyTuningProfiles:TakeoffLanding";
            //        break;

            //    case flightStatus.Cruising:
            //        key = "AnomalyTuningProfiles:Cruising";
            //        break;

            //    case flightStatus.FullFlight:
            //        key = "AnomalyTuningProfiles:FullFlight";
            //        break;
            //}

            PeltTuningSettings settings = new PeltTuningSettings();
            configuration.GetSection(key).Bind(settings);
            return settings;
        }
    }
}
