using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace JYPPX.DeploySharp.ModelFactory
{
    /// <summary>Loads a bounded catalog snapshot over HTTPS without following redirects. / 通过 HTTPS 加载有界目录快照且不跟随重定向。</summary>
    public static class ModelCatalogClient
    {
        /// <summary>Downloads and validates a remote catalog using an application-owned HttpClient. / 使用应用所有的 HttpClient 下载并验证远程目录。</summary>
        public static async Task<ValidatedModelCatalog> LoadAsync(Uri catalogUri, HttpClient httpClient, ModelCatalogValidationOptions? validationOptions = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (catalogUri == null) throw new ArgumentNullException(nameof(catalogUri));
            if (httpClient == null) throw new ArgumentNullException(nameof(httpClient));
            if (!catalogUri.IsAbsoluteUri || catalogUri.Scheme != Uri.UriSchemeHttps) throw new ArgumentException("Catalog URI must be absolute HTTPS.", nameof(catalogUri));
            using (var timeoutSource = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30)))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token))
            using (var request = new HttpRequestMessage(HttpMethod.Get, catalogUri))
            {
                request.Headers.TryAddWithoutValidation("User-Agent", "DeploySharp-ModelFactory/2.0");
                try
                {
                    using (HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false))
                    {
                        Uri? finalUri = response.RequestMessage?.RequestUri;
                        if (finalUri == null || finalUri != catalogUri || (int)response.StatusCode >= 300 && (int)response.StatusCode < 400) ThrowHttp("Catalog redirects are not allowed.", catalogUri, response.StatusCode);
                        if (!response.IsSuccessStatusCode) ThrowHttp("Catalog HTTP request failed.", catalogUri, response.StatusCode);
                        Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                        return await ModelCatalogJsonSerializer.DeserializeAsync(stream, validationOptions, linked.Token).ConfigureAwait(false);
                    }
                }
                catch (ModelFactoryException) { throw; }
                catch (OperationCanceledException exception) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    throw new ModelFactoryException("Catalog request timed out.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.Timeout, "Catalog request timed out.", uri: catalogUri) }, exception, exception.ToString());
                }
                catch (OperationCanceledException exception)
                {
                    throw new ModelFactoryException("Catalog request was cancelled.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.Cancelled, "Catalog request was cancelled.", uri: catalogUri) }, exception, exception.ToString());
                }
                catch (HttpRequestException exception)
                {
                    throw new ModelFactoryException("Catalog HTTP request failed.", new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.HttpFailure, exception.Message, uri: catalogUri) }, exception, exception.ToString());
                }
            }
        }

        private static void ThrowHttp(string message, Uri uri, HttpStatusCode status)
        {
            throw new ModelFactoryException(message, new[] { new ModelFactoryDiagnostic(ModelFactoryDiagnosticCodes.HttpFailure, message, uri: uri, statusCode: status) }, technicalDetails: status.ToString());
        }
    }
}
