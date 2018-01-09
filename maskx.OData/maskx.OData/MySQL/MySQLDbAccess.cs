using maskx.Database;
using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;

namespace maskx.OData.Sql
{
    internal class MySQLDbAccess : DbAccess<SqlParameter, SqlParameterCollection>
    {
        public MySQLDbAccess(string connectionString) : base(new SqlConnection(connectionString))
        {

        }
    }
}
