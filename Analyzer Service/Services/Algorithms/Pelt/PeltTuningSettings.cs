namespace Analyzer_Service.Services.Algorithms.Pelt
{
    public class PeltTuningSettings
    {
        public int SAMPLING_JUMP { get; set; }
        public double PENALTY_BETA { get; set; }
        public double MINIMUM_SEGMENT_DURATION_SECONDS { get; set; }

        public double MINIMUM_DURATION_SECONDS { get; set; }
        public double MINIMUM_RANGEZ { get; set; }
        public int PATTERN_SUPPORT_THRESHOLD { get; set; }

        public double FINAL_SCORE { get; set; }
        public double HASH_SIMILARITY { get; set; }
        public double FEATURE_SIMILARITY { get; set; }
        public double DURATION_SIMILARITY { get; set; }

        public double HASH_THRESHOLD { get; set; }

        public int RARE_LABEL_COUNT_MAX { get; set; }
        public double RARE_LABEL_TIME_FRACTION { get; set; }
        public int POST_MINIMUM_GAP_SECONDS { get; set; }
    }
}
