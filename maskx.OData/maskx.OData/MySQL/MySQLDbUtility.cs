using maskx.OData.SQLSource;
using System.Collections.Generic;
using System.Data.Common;

namespace maskx.OData.MySQL
{
    public class MySQLDbUtility : DbUtility
    {
        public override DbParameter CreateParameter(object value, List<DbParameter> pars)
        {
            var par = new MySql.Data.MySqlClient.MySqlParameter("?p" + pars.Count, value);
            pars.Add(par);
            return par;
        }

        public override DbParameter CreateParameter(string name, object value)
        {
            var par = new MySql.Data.MySqlClient.MySqlParameter(name, value);
            return par;
        }

        public override string SafeDbObject(string obj)
        {
            return string.Format("`{0}`", obj);
        }

    }
}
