namespace Nisp.Core.Entities
{
    public class CoherenceParams
    {
        public CoherenceType Type { get; set; }
        public Endpoint Endpoint { get; set; }
        public int Delay { get; set; }
        public int? SendTimeout { get; set; }
    }
}
