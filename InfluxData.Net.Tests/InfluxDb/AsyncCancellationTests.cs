using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using InfluxData.Net.InfluxDb.Models;
using InfluxData.Net.Kapacitor;
using Xunit;

namespace InfluxData.Net.Tests.InfluxDb
{
    public class AsyncCancellationTests
    {
        [Theory]
        [InlineData("query")]
        [InlineData("parameterized")]
        [InlineData("database")]
        [InlineData("write")]
        [InlineData("delete")]
        [InlineData("kapacitor")]
        public async Task PublicOperations_CancelInFlightRequests(string operation)
        {
            using var handler = new WaitingHandler();
            using var http = new HttpClient(handler);
            using var client = new InfluxDbClient("http://localhost:8086/", "user", "password",
                InfluxDbVersion.v_1_8, httpClient: http);
            using var cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            Task pending = operation switch
            {
                "query" => client.Client.QueryAsync("SHOW MEASUREMENTS", cancellationToken: token),
                "parameterized" => client.Client.QueryAsync("SELECT * FROM $name", new { name = "temperature" }, cancellationToken: token),
                "database" => client.Database.CreateDatabaseAsync("metrics", token),
                "write" => client.Client.WriteAsync(Point(), "metrics", cancellationToken: token),
                "delete" => client.Delete.DeleteAsync(new DeleteRequest
                {
                    Database = "metrics", RetentionPolicy = "autogen",
                    Start = DateTimeOffset.UtcNow.AddHours(-1), Stop = DateTimeOffset.UtcNow
                }, cancellationToken: token),
                _ => new KapacitorClient("http://localhost:9092/", KapacitorVersion.Latest, http)
                    .Task.GetTasksAsync(token)
            };
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task Query_CancelsWhileReadingResponseBody()
        {
            using var content = new WaitingContent();
            using var http = new HttpClient(new BodyHandler(content));
            using var client = new InfluxDbClient("http://localhost:8086/", "user", "password",
                InfluxDbVersion.v_1_8, httpClient: http);
            using var cancellation = new CancellationTokenSource();
            var pending = client.Client.QueryAsync("SHOW MEASUREMENTS", cancellationToken: cancellation.Token);
            await content.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task BatchWriter_StopAsyncCancelsActiveWriteWithoutReportingError()
        {
            using var handler = new WaitingHandler();
            using var http = new HttpClient(handler);
            using var client = new InfluxDbClient("http://localhost:8086/", "user", "password",
                InfluxDbVersion.v_1_8, httpClient: http);
            var writer = client.Serie.CreateBatchWriter("metrics");
            var errors = 0;
            writer.OnError += (_, _) => Interlocked.Increment(ref errors);
            writer.AddPoint(Point());
            writer.Start(interval: 1);
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Throws<InvalidOperationException>(() => writer.Start());
            await writer.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, errors);
            writer.Start(interval: 60000);
            await writer.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task BatchWriter_SuccessfulWritesContinueWithoutOverlap()
        {
            using var handler = new SequentialHandler();
            using var http = new HttpClient(handler);
            using var client = new InfluxDbClient("http://localhost:8086/", "user", "password",
                InfluxDbVersion.v_1_8, httpClient: http);
            var writer = client.Serie.CreateBatchWriter("metrics");
            writer.AddPoints(new[] { Point(), Point() });
            writer.Start(interval: 1, maxPointsPerBatch: 1);
            try
            {
                await handler.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                await writer.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            Assert.Equal(1, handler.MaxActive);
        }

        private static Point Point() => new Point
        {
            Name = "temperature", Fields = new Dictionary<string, object> { ["value"] = 42 }
        };

        private sealed class WaitingHandler : HttpMessageHandler
        {
            public readonly TaskCompletionSource Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Started.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        }

        private sealed class SequentialHandler : HttpMessageHandler
        {
            public readonly TaskCompletionSource Completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _active;
            private int _calls;
            public int MaxActive;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                MaxActive = Math.Max(MaxActive, Interlocked.Increment(ref _active));
                try { await Task.Delay(20, cancellationToken); }
                finally { Interlocked.Decrement(ref _active); }
                if (Interlocked.Increment(ref _calls) == 2) Completed.TrySetResult();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        }

        private sealed class BodyHandler(HttpContent content) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }

        private sealed class WaitingContent : HttpContent
        {
            public readonly TaskCompletionSource Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
                => SerializeToStreamAsync(stream, context, CancellationToken.None);
            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context, CancellationToken cancellationToken)
            {
                Started.TrySetResult();
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            protected override bool TryComputeLength(out long length) { length = 0; return false; }
        }
    }
}
