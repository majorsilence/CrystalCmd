﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Majorsilence.CrystalCmd.Client
{
    class CompressedContent : HttpContent
    {
        // https://raw.githubusercontent.com/WebApiContrib/WebAPIContrib/master/src/WebApiContrib/Content/CompressedContent.cs
        // license https://github.com/WebApiContrib/WebAPIContrib/blob/master/LICENSE.txt
        private readonly HttpContent originalContent;
        private readonly string encodingType;

        public CompressedContent(HttpContent content, string encodingType)
        {
            if (content == null)
            {
                throw new ArgumentNullException("content");
            }

            if (encodingType == null)
            {
                throw new ArgumentNullException("encodingType");
            }

            originalContent = content;
            this.encodingType = encodingType.ToLowerInvariant();

            if (this.encodingType != "gzip" && this.encodingType != "deflate")
            {
                throw new InvalidOperationException(string.Format("Encoding '{0}' is not supported. Only supports gzip or deflate encoding.", this.encodingType));
            }

            foreach (KeyValuePair<string, IEnumerable<string>> header in originalContent.Headers)
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            Headers.ContentEncoding.Add(encodingType);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;

            return false;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            Stream compressedStream = null;

            if (encodingType == "gzip")
            {
                compressedStream = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true);
            }
            else if (encodingType == "deflate")
            {
                compressedStream = new DeflateStream(stream, CompressionMode.Compress, leaveOpen: true);
            }

            return originalContent.CopyToAsync(compressedStream).ContinueWith(tsk =>
            {
                if (compressedStream != null)
                {
                    compressedStream.Dispose();
                }
            });
        }
        public static Stream Compress(string content)
        {
            byte[] jsonBytes = Encoding.UTF8.GetBytes(content);
            var memoryStream = new MemoryStream();
            var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress);
            gzipStream.Write(jsonBytes, 0, jsonBytes.Length);
            gzipStream.Flush(); // Ensure all data is written to the stream
            memoryStream.Seek(0, SeekOrigin.Begin); // Seek on the memoryStream instead of gzipStream
            return memoryStream;
        }

    }
}
