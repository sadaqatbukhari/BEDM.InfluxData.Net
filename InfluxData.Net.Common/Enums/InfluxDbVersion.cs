namespace InfluxData.Net.Common.Enums
{
    /// <summary>
    /// InfluxDb version used by InfluxDbClient instance.
    /// </summary>
    public enum InfluxDbVersion
    {
        Latest,
        v_1_3,
        v_1_0_0,
        v_0_9_6,
        v_0_9_5,
        v_0_9_2,
        v_0_8_x,

        // New values are appended to preserve the numeric values of existing members.
        v_1_8,
        v_1_12
    }
}
