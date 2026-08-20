using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using InfluxData.Net.Common.Infrastructure;
using InfluxData.Net.Common.RequestClients;
using InfluxData.Net.InfluxDb.Constants;
using InfluxData.Net.InfluxDb.Models;
using InfluxData.Net.InfluxDb.RequestClients;

namespace InfluxData.Net.InfluxDb.ClientModules
{
    public class DeleteClientModule : IDeleteClientModule
    {
        private static readonly HashSet<string> SupportedPrecisions =
            new HashSet<string>(StringComparer.Ordinal) { "ns", "us", "ms", "s" };

        private readonly IInfluxDbRequestClient _requestClient;
        private readonly RequestClientBase _headerAwareRequestClient;

        public DeleteClientModule(IInfluxDbRequestClient requestClient)
        {
            _requestClient = requestClient ?? throw new ArgumentNullException(nameof(requestClient));
            _headerAwareRequestClient = requestClient as RequestClientBase
                ?? throw new ArgumentException(
                    "The delete API requires a request client derived from RequestClientBase.",
                    nameof(requestClient));
        }

        public virtual async Task<IInfluxDataApiResponse> DeleteAsync(
            DeleteRequest request,
            string authorizationToken = null)
        {
            ValidateRequest(request);

            var requestParams = new Dictionary<string, string>
            {
                [QueryParams.Bucket] = Uri.EscapeDataString(
                    String.Format(CultureInfo.InvariantCulture, "{0}/{1}", request.Database, request.RetentionPolicy))
            };

            if (!String.IsNullOrEmpty(request.Precision))
            {
                requestParams[QueryParams.Precision] = request.Precision;
            }

            var headers = BuildAuthorizationHeaders(authorizationToken);
            var body = new
            {
                start = request.Start.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                stop = request.Stop.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                predicate = request.Predicate
            };
            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var content = new StringContent(JsonSerializer.Serialize(body, jsonOptions), Encoding.UTF8, "application/json");

            var response = await _headerAwareRequestClient.RequestWithHeadersAsync(
                HttpMethod.Post,
                RequestPaths.Delete,
                requestParams,
                content,
                false,
                false,
                headers).ConfigureAwait(false);

            return new InfluxDataApiDeleteResponse(response.StatusCode, response.Body);
        }

        private IDictionary<string, string> BuildAuthorizationHeaders(string authorizationToken)
        {
            var token = authorizationToken;

            if (String.IsNullOrEmpty(token))
            {
                var username = _requestClient.Configuration.Username;
                var password = _requestClient.Configuration.Password;

                if (String.IsNullOrEmpty(username) && String.IsNullOrEmpty(password))
                {
                    return null;
                }

                token = String.Format(CultureInfo.InvariantCulture, "{0}:{1}", username, password);
            }

            return new Dictionary<string, string> { ["Authorization"] = "Token " + token };
        }

        private static void ValidateRequest(DeleteRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (String.IsNullOrWhiteSpace(request.Database))
                throw new ArgumentException("Database may not be null or empty.", nameof(request));
            if (String.IsNullOrWhiteSpace(request.RetentionPolicy))
                throw new ArgumentException("Retention policy may not be null or empty.", nameof(request));
            if (request.Start >= request.Stop)
                throw new ArgumentException("Start must be earlier than stop.", nameof(request));
            if (!String.IsNullOrEmpty(request.Precision) && !SupportedPrecisions.Contains(request.Precision))
                throw new ArgumentException("Precision must be one of: ns, us, ms, s.", nameof(request));
        }
    }
}
