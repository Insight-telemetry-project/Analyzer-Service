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
        public const double LandingStableMinSeconds = 600.0;

        public const double SpikeThresholdMedianMultiplier = 12.0;
        public const double MaxAllowedSpikeFraction = 0.03;
        public const double MaxAllowedTailRatio = 25.0;
        public const double P95Quantile = 0.95;

        public const double TakeoffMustBeBeforePercent = 0.60;
        public const double LandingMustBeAfterPercent = 0.70;

        public const double StableAbsSlopeMultiplier = 1.2;
        public const double ClimbSlopeMultiplier = 2.5;
        public const double DescentSlopeMultiplier = 2.5;

        public const double TakeoffStableMinSeconds = 120.0;
        public const double LandingDropStd = 1.0;

        public const double TakeoffCruiseStdTolerance = 1.0;
        public const double TakeoffRiseFraction = 0.75;

        public const int FirstSegmentIndex = 0;
    }
}
