using System;

namespace InfluxData.Net.InfluxDb.Models
{
    /// <summary>
    /// Describes a v2-compatible delete request supported by InfluxDB 1.8 and newer.
    /// </summary>
    public class DeleteRequest
    {
        /// <summary>
        /// Database containing the data to delete.
        /// </summary>
        public string Database { get; set; }

        /// <summary>
        /// Retention policy containing the data to delete.
        /// </summary>
        public string RetentionPolicy { get; set; }

        /// <summary>
        /// Inclusive beginning of the time range.
        /// </summary>
        public DateTimeOffset Start { get; set; }

        /// <summary>
        /// Exclusive end of the time range.
        /// </summary>
        public DateTimeOffset Stop { get; set; }

        /// <summary>
        /// Optional predicate, for example: _measurement="temperature" AND location="west".
        /// Only measurement and tag predicates are supported by InfluxDB's delete API.
        /// </summary>
        public string Predicate { get; set; }

        /// <summary>
        /// Optional timestamp precision: ns, us, ms, or s.
        /// </summary>
        public string Precision { get; set; }
    }
}
