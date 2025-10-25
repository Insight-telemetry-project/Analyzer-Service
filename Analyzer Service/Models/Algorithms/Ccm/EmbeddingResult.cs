namespace Analyzer_Service.Models.Algorithms.Ccm
{
    public class EmbeddingResult
    {
        public List<double[]> Vectors { get; }
        public List<int> AnchorIndices { get; }

        public EmbeddingResult(List<double[]> vectors, List<int> anchorIndices)
        {
            Vectors = vectors;
            AnchorIndices = anchorIndices;
        }
    }
}
