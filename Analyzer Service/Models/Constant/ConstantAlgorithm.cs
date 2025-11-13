namespace Analyzer_Service.Models.Constant
{
    public class ConstantAlgorithm
    {
        public const double PEARSON_LINEAR_THRESHOLD = 0.65;
        public const double PEARSON_STRONG_THRESHOLD = 0.85;

        public const double GRANGER_CAUSALITY_THRESHOLD = 0.015;

        public const double CCM_CAUSALITY_THRESHOLD = 0.60;

        public const int CCM_EMBEDDING_DIM = 4;
        public const int CCM_DELAY = 1;

        public const int LAG_DIVISOR = 5000;
        public const int MIN_LAG = 2;

        public const double Epsilon = 1e-12;
        public const double MadToStdScale = 1.4826;

    }
}
