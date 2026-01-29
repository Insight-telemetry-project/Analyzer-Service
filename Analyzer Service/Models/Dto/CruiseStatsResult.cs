namespace Analyzer_Service.Models.Dto
{
    public class CruiseStatsResult
    {
        public bool HasCruiseStats { get; private set; }
        public double CruiseMeanZ { get; private set; }
        public double CruiseStdZ { get; private set; }



        public static CruiseStatsResult CreateValid(double cruiseMeanZ, double cruiseStdZ)
        {
            CruiseStatsResult result = new CruiseStatsResult();
            result.HasCruiseStats = true;
            result.CruiseMeanZ = cruiseMeanZ;
            result.CruiseStdZ = cruiseStdZ;
            return result;
        }

        public static CruiseStatsResult CreateEmpty()
        {
            CruiseStatsResult result = new CruiseStatsResult();
            result.HasCruiseStats = false;
            result.CruiseMeanZ = 0.0;
            result.CruiseStdZ = 0.0;
            return result;
        }
    }
}
