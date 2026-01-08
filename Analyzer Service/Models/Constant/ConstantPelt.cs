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

    }
}
