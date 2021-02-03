using Microsoft.AspNetCore.OData;
using System;
using System.Collections.Generic;

namespace maskx.OData
{
    public class DynamicOdataOptions
    {
        public ODataOptions ODataOptions { get; set; }
        public IServiceProvider ServiceProvider { get; set; }
        private IDictionary<string, IDataSource> DataSources { get; } = new Dictionary<string, IDataSource>();
        public DynamicOdataOptions AddDataSource(string prefix,IDataSource dataSource)
        {
            DataSources[prefix] = dataSource;
            this.ODataOptions.AddModel(prefix, dataSource.Model);
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
