using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sync
{
    public static class StreamSplitting
    {
        public static void ReadStreamsFromStream(Stream stream, Stream[] streams, byte specialByte = 0)
        {
            if (streams.Length > 256 - 1) throw new NotImplementedException();

            new Thread(() =>
            {
                byte targetStream = 0;

                int ch;
                while ((ch = stream.ReadByte()) != -1)
                {
                    if (ch == 0)
                    {
                        int action = streams[targetStream].ReadByte();
                        if (action == 0) //Write 0
                        {
                            streams[targetStream].WriteByte((byte)ch);
                        }
                        else
                        {
                            targetStream = (byte)(action - 1);
                        }
                    }
                    else
                    {
                        streams[targetStream].WriteByte((byte)ch);
                    }
                }
            }).Start();
        }
        public static void WriteStreamsToStream(Stream stream, Stream[] streams, byte specialByte = 0)
        {
            if (streams.Length > 256 - 1) throw new NotImplementedException();

            object streamLock = new object();

            var threads = streams.Select(readStream =>
            {
                return new Thread(() =>
                {
                    byte[] buffer = new byte[2 << 12];
                    while (true)
                    {
                        int read = readStream.Read(buffer, 0, buffer.Length);
                        if (read == 0) continue;
                        lock (streamLock)
                        {
                            stream.Write(buffer, 0, read);
                        }
                    }
                });
            }).ToList();

            threads.ForEach(x => x.Start());
        }
    }

    public class BetterStream : Stream
    {
        private SemaphoreSlim hasData = new SemaphoreSlim(0, 1);
        private SemaphoreSlim hasNoData = new SemaphoreSlim(0, 1);
        private int writePosition = 0;
        private int readPosition = 0;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        private long _length = 0;
        public override long Length => _length;

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        private long bufferFilled = 0;
        private byte[] buffer = new byte[2 << 16];

        static int nextDebugId = 0;
        int debugId = nextDebugId++;

        public override void Write(byte[] buffer, int intOffset, int intCount)
        {
            if (intOffset != 0) throw new NotImplementedException();
            if (intCount == 0) return;

            long count = intCount;
            long offset = intOffset;

            while (count > 0)
            {
                lock (buffer)
                {
                    if (hasNoData.CurrentCount == 1)
                    {
                        hasNoData.Wait();
                    }

                    long countToWrite = Math.Min(count, buffer.Length - bufferFilled);
                    Array.Copy(buffer, offset, this.buffer, bufferFilled, countToWrite);
                    bufferFilled += countToWrite;
                    count -= countToWrite;
                    offset += countToWrite;
                }
                if (hasData.CurrentCount == 0)
                {
                    hasData.Release();
                }
                // This is probaly unsafe.. but whatever...
                if (count >= 0 && bufferFilled == this.buffer.Length)
                {
                    hasNoData.Wait();
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset != 0) throw new NotImplementedException();
            if (count == 0) return 0;

            if (bufferFilled == 0)
            {
                hasData.Wait();
            }

            long countToRead;
            lock (buffer)
            {
                countToRead = Math.Min(count, bufferFilled);
                Array.Copy(this.buffer, 0, buffer, 0, countToRead);
                // Move bytes down
                Array.Copy(this.buffer, countToRead, this.buffer, 0, bufferFilled - countToRead);
                bufferFilled -= countToRead;
                if(bufferFilled == 0)
                {
                    if (hasNoData.CurrentCount == 0)
                    {
                        hasNoData.Release();
                    }
                }
            }
            return (int)countToRead;
        }

        public override long Seek(long offset, SeekOrigin loc)
        {
            throw new Exception("We support Read and Write seeking... so this function is now ambigious.");
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }
    }
}
