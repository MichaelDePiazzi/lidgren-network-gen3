namespace Lidgren.Network
{
    public interface INetCompression
    {
        /// <summary>
        /// Tries to compress an outgoing message. Returns true if it compressed it.
        /// </summary>
        bool TryCompress(NetOutgoingMessage msg);

        /// <summary>
        /// Tries to decompress an incoming message. Returns true if it decompressed it.
        /// </summary>
        bool TryDecompress(NetIncomingMessage msg);
    }
}