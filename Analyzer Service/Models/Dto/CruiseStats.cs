namespace Analyzer_Service.Models.Dto
{
    public class CruiseStats
    {

        public bool hasCruiseStats;
        public double cruiseMeanZ;
        public double cruiseStdZ;

        public CruiseStats(bool hasCruiseStats, double cruiseMeanZ, double cruiseStdZ) {
            this.hasCruiseStats = hasCruiseStats;
            this.cruiseStdZ = cruiseStdZ;
            this.cruiseMeanZ = cruiseMeanZ;
        }
    }
}
