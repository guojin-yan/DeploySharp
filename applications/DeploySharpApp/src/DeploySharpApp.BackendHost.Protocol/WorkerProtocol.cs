using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DeploySharpApp.Contracts;

namespace DeploySharpApp.BackendHost.Protocol
{
    public static class WorkerProtocol
    {
        public const int ProtocolVersion = 1;

        public static string SerializeRequest(WorkerRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return JsonSerializer.Serialize(request);
        }

        public static WorkerRequest DeserializeRequest(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) throw new FormatException("Worker request line is empty.");
            try { return JsonSerializer.Deserialize<WorkerRequest>(line) ?? throw new FormatException("Worker request is invalid."); }
            catch (JsonException exception) { throw new FormatException("Worker request JSON is invalid.", exception); }
        }

        public static string SerializeResponse(WorkerResponse response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            return JsonSerializer.Serialize(response);
        }

        public static WorkerResponse DeserializeResponse(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) throw new FormatException("Worker response line is empty.");
            try { return JsonSerializer.Deserialize<WorkerResponse>(line) ?? throw new FormatException("Worker response is invalid."); }
            catch (JsonException exception) { throw new FormatException("Worker response JSON is invalid.", exception); }
        }

        public static async Task WriteRequestAsync(Stream stream, WorkerRequest request, CancellationToken cancellationToken = default)
        {
            await WriteLineAsync(stream, SerializeRequest(request), cancellationToken).ConfigureAwait(false);
        }

        public static async Task<WorkerRequest?> ReadRequestAsync(StreamReader reader, CancellationToken cancellationToken = default)
        {
            var line = await ReadLineAsync(reader, cancellationToken).ConfigureAwait(false);
            return line == null ? null : DeserializeRequest(line);
        }

        public static async Task WriteResponseAsync(Stream stream, WorkerResponse response, CancellationToken cancellationToken = default)
        {
            await WriteLineAsync(stream, SerializeResponse(response), cancellationToken).ConfigureAwait(false);
        }

        public static async Task<WorkerResponse?> ReadResponseAsync(StreamReader reader, CancellationToken cancellationToken = default)
        {
            var line = await ReadLineAsync(reader, cancellationToken).ConfigureAwait(false);
            return line == null ? null : DeserializeResponse(line);
        }

        private static async Task WriteLineAsync(Stream stream, string value, CancellationToken cancellationToken)
        {
            var bytes = Encoding.UTF8.GetBytes(value + "\n");
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<string?> ReadLineAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await reader.ReadLineAsync().ConfigureAwait(false);
        }
    }
}
