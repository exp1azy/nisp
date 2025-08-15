using MemoryPack;

namespace Nisp.Test.Shared
{
    [MemoryPackable]
    public partial class UserMessage
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public byte[] MessageBytes { get; set; }
        public DateTime Time { get; set; }
        public int RandomNumber { get; set; }
    }
}
