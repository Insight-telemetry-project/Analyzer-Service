namespace Analyzer_Service.Models.Dto
{
    public class CausalityRelation
    {
        public string CauseParameter { get; }
        public string EffectParameter { get; }

        public CausalityRelation(string causeParameter, string effectParameter)
        {
            CauseParameter = causeParameter;
            EffectParameter = effectParameter;
        }
    }
}
