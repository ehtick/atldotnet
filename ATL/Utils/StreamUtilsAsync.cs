﻿using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ATL
{
    /// <summary>
    /// Misc. utilities used by binary readers
    /// </summary>
    internal static class StreamUtilsAsync
    {
        /// <summary>
        /// Copies a given number of bytes from a given stream to another, starting at current stream positions
        /// i.e. first byte will be read at from.Position and written at to.Position
        /// NB : This method cannot be used to move data within one single stream; use CopySameStream instead
        /// </summary>
        /// <param name="from">Stream to start copy from</param>
        /// <param name="to">Stream to copy to</param>
        /// <param name="length">Number of bytes to copy (optional; default = 0 = all bytes until the end of the 'from' stream)</param>
        public static async Task CopyStreamAsync(Stream from, Stream to, int length = 0)
        {
            byte[] data = new byte[Settings.FileBufferSize];
            int bytesToRead;
            int totalBytesRead = 0;

            while (true)
            {
                if (length > 0)
                {
                    if (totalBytesRead + Settings.FileBufferSize < length) bytesToRead = Settings.FileBufferSize; else bytesToRead = length - totalBytesRead;
                }
                else // Read everything we can
                {
                    bytesToRead = Settings.FileBufferSize;
                }
                int bytesRead = await from.ReadAsync(data, 0, bytesToRead);
                if (bytesRead == 0)
                {
                    break;
                }
                await to.WriteAsync(data, 0, bytesRead);
                totalBytesRead += bytesRead;
            }
        }

        /// <summary>
        /// Copy data between the two given offsets within the given stream
        /// </summary>
        /// <param name="s">Stream to process</param>
        /// <param name="offsetFrom">Starting offset to copy data from</param>
        /// <param name="offsetTo">Starting offset to copy data to</param>
        /// <param name="length">Length of the data to copy</param>
        public static async Task CopySameStreamAsync(Stream s, long offsetFrom, long offsetTo, int length, IProgress<float> progress = null)
        {
            await CopySameStreamAsync(s, offsetFrom, offsetTo, length, Settings.FileBufferSize, progress);
        }

        /// <summary>
        /// Copy data between the two given offsets within the given stream, using the given buffer size
        /// </summary>
        /// <param name="s">Stream to process</param>
        /// <param name="offsetFrom">Starting offset to copy data from</param>
        /// <param name="offsetTo">Starting offset to copy data to</param>
        /// <param name="length">Length of the data to copy</param>
        /// <param name="bufferSize">Buffer size to use during the operation</param>
        public static async Task CopySameStreamAsync(Stream s, long offsetFrom, long offsetTo, int length, int bufferSize, IProgress<float> progress = null)
        {
            if (offsetFrom == offsetTo) return;

            byte[] data = new byte[bufferSize];
            int bufSize;
            int written = 0;
            bool forward = offsetTo > offsetFrom;
            int nbIterations = (int)Math.Ceiling((float)length / bufferSize);
            int resolution = (int)Math.Ceiling(nbIterations / 10f);
            float iteration = 0;

            while (written < length)
            {
                bufSize = Math.Min(bufferSize, length - written);
                if (forward)
                {
                    s.Seek(offsetFrom + length - written - bufSize, SeekOrigin.Begin);
                    await s.ReadAsync(data, 0, bufSize);
                    s.Seek(offsetTo + length - written - bufSize, SeekOrigin.Begin);
                }
                else
                {
                    s.Seek(offsetFrom + written, SeekOrigin.Begin);
                    await s.ReadAsync(data, 0, bufSize);
                    s.Seek(offsetTo + written, SeekOrigin.Begin);
                }
                await s.WriteAsync(data, 0, bufSize);
                written += bufSize;

                if (progress != null)
                {
                    iteration++;
                    if (0 == iteration % resolution) progress.Report(iteration / nbIterations);
                }
            }
        }

        /// <summary>
        /// Remove a portion of bytes within the given stream
        /// </summary>
        /// <param name="s">Stream to process; must be accessible for reading and writing</param>
        /// <param name="endOffset">End offset of the portion of bytes to remove</param>
        /// <param name="delta">Number of bytes to remove</param>
        public static async Task ShortenStreamAsync(Stream s, long endOffset, uint delta, IProgress<float> progress = null)
        {
            await CopySameStreamAsync(s, endOffset, endOffset - delta, (int)(s.Length - endOffset), progress);

            s.SetLength(s.Length - delta);
        }

        /// <summary>
        /// Add bytes within the given stream
        /// </summary>
        /// <param name="s">Stream to process; must be accessible for reading and writing</param>
        /// <param name="oldIndex">Offset where to add new bytes</param>
        /// <param name="delta">Number of bytes to add</param>
        /// <param name="fillZeroes">If true, new bytes will all be zeroes (optional; default = false)</param>
        public static async Task LengthenStreamAsync(Stream s, long oldIndex, uint delta, bool fillZeroes = false, IProgress<float> progress = null)
        {
            long newIndex = oldIndex + delta;
            await CopySameStreamAsync(s, oldIndex, oldIndex + delta, (int)(s.Length - oldIndex), progress);

            if (fillZeroes)
            {
                // Fill the location of old copied data with zeroes
                s.Seek(oldIndex, SeekOrigin.Begin);
                for (long i = oldIndex; i < newIndex; i++) s.WriteByte(0);
            }
        }
    }
}