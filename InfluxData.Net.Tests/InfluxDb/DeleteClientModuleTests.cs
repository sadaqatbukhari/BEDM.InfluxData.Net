using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using InfluxData.Net.InfluxDb.Models;
using Xunit;

namespace InfluxData.Net.Tests.InfluxDb
{
    public class DeleteClientModuleTests
    {
        [Theory]
        [InlineData(InfluxDbVersion.Latest)]
        [InlineData(InfluxDbVersion.v_1_8)]
        [InlineData(InfluxDbVersion.v_1_12)]
        public async Task DeleteAsync_OnSupportedVersion_SendsV2CompatibleRequest(InfluxDbVersion version)
        {
            var handler = new RecordingHandler();
            var client = CreateClient(version, handler);
            var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var stop = start.AddHours(1);

            var response = await client.Delete.DeleteAsync(new DeleteRequest
            {
                Database = "metrics db",
                RetentionPolicy = "autogen",
                Start = start,
                Stop = stop,
                Predicate = "_measurement=\"temperature\" AND location=\"west\"",
                Precision = "ms"
            });

            Assert.True(response.Success);
            Assert.Equal(HttpMethod.Post, handler.Method);
            Assert.Equal("/api/v2/delete", handler.RequestUri.AbsolutePath);
            Assert.Contains("bucket=metrics%20db%2Fautogen", handler.RequestUri.Query);
            Assert.Contains("precision=ms", handler.RequestUri.Query);
            Assert.DoesNotContain("u=", handler.RequestUri.Query);
            Assert.DoesNotContain("p=", handler.RequestUri.Query);
            Assert.Equal("Token influx-user:influx-password", handler.Authorization);
            Assert.Equal("application/json", handler.ContentType);

            using var document = JsonDocument.Parse(handler.Body);
            var root = document.RootElement;
            Assert.Equal(start.ToString("O"), root.GetProperty("start").GetString());
            Assert.Equal(stop.ToString("O"), root.GetProperty("stop").GetString());
            Assert.Equal("_measurement=\"temperature\" AND location=\"west\"", root.GetProperty("predicate").GetString());
        }

        [Fact]
        public async Task DeleteAsync_WithAuthorizationToken_UsesTokenDirectly()
        {
            var handler = new RecordingHandler();
            var client = CreateClient(InfluxDbVersion.v_1_12, handler);

            await client.Delete.DeleteAsync(ValidRequest(), "api-token");

            Assert.Equal("Token api-token", handler.Authorization);
        }

        [Fact]
        public async Task DeleteAsync_OnOlderVersion_FailsWithoutSendingRequest()
        {
            var handler = new RecordingHandler();
            var client = CreateClient(InfluxDbVersion.v_1_3, handler);

            var exception = await Assert.ThrowsAsync<NotSupportedException>(
                () => client.Delete.DeleteAsync(ValidRequest()));

            Assert.Contains("requires InfluxDB 1.8 or newer", exception.Message);
            Assert.Equal(0, handler.CallCount);
        }

        [Fact]
        public async Task DeleteAsync_WithInvalidRange_FailsWithoutSendingRequest()
        {
            var handler = new RecordingHandler();
            var client = CreateClient(InfluxDbVersion.v_1_12, handler);
            var request = ValidRequest();
            request.Stop = request.Start;

            await Assert.ThrowsAsync<ArgumentException>(() => client.Delete.DeleteAsync(request));

            Assert.Equal(0, handler.CallCount);
        }

        [Fact]
        public void ExistingVersionNumericValues_ArePreserved()
        {
            Assert.Equal(0, (int)InfluxDbVersion.Latest);
            Assert.Equal(1, (int)InfluxDbVersion.v_1_3);
            Assert.Equal(2, (int)InfluxDbVersion.v_1_0_0);
            Assert.Equal(3, (int)InfluxDbVersion.v_0_9_6);
            Assert.Equal(4, (int)InfluxDbVersion.v_0_9_5);
            Assert.Equal(5, (int)InfluxDbVersion.v_0_9_2);
            Assert.Equal(6, (int)InfluxDbVersion.v_0_8_x);
        }

        private static DeleteRequest ValidRequest()
        {
            return new DeleteRequest
            {
                Database = "metrics",
                RetentionPolicy = "autogen",
                Start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                Stop = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)
            };
        }

        private static InfluxDbClient CreateClient(InfluxDbVersion version, RecordingHandler handler)
        {
            return new InfluxDbClient(
                "http://localhost:8086/",
                "influx-user",
                "influx-password",
                version,
                httpClient: new HttpClient(handler));
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            public int CallCount { get; private set; }
            public HttpMethod Method { get; private set; }
            public Uri RequestUri { get; private set; }
            public string Authorization { get; private set; }
            public string ContentType { get; private set; }
            public string Body { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                CallCount++;
                Method = request.Method;
                RequestUri = request.RequestUri;
                Authorization = request.Headers.GetValues("Authorization").Single();
                ContentType = request.Content.Headers.ContentType.MediaType;
                Body = await request.Content.ReadAsStringAsync(cancellationToken);

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        }
    }
}
