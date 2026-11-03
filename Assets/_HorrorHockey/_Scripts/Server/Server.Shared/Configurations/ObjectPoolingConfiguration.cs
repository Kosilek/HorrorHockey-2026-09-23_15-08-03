namespace Server.Shared.Configurations
{
    public class ObjectPoolingConfiguration
    {
        public int StartPoolSize { get; set; }
        public int MaxPoolSize { get; set; }
        public float PercentageToAdd { get; set; }
        public float PercentageToReduce { get; set; }
        public float PercentageAdd { get; set; }
        public float PercentageReduction { get; set; }
        public int DurationDelete { get; set; }
    }
}
