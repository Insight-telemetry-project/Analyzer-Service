namespace Analyzer_Service.Models.Constant
{
    public static class ConstantPelt
    {
        public static int HAMPEL_WINDOWSIZE = 10;
        public static double HAMPEL_SIGMA_THRESHOLD = 2.5;
        public static double PENALTY_BETA = 2.0;
        public static double MINIMUM_SEGMENT_DURATION_SECONDS = 5.0;
        public static int SAMPLING_JUMP = 30;

        public const double ZEROTO_LERANCE = 1e-12;
        public const double DEFAULT_SIGMA_VALUE = 1.0;
        public const double SIGMA_DIVISION_FACTOR = 2.0;
        public const int MINIMUM_SEGMENT_LENGTH = 2;

        public const double TAKE_OF_AREA = 0.25;
        public const double LANDING_AREA = 0.75;
        public const int FIRST_SEGMENT = 0;
        public const double LANDING_STABLE_MIN_SECONDS = 600.0;

        public const double SPIKE_THRESHOLD_MEDIAN_MULTIPLIER = 12.0;
        public const double MAX_ALLOWED_SPIKE_FRACTION = 0.03;
        public const double MAX_ALLOWED_TAIL_RATIO = 25.0;
        public const double P95Quantile = 0.95;

        public const double Takeoff_Must_Be_Before_Percent = 0.60;
        public const double LANDING_MUST_BE_AFTER_PERCENT = 0.70;

        public const double STABLE_ABS_SLOPE_MULTIPLIER = 1.2;
        public const double CLIMB_SLOPE_MULTIPLIER = 2.5;
        public const double DESCENT_SLOPE_MULTIPLIER = 2.5;

        public const double TAKEOFF_STABLE_MIN_SECONDS = 120.0;
        public const double LANDING_DROP_STD = 1.0;

        public const double TAKEOFF_CRUISE_STD_TO_LERANCE = 1.0;
        public const double TAKEOFF_RISE_FRACTION = 0.75;

        public const int FIRST_SEGMENTINDEX = 0;

        public const double MULTIPLIER_THRESHOLD = 2.0;
    }
}
