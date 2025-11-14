namespace Analyzer_Service.Models.Constant
{
    public class ConstantPelt
    {
        public const int HAMPEL_WINDOWSIZE = 10;
        public const double HAMPEL_SIGMA_THRESHOLD = 2.5;
        public const double PENALTY_BETA = 2.0;
        public const double MINIMUM_SEGMENT_DURATION_SECONDS = 5.0;
        public const int SAMPLING_JUMP = 30;

        public const double ZeroTolerance = 1e-12;
        public const double DefaultSigmaValue = 1.0;
        public const double SigmaDivisionFactor = 2.0;
        public const int MinimumSegmentLength = 2;
    }
}
