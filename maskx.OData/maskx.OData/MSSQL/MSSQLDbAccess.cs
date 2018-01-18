﻿using maskx.Database;
using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;

namespace maskx.OData.Sql
{
    internal class MSSQLDbAccess : DbAccess<SqlParameter, SqlParameterCollection>
    {
        public MSSQLDbAccess(string connectionString) : base(new SqlConnection(connectionString))
        {

        }
    }
}