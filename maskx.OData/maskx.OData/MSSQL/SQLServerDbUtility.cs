using maskx.OData.DataSource;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace maskx.OData.MSSQL
{
    public class SQLServerDbUtility : DbUtility
    {
        public override DbParameter CreateParameter(object value, List<DbParameter> pars)
        {
            var par = new System.Data.SqlClient.SqlParameter("@p" + pars.Count, value);
            pars.Add(par);
            return par;
        }

        public override DbParameter CreateParameter(string name, object value)
        {
            var par = new System.Data.SqlClient.SqlParameter(name, value);
            return par;
        }

        public override string SafeDbObject(string obj)
        {
            return string.Format("[{0}]", obj);
        }
    }
}
