using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace maskx.OData
{
    public class DynamicOData
    {
        public static void AddDataSource(IDataSource dataSource)
        {
            DataSourceProvider.AddDataSource(dataSource);
        }
        public static Action<RequestInfo> BeforeExcute;
    }
}
