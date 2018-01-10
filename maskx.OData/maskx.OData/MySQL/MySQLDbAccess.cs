using maskx.Database;
using System;
using System.Collections.Generic;
using System.Text;
using MySql.Data.MySqlClient;

namespace maskx.OData.Sql
{
    internal class MySQLDbAccess : DbAccess<MySqlParameter, MySqlParameterCollection>
    {
        public MySQLDbAccess(string connectionString) : base(new MySqlConnection(connectionString))
        {

        }
    }
}
