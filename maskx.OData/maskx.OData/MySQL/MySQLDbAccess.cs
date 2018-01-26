using maskx.Database;
using System;
using System.Collections.Generic;
using System.Text;
using MySql.Data.MySqlClient;

namespace maskx.OData.Sql
{
    internal class MySQLDbAccess : DbAccess
    {
        public MySQLDbAccess(string connectionString)
            : base(MySql.Data.MySqlClient.MySqlClientFactory.Instance, connectionString)
        {

        }
    }
}
