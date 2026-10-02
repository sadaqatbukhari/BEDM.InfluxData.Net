using System.Collections.Generic;
using InfluxData.Net.InfluxDb.Models;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System;
using System.Threading;
using InfluxData.Net.InfluxDb.ClientModules;
using InfluxData.Net.Common.Constants;

namespace InfluxData.Net.InfluxDb.ClientSubModules
{
    public class BatchWriter : IBatchWriterFactory
    {
        private readonly IBasicClientModule _basicClientModule;
        private string _dbName;
        private string _retentionPolicy;
        private string _precision;
        private int _interval;
        private bool _continueOnError;
        private volatile bool _isRunning;
        private readonly object _lifecycleLock = new object();
        private CancellationTokenSource _runCancellation;
        private Task _runTask = Task.CompletedTask;
        private long _maxPointsPerBatch;

        /// <summary>
        /// Concurrent readings queue.
        /// <see cref="http://www.codethinked.com/blockingcollection-and-iproducerconsumercollection"/>
        /// </summary>
        private BlockingCollection<Point> _pointCollection;

        /// <summary>
        /// On batch writing error event handler.
        /// </summary>
        public event EventHandler<Exception> OnError = delegate { };

        /// <summary>
        /// Constructor used by InfluxDbClient to inject the IBasicClientModule.
        /// </summary>
        internal BatchWriter(IBasicClientModule basicClientModule)
        {
            _basicClientModule = basicClientModule;
        }

        /// <summary>
        /// Constructor used by BatchWriter to create new instances of BatchWriter (through the CreateBatchWriter() method) with
        /// IBasicClientModule from InfluxDbClient. This instance BatchWriter instance is served to the end users.
        /// </summary>
        private BatchWriter(IBasicClientModule basicClientModule, string dbName, string retenionPolicy = null, string precision = TimeUnit.Milliseconds)
        {
            _basicClientModule = basicClientModule;
            _dbName = dbName;
            _retentionPolicy = retenionPolicy;
            _precision = precision;
            _pointCollection = new BlockingCollection<Point>();
        }

        public virtual IBatchWriter CreateBatchWriter(string dbName, string retenionPolicy = null, string precision = TimeUnit.Milliseconds)
        {
            return new BatchWriter(_basicClientModule, dbName, retenionPolicy, precision);
        }

        /// <summary>
        /// Starts the batch writer.
        /// <param name="interval">Interval between writes (milliseconds).</param>
        /// <param param name="continueOnError">Should continue running on write error? (defaults to false)</param>
        /// <param name="maxPointsPerBatch">Max batch point count (long max by default).</param>
        /// </summary>
        public virtual void Start(int interval = 1000, bool continueOnError = false, long maxPointsPerBatch = long.MaxValue, CancellationToken cancellationToken = default)
        {
            if (interval <= 0)
                throw new ArgumentException("Interval must be a positive value (milliseconds)");

            if (maxPointsPerBatch <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPointsPerBatch));
            cancellationToken.ThrowIfCancellationRequested();
            lock (_lifecycleLock)
            {
                if (!_runTask.IsCompleted)
                    throw new InvalidOperationException("The batch writer is already running or stopping.");
                _runCancellation?.Dispose();
                _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _continueOnError = continueOnError;
                _interval = interval;
                _isRunning = true;
                _maxPointsPerBatch = maxPointsPerBatch;
                _runTask = this.EnqueueBatchWritingAsync();
            }
        }

        /// <summary>
        /// Adds a single point to the BatchWriter points collection (uses BlockingCollection 
        /// internally for thread safety).
        /// </summary>
        /// <see cref="http://www.codethinked.com/blockingcollection-and-iproducerconsumercollection"/>
        /// <param name="point">Point to write.</param>
        public virtual void AddPoint(Point point)
        {
            _pointCollection.Add(point);
        }

        /// <summary>
        /// Adds multiple points to the BatchWriter points collection (uses BlockingCollection 
        /// internally for thread safety).
        /// </summary>
        /// <see cref="http://www.codethinked.com/blockingcollection-and-iproducerconsumercollection"/>
        /// <param name="points">Points to write.</param>
        public virtual void AddPoints(IEnumerable<Point> points)
        {
            foreach (var point in points)
            {
                _pointCollection.Add(point);
            }
        }

        /// <summary>
        /// Stops the batch writer.
        /// </summary>
        public virtual void Stop()
        {
            lock (_lifecycleLock)
            {
                _isRunning = false;
                _runCancellation?.Cancel();
            }
        }

        /// <summary>Stops the writer and waits for the active write to finish cancelling.
        /// Queued points remain available for a subsequent start.</summary>
        public virtual async Task StopAsync(CancellationToken cancellationToken = default)
        {
            Task runTask;
            lock (_lifecycleLock)
            {
                Stop();
                runTask = _runTask;
            }
            await runTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the maximum size (point count) of a batch to commit to InfluxDB. If the collection currently 
        /// holds more than the `pointCount` points, any overflow will be commited in future requests on FIFO principle.
        /// </summary>
        /// <param name="pointCount">Max batch point count (long max by default).</param>
        public void SetMaxBatchSize(long pointCount)
        {
            if (pointCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(pointCount));
            _maxPointsPerBatch = pointCount;
        }

        /// <summary>
        /// Waits for the "interval" amount of time then writes all the current
        /// blocking collection points to InfluxDb and calls itself again.
        /// </summary>
        /// <returns>Task.</returns>
        protected virtual async Task EnqueueBatchWritingAsync()
        {
            var cancellationToken = _runCancellation.Token;
            try
            {
                while (_isRunning)
                {
                    await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    await this.WriteBatchedPointsAsync().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Cancellation is an expected shutdown, not a write error.
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// Dequeues all the current points from the blocking collection
        /// and writes them all to InfluxDb in a single request.
        /// </summary>
        /// <returns>Task.</returns>
        protected virtual async Task WriteBatchedPointsAsync()
        {
            var pointsToSendCount = Math.Min(_pointCollection.Count, _maxPointsPerBatch);
            IList<Point> points = new List<Point>();

            for (var i = 0; i < pointsToSendCount; i++)
            {
                Point point;
                var dequeueSuccess = _pointCollection.TryTake(out point);

                if (dequeueSuccess)
                {
                    points.Add(point);
                }
                else
                {
                    RaiseError(new InvalidOperationException("Could not dequeue the collection"));
                    return;
                }
            }

            if (points.Count > 0)
            {
                try
                {
                    await _basicClientModule.WriteAsync(points, _dbName, _retentionPolicy, _precision,
                        _runCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_runCancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    RaiseError(exception);
                }
            }
        }

        /// <summary>
        /// Raises an error event and stops the BatchWritter unless continueOnError is set.
        /// </summary>
        /// <param name="exception">Exception to raise.</param>
        protected virtual void RaiseError(Exception exception)
        {
            if (!_continueOnError)
                _isRunning = false;

            this.OnError(this, exception);
        }
    }
}
