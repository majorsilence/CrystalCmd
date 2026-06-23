using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading.Tasks;
using Majorsilence.CrystalCmd.Common;
using System.Collections.Specialized;

namespace Majorsilence.CrystalCmd.Server
{
    internal class BaseRoute
    {
        // Upper bound on the number of bytes we will read out of a client-supplied GZip
        // stream. Without this a small compressed payload can decompress to gigabytes
        // (a "zip bomb") and exhaust server memory. Override with Limits:MaxDecompressedBytes.
        private const long DefaultMaxDecompressedBytes = 209_715_200L; // 200 MB

        protected readonly ILogger _logger;
        public BaseRoute(ILogger logger)
        {
            _logger = logger;
        }

        public static NameValueCollection HeadersFromAsp(Microsoft.AspNetCore.Http.IHeaderDictionary headers)
        {
            var nvc = new NameValueCollection();
            if (headers == null) return nvc;
            foreach (var kv in headers)
            {
                // StringValues.ToString() returns a comma-separated string without allocating an array
                nvc.Add(kv.Key, kv.Value.ToString());
            }
            return nvc;
        }

        public async Task<(Data ReportData, byte[] ReportTemplate, string Id)> ReadInput(Stream inputStream, string contentType,
          NameValueCollection headers,
          bool templateOnly = false)
        {
            if (inputStream == null) throw new CrystalCmdException("input stream is null");

            Data reportData = null;
            byte[] reportTemplate = null;

            if (string.IsNullOrWhiteSpace(contentType))
            {
                throw new CrystalCmdException("content type is null");
            }

            // avoid allocation from ToLower()
            bool contentIndicatesGzip = contentType.IndexOf("gzip", StringComparison.OrdinalIgnoreCase) >= 0
                                        || string.Equals(headers?["Content-Encoding"] ?? "", "gzip", StringComparison.OrdinalIgnoreCase);

            if (contentIndicatesGzip)
            {
                var result = await CompressedStreamInput(inputStream).ConfigureAwait(false);
                reportData = result.ReportData;
                reportTemplate = result.Template;
            }
            else
            {
                using var streamContent = new StreamContent(inputStream);
                streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

                if (!streamContent.IsMimeMultipartContent())
                    throw new InvalidOperationException("Unsupported media type");

                var provider = await streamContent.ReadAsMultipartAsync().ConfigureAwait(false);
                foreach (var file in provider.Contents)
                {
                    string name = file.Headers.ContentDisposition?.Name?.Replace("\"", "") ?? "";
                    if (string.Equals(name, "reportdata", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var json = await file.ReadAsStringAsync().ConfigureAwait(false);
                        reportData = JsonConvert.DeserializeObject<CrystalCmd.Common.Data>(json);
                    }
                    else
                    {
                        reportTemplate = await file.ReadAsByteArrayAsync().ConfigureAwait(false);
                    }
                }
            }

            if (!templateOnly && reportData == null)
            {
                throw new CrystalCmdException("report data is null");
            }

            if (reportTemplate == null)
            {
                throw new CrystalCmdException("report template is null");
            }

            string id = Guid.NewGuid().ToString();
            return (reportData, reportTemplate, id);
        }

        private static async Task<(Data ReportData, byte[] Template)> CompressedStreamInput(Stream inputStream)
        {
            if (inputStream == null)
            {
                throw new CrystalCmdException("CompressedStreamInput inputStream is null");
            }

            // Decompress and deserialize directly from stream to avoid intermediate MemoryStream and extra allocations.
            await using var decompressed = new GZipStream(inputStream, CompressionMode.Decompress);
            // Guard against decompression bombs: cap the number of decompressed bytes read.
            using var limited = new MaxLengthReadStream(decompressed, DefaultMaxDecompressedBytes);
            using var sr = new StreamReader(limited);
            await using var jsonReader = new JsonTextReader(sr);
            var serializer = new JsonSerializer();
            var dto = serializer.Deserialize<StreamedRequest>(jsonReader);
            if (dto == null)
            {
                throw new CrystalCmdException("CompressedStreamInput dto is null");
            }
            return (dto.ReportData, dto.Template);
        }
    }

    /// <summary>
    /// Read-only stream wrapper that throws once more than <c>maxLength</c> bytes have been
    /// read from the inner stream. Used to bound decompressed output and prevent
    /// decompression-bomb memory exhaustion.
    /// </summary>
    internal sealed class MaxLengthReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _maxLength;
        private long _totalRead;

        public MaxLengthReadStream(Stream inner, long maxLength)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _maxLength = maxLength;
        }

        private void Account(int read)
        {
            _totalRead += read;
            if (_totalRead > _maxLength)
            {
                throw new CrystalCmdException(
                    $"Decompressed request exceeded the maximum allowed size of {_maxLength} bytes.");
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _inner.Read(buffer, offset, count);
            Account(read);
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            int read = await _inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            Account(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, System.Threading.CancellationToken cancellationToken = default)
        {
            int read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            Account(read);
            return read;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _totalRead; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
