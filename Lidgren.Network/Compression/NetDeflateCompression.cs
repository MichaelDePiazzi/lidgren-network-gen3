using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace Lidgren.Network
{
    public class NetDeflateCompression : INetCompression
    {
        private const int HeaderLength = 3;
        private const int LengthBits = (HeaderLength * 8) - 1;
        private const int MaxLengthInBits = (1 << LengthBits) - 1;

        private readonly NetPeer _peer;

        public NetDeflateCompression(NetPeer peer)
        {
            _peer = peer;
        }

        public bool TryCompress(NetOutgoingMessage msg)
        {
            if (msg.LengthBytes <= (HeaderLength + 1))
                return false;
            if (msg.LengthBits > MaxLengthInBits)
                return false;

            var data = _peer.GetStorage(msg.LengthBytes - 1);
            int compressedLength;
            try
            {
                using (var compressedStream = new MemoryStream(data, HeaderLength, data.Length - HeaderLength))
                {
                    compressedStream.SetLength(0);
                    using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Compress, leaveOpen: true))
                    {
                        deflateStream.Write(msg.Data, 0, msg.LengthBytes);
                    }
                    compressedLength = (int)compressedStream.Length;
                }
            }
            catch (NotSupportedException)
            {
                // Most likely the MemoryStream needed to expand the buffer
                //Debug.WriteLine("NetCompression: " + msg.LengthBytes + " bytes could not be compressed into " + data.Length);
                _peer.Recycle(data);
                return false;
            }

            //Debug.WriteLine("NetCompression: " + msg.LengthBytes + " -> " + compressedLength + " bytes" +
            //    " (" + (100.0 - (100.0 * compressedLength / msg.LengthBytes)).ToString("0.0") + "% reduction)");

            var finalLength = compressedLength + HeaderLength;
            if (finalLength >= msg.LengthBytes)
            {
                _peer.Recycle(data);
                return false;
            }

            var originalData = msg.Data;
            var uncompressedLengthInBits = (uint)msg.LengthBits;

            msg.Data = data;
            msg.LengthBits = 0; // reset write pointer
            msg.Write(true); // indicates the message is compressed
            msg.Write(uncompressedLengthInBits, LengthBits);
            msg.LengthBytes = finalLength; // include compressed data

            _peer.Recycle(originalData);

            return true;
        }

        public bool TryDecompress(NetIncomingMessage msg)
        {
            var isCompressed = msg.ReadBoolean();
            if (!isCompressed)
                return false;

            var uncompressedLengthInBits = (int)msg.ReadUInt32(LengthBits);
            var uncompressedLength = NetUtility.BytesToHoldBits(uncompressedLengthInBits);

            var data = _peer.GetStorage(uncompressedLength);
            using (var compressedStream = new MemoryStream(msg.Data, HeaderLength, msg.LengthBytes - HeaderLength))
            {
                using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                {
                    deflateStream.Read(data, 0, uncompressedLength);
                }
            }

            var originalData = msg.Data;
            
            msg.Data = data;
            msg.LengthBits = uncompressedLengthInBits;
            msg.Position = 1; // skip the reserved "isCompressed" bit

            _peer.Recycle(originalData);

            return true;
        }
    }
}