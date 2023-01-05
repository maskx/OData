using System.Collections.Generic;

namespace maskx.OData.Extensions
{
    public class DataSourceBuilder
    {
        internal DataSourceBuilder(DynamicODataOptions options)
        {
            this._Options = options;
        }
        DynamicODataOptions _Options;
        internal IDictionary<string, IDataSource> DataSources { get; } = new Dictionary<string, IDataSource>();
        public DataSourceBuilder AddDataSource(string prefix, IDataSource dataSource)
        {
            dataSource.DynamicODataOptions = _Options;
            DataSources[prefix] = dataSource;
            return this;
        }
    }
}
