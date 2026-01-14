namespace Analyzer_Service.Models.Dto
{
    public class CruiseStats
    {

        public double cruiseMeanZ;
        public double cruiseStdZ;

        public CruiseStats( double cruiseMeanZ, double cruiseStdZ) {
            this.cruiseStdZ = cruiseStdZ;
            this.cruiseMeanZ = cruiseMeanZ;
        }
    }
}
