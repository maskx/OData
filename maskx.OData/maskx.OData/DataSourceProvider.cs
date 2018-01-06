using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;

namespace maskx.OData
{
    internal class DataSourceProvider
    {
        static Dictionary<string, IDataSource> DataSource = new Dictionary<string, IDataSource>();
        public static void AddDataSource(string key, IDataSource dataSource)
        {
            DataSource.Add(key, dataSource);
        }

        public static IEdmModel GetEdmModel(string dataSourceName)
        {
            return GetDataSource(dataSourceName).Model;
        }

        public static IDataSource GetDataSource(string dataSourceName)
        {
            dataSourceName = dataSourceName == null ? string.Empty : dataSourceName;
            IDataSource ds = null;
            if (DataSource.TryGetValue(dataSourceName, out ds))
                return ds;
            throw new InvalidOperationException(
                string.Format("Data source: {0} is not registered.", dataSourceName));

        }
    }
}
