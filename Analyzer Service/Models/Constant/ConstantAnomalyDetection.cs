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

        public const int SHAPE_LENGTH = 32;
        public const int ROUND_DECIMALS = 1;


        public const double FINAL_SCORE = 0.75;
        public const double HASH_SIMILARITY = 0.75;
        public const double FEATURE_SIMILARITY = 0.2;
        public const double DURATION_SIMILARITY = 0.05;

        public const double HASH_THRESHOLD = 0.001;

        public const char HASH_SPLIT = ',';



    }
}
