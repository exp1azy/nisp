using EasyCompressor;

namespace Nisp.Core.Compressors
{
    /// <summary>
    /// Factory class for creating compression algorithm instances used in NISP communication.
    /// </summary>
    public class CompressorFactory : ICompressorFactory
    {
        // <inheritdoc/>
        public ICompressor UseZstdSharp() => new ZstdSharpCompressor();

        // <inheritdoc/>
        public ICompressor UseLZ4() => new LZ4Compressor();

        // <inheritdoc/>
        public ICompressor UseSnappier() => new SnappierCompressor();
    }
}
