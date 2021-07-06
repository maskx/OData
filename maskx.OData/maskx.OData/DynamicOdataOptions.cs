using System.Collections.Generic;

namespace maskx.OData
{
    public class DynamicOdataOptions
    {
        public IDictionary<string, IDataSource> DataSources { get; } = new Dictionary<string, IDataSource>();
        public DynamicOdataOptions AddDataSource(string prefix,IDataSource dataSource)
        {
            DataSources[prefix] = dataSource;
            return this;
        }
        public IDataSource GetDataSource(string prefix)
        {
            if (this.DataSources.TryGetValue(prefix, out IDataSource dataSource))
                return dataSource;
            return null;
        }
    }
}
