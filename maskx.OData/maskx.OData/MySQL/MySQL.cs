using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using maskx.Database;
using maskx.OData.MySQL;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;

namespace maskx.OData.DataSource
{
    public class MySQL : SQLBase
    {
        public MySQL(string name, string connectionString) : base(name, connectionString) { }
        protected override EdmPrimitiveTypeKind? GetEdmType(string dbType)
        {
            switch (dbType.ToLower())
            {
                case "uniqueidentifier":
                    return EdmPrimitiveTypeKind.Guid;
                case "xml":
                case "varchar":
                case "nvarchar":
                case "text":
                case "ntext":
                    return EdmPrimitiveTypeKind.String;
                case "char":
                case "nchar":
                    return EdmPrimitiveTypeKind.String;
                case "money":
                case "smallmoney":
                case "numeric":
                case "decimal":
                    return EdmPrimitiveTypeKind.Decimal;
                case "smallint":
                    return EdmPrimitiveTypeKind.Int16;
                case "int":
                    return EdmPrimitiveTypeKind.Int32;
                case "bigint":
                    return EdmPrimitiveTypeKind.Int64;
                case "tinyint":
                    return EdmPrimitiveTypeKind.Byte;
                case "float":
                    return EdmPrimitiveTypeKind.Double;
                case "real":
                    return EdmPrimitiveTypeKind.Single;
                case "bit":
                    return EdmPrimitiveTypeKind.Boolean;
                case "date":
                case "timestamp":
                case "time":
                case "smalldatetime":
                case "datetime":
                    return EdmPrimitiveTypeKind.DateTimeOffset;
                case "image":
                case "varbinary":
                case "binary":
                    return EdmPrimitiveTypeKind.Byte;
                default:
                    return null;
            }
        }

        protected override IEnumerable<(string ForeignKeyName,
            string ParentSchemaName,
            string ParentName,
            string ParentColumnName,
            string RefrencedName,
            string RefrencedSchemaName,
            string RefrencedColumnName)>
            GetRelationship()
        {
            yield break;
        }

        protected override IEnumerable<(string SchemaName,
            string StoredProcedureName,
            string ParameterName,
            string ParameterDataType,
            string ParemeterMode,
            string UserDefinedTypeSchema,
            string UserDefinedTypeName,
            int MaxLength,
            int NumericScale)> GetStoredProcedures()
        {
            String cmdtxt = @"select r.ROUTINE_SCHEMA,
    r.ROUTINE_NAME,
    p.PARAMETER_NAME,
    p.DATA_TYPE,
    p.PARAMETER_MODE,
    p.CHARACTER_MAXIMUM_LENGTH,
    p.NUMERIC_SCALE   
from information_schema.ROUTINES AS r
  left join  information_schema.parameters AS p 
    on p.SPECIFIC_NAME=r.ROUTINE_NAME 
    and
        p.SPECIFIC_SCHEMA =r.ROUTINE_SCHEMA";
            using (MySqlConnection conn = new MySqlConnection(this.ConnectionString))
            {
                
                MySqlCommand cmd = new MySqlCommand(cmdtxt, conn);
                conn.Open();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    yield return (reader.GetString("ROUTINE_SCHEMA"),
                                       reader.GetString("ROUTINE_NAME"),
                                       reader.IsDBNull("PARAMETER_NAME") ? string.Empty : reader.GetString("PARAMETER_NAME"),
                                       reader.IsDBNull("DATA_TYPE") ? string.Empty : reader.GetString("DATA_TYPE"),
                                       reader.IsDBNull("PARAMETER_MODE") ? string.Empty : reader.GetString("PARAMETER_MODE"),
                                       string.Empty,
                                       string.Empty,
                                       reader.IsDBNull("CHARACTER_MAXIMUM_LENGTH") ? 0 : reader.GetInt32("CHARACTER_MAXIMUM_LENGTH"),
                                       reader.IsDBNull("NUMERIC_SCALE") ? 0 : reader.GetInt32("NUMERIC_SCALE"));
                }
                conn.Close();
            }
        }

        protected override IEnumerable<(string SchemaName,
            string TableName,
            string ColumnName,
            string DataType,
            bool isKey)> GetTables()
        {
            String cmdtxt = @"select  
    TABLE_SCHEMA  AS 'SCHEMA_NAME'
    ,TABLE_NAME
    ,COLUMN_NAME
    ,DATA_TYPE
    ,IS_NULLABLE
    ,CHARACTER_MAXIMUM_LENGTH 
    ,NUMERIC_PRECISION
    ,NUMERIC_SCALE
    ,COLUMN_KEY
    from INFORMATION_SCHEMA.COLUMNS
   ";
            using (MySqlConnection conn = new MySqlConnection(this.ConnectionString))
            {
                
                MySqlCommand cmd = new MySqlCommand(cmdtxt, conn);
                conn.Open();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    yield return (reader.GetString("SCHEMA_NAME"),
                                       reader.GetString("TABLE_NAME"),
                                        reader.GetString("COLUMN_NAME"),
                                        reader.GetString("DATA_TYPE"),
                                        !reader.IsDBNull("COLUMN_KEY") && reader.GetString("COLUMN_KEY") == "PRI" ? true : false
                                       );
                }
                conn.Close();
            }
        }

        protected override IEnumerable<(string SchemaName,
            string FunctionName,
            string ParameterName,
            string ParameterDataType,
            string UserDefinedTypeSchema,
            string UserDefinedTypeName,
            int MaxLength,
            int NumericScale)> GetFunctions()
        {
            yield break;
        }

        protected override IEnumerable<(string ColumnName,
            string DataType,
            int Length,
            bool isNullable)> GetTableValueType(string schema, string name)
        {
            yield break;
        }

        protected override IEnumerable<(string ColumnName, string DataType, int Length, bool isNullable)> GetUserDefinedType(string schema, string name)
        {
            yield break;
        }

        protected override IEnumerable<(string SchemaName, string ViewName, string ColumnName, string DataType, bool isKey)> GetViews()
        {
            yield break;
        }

        protected override DbAccess CreateDbAccess(string connectionString)
        {
            return new DbAccess(MySql.Data.MySqlClient.MySqlClientFactory.Instance, connectionString);
        }
        protected override string GetCmdTemplete(MethodType methodType, ODataQueryOptions options)
        {
            switch (methodType)
            {
                case MethodType.Replace:
                    break;
                case MethodType.Merge:
                    break;
                case MethodType.Delete:
                    break;
                case MethodType.Create:
                    return "insert into {0}.{1} ({2}) values ({3}); select LAST_INSERT_ID() ";
                case MethodType.Get:
                    //0:Top,1:Select,2:Schema,3:Table,4:where,5:orderby,6:skip
                    if (options.Skip != null)
                        return "select {1} from {2}.{3} {4} {5} LIMIT {0} OFFSET {6}";
                    if (options.Top != null)
                        return "select top {0} {1} from {2}.{3} {4} {5}";
                    return "select {0} {1} from {2}.{3} {4} {5}";
                case MethodType.Count:
                    break;
                case MethodType.Function:
                    break;
                case MethodType.Action:
                    break;
                default:
                    break;
            }
            return string.Empty;
        }

        protected override string GetCmdTemplete(MethodType methodType, ExpandedNavigationSelectItem expanded)
        {
            switch (methodType)
            {
                case MethodType.Replace:
                    break;
                case MethodType.Merge:
                    break;
                case MethodType.Delete:
                    break;
                case MethodType.Create:
                    return "insert into {0}.{1} ({2}) values ({3}); select LAST_INSERT_ID() ";
                case MethodType.Get:
                    //0:Top,1:Select,2:Schema,3:Table,4:where,5:orderby,6:skip
                    if (expanded.SkipOption != null)
                        return "select {1} from {2}.{3} {4} {5} LIMIT {0} OFFSET {6}";
                    if (expanded.TopOption != null)
                        return "select top {0} {1} from {2}.{3} {4} {5}";
                    return "select {0} {1} from {2}.{3} {4} {5}";
                case MethodType.Count:
                    break;
                case MethodType.Function:
                    break;
                case MethodType.Action:
                    break;
                default:
                    break;
            }
            return string.Empty;
        }

        protected override string GetCmdTemplete(MethodType methodType)
        {
            switch (methodType)
            {
                case MethodType.Replace:
                    break;
                case MethodType.Merge:
                    break;
                case MethodType.Delete:
                    break;
                case MethodType.Create:
                    return "insert into {0}.{1} ({2}) values ({3}); select LAST_INSERT_ID() ";
                case MethodType.Get:
                    break;
                case MethodType.Count:
                    break;
                case MethodType.Function:
                    break;
                case MethodType.Action:
                    break;
                default:
                    break;
            }
            return string.Empty;
        }

        readonly MySQLDbUtility _MySQLDbUtility = new MySQLDbUtility();
        protected override DbUtility _DbUtility { get { return _MySQLDbUtility; } }
    }
}
