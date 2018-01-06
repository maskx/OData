using System;
using System.Data;

namespace maskx.OData.Sql
{
    public class ParameterInfo
    {
        public string Name
        {
            get;
            set;
        }
        public int Length
        {
            get;
            set;
        }
        public SqlDbType SqlDbType
        {
            get;
            set;
        }
        public Type Type
        {
            get
            {
                return Utility.SqlType2CsharpType(SqlDbType);
            }
        }
        public ParameterDirection Direction
        {
            get;
            set;
        }
    }
}
