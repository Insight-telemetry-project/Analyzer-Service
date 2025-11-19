namespace Analyzer_Service.Models.Constant
{
    public class ConstantAnomalyDetection
    {
        public const double MINIMUM_DURATION_SECONDS = 0.6;
        public const double MINIMUM_RANGEZ = 0.5;
        public const int PATTERN_SUPPORT_THRESHOLD = 2;
        public const double RARE_LABEL_COUNT_MAX = 3.0;
        public const double RARE_LABEL_TIME_FRACTION = 0.05;
        public const double POST_MINIMUM_GAP_SECONDS = 8.0;
        public const double INITIAL_MIN_TIME = -1e18;

    }
}
