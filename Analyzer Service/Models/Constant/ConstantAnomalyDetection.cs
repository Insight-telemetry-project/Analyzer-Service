namespace Analyzer_Service.Models.Constant
{
    public static class ConstantAnomalyDetection
    {
        //public static double MINIMUM_DURATION_SECONDS = 0.6;
        //public static double MINIMUM_RANGEZ = 0.5;
        //public static int PATTERN_SUPPORT_THRESHOLD = 2;

        //public static double FINAL_SCORE = 0.75;
        //public static double HASH_SIMILARITY = 0.75;
        //public static double FEATURE_SIMILARITY = 0.2;
        //public static double DURATION_SIMILARITY = 0.05;

        //public static double HASH_THRESHOLD = 0.001;

        //public static double RARE_LABEL_COUNT_MAX = 3.0;
        //public static double RARE_LABEL_TIME_FRACTION = 0.05;
        //public static double POST_MINIMUM_GAP_SECONDS = 8.0;

        public const double INITIAL_MIN_TIME = -1e18;
        public const int SHAPE_LENGTH = 32;
        public const int ROUND_DECIMALS = 1;
        public const char HASH_SPLIT = ',';

        public const int MAX_ANOMALIES_PER_FLIGHT = 20;
        public const double MIN_SIGNIFICANT_RANGE_Z = 1.5;

        public const double RANGEZ_SCORE_THRESHOLD = 0.7;
        public const double ENERGYZ_SCORE_THRESHOLD = 0.3;

        public const double DURATION_HASH_THRESHOLD = 0.0;
        public const double DURATION_HASH_MIN = 1.0;


    }
}
