using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace maskx.OData
{
    internal class DataSourceProvider
    {
        static Dictionary<string, IDataSource> DataSource = new Dictionary<string, IDataSource>();
        public static void AddDataSource(IDataSource dataSource)
        {
            DataSource.Add(dataSource.Name, dataSource);
        }

        public static IEdmModel GetEdmModel(string dataSourceName)
        {
            return GetDataSource(dataSourceName).Model;
        }

        public static IDataSource GetDataSource(string dataSourceName)
        {
            dataSourceName = dataSourceName == null ? string.Empty : dataSourceName.ToLowerInvariant();
            IDataSource ds = null;
            if (DataSource.TryGetValue(dataSourceName, out ds))
                return ds;
            throw new InvalidOperationException(
                string.Format("Data source: {0} is not registered.", dataSourceName));

        }

    }
}
