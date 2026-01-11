namespace Analyzer_Service.Models.Dto
{
    public class FlightPhaseIndexes
    {
        public int TakeoffEndIndex { get; }
        public int LandingStartIndex { get; }

        public FlightPhaseIndexes(int takeoffEndIndex, int landingStartIndex)
        {
            TakeoffEndIndex = takeoffEndIndex;
            LandingStartIndex = landingStartIndex;
        }
    }
}
