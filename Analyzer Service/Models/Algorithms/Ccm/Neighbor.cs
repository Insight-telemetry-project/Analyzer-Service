namespace Analyzer_Service.Models.Algorithms.Ccm
{
    public class Neighbor
    {
        public int Index { get; set; }
        public double Distance { get; set; }

        public Neighbor(int index, double distance)
        {
            Index = index;
            Distance = distance;
        }
    }
}
