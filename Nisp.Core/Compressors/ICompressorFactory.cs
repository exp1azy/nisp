using EasyCompressor;

namespace Nisp.Core.Compressors
{
    /// <summary>
    /// Factory interface for creating compression algorithm instances used in NISP communication.
    /// </summary>
    public interface ICompressorFactory
    {
        /// <summary>
        /// Creates a Zstandard (Zstd) compressor using the ZstdSharp library.
        /// </summary>
        /// <returns>An <see cref="ICompressor"/> instance configured for Zstandard compression.</returns>
        public ICompressor UseZstdSharp();

        /// <summary>
        /// Creates an LZ4 compressor optimized for maximum compression and decompression speed.
        /// </summary>
        /// <returns>An <see cref="ICompressor"/> instance configured for LZ4 compression.</returns>
        public ICompressor UseLZ4();

        /// <summary>
        /// Creates a Snappy (Snappier) compressor.
        /// </summary>
        /// <returns>An <see cref="ICompressor"/> instance configured for Snappy compression.</returns>
        public ICompressor UseSnappier();
    }
}