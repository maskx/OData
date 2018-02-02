using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace maskx.OData.DataSource
{
    public abstract class DbUtility
    {
        public abstract string SafeDbObject(string obj);
        public abstract DbParameter CreateParameter(object value, List<DbParameter> pars);
    }
}
