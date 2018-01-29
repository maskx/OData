using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using maskx.Database;
using maskx.OData.Database;
using maskx.OData.MySQL;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;

namespace maskx.OData.Database
{
    public class MySQL : DbSourceBase
    {
        private MySQLFilterBinder _FilterBinder = new MySQLFilterBinder();
        private MySQLOrderByBinder _OrderByBinder = new MySQLOrderByBinder();
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
            using (MySqlConnection conn = new MySqlConnection(this.ConnectionString))
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
            using (MySqlConnection conn = new MySqlConnection(this.ConnectionString))
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
    where TABLE_SCHEMA='TimeCollection';";
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

        protected override string BuildQueryCmd(ODataQueryOptions options, List<DbParameter> pars, string target = "")
        {
            var cxt = options.Context;
            string table = target;
            if (string.IsNullOrEmpty(target))
            {
                var t = cxt.ElementType as EdmEntityType;
                table = string.Format("`{0}`.`{1}`", t.Namespace, t.Name);
            }

            string cmdSql = "select {0} {1} from {2} {3} {4} {5} {6}";
            string top = string.Empty;
            string skip = string.Empty;
            string fetch = string.Empty;
            string orderby = _OrderByBinder.ParseOrderBy(options);
            if (options.Count == null && options.Top != null)
            {
                if (options.Skip != null)
                {
                    skip = string.Format("OFFSET {0} ", options.Skip.RawValue);
                    fetch = string.Format("limit {0} ", options.Top.RawValue);
                    top = string.Empty;
                    if (string.IsNullOrEmpty(orderby))
                    {
                        var entityType = cxt.ElementType as EdmEntityType;
                        var keyDefine = entityType.DeclaredKey.First();
                        orderby = string.Format(" order by `{0}` ", keyDefine.Name);
                    }
                }
                else
                    top = "top " + options.Top.RawValue;
            }

            var cmdtxt = string.Format(cmdSql
                , top
                , options.ParseSelect()
                , table
                , _FilterBinder.ParseFilter(options, pars)
                , orderby
                , fetch
                , skip);
            return cmdtxt;
        }

        protected override string BuildExpandQueryCmd(EdmEntityObject edmEntity, ExpandedNavigationSelectItem expanded, List<DbParameter> pars)
        {
            string cmdSql = "select {0} {1} from `{2}`.`{3}` where  {4} {5} {6} {7}";
            string schema = string.Empty;
            string table = string.Empty;
            string top = string.Empty;
            string skip = string.Empty;
            string fetch = string.Empty;
            string where = string.Empty;
            string safeVar = string.Empty;
            var wp = new List<string>();
            foreach (NavigationPropertySegment item2 in expanded.PathToNavigationProperty)
            {
                foreach (var p in item2.NavigationProperty.ReferentialConstraint.PropertyPairs)
                {
                    edmEntity.TryGetPropertyValue(p.DependentProperty.Name, out object v);
                    safeVar = Extensions.SafeSQLVar(p.PrincipalProperty.Name) + pars.Count;
                    wp.Add(string.Format("`{0}`=?{1}", p.PrincipalProperty.Name, safeVar));
                    pars.Add(new MySqlParameter(safeVar, v));
                }
            }
            where = string.Join("and", wp);
            var entityType = expanded.NavigationSource.EntityType();
            schema = entityType.Namespace;
            table = entityType.Name; //expanded.NavigationSource.Name;
            if (!expanded.CountOption.HasValue && expanded.TopOption.HasValue)
            {
                if (expanded.SkipOption.HasValue)
                {
                    skip = string.Format("OFFSET {0} ROWS", expanded.SkipOption.Value);
                    fetch = string.Format("FETCH NEXT {0} ROWS ONLY", expanded.TopOption.Value);
                    top = string.Empty;
                }
                else
                    top = "top " + expanded.TopOption.Value;
            }
            return string.Format(cmdSql
                , top
                , expanded.ParseSelect()
                , schema
                , table
                , where
                , _FilterBinder.ParseFilter(expanded, pars)
                , _OrderByBinder.ParseOrderBy(expanded)
                , skip
                , fetch);
        }

        protected override string BuildQueryByKeyCmd(string key, ODataQueryOptions options, List<DbParameter> pars)
        {
            var cxt = options.Context;
            var entityType = cxt.ElementType as EdmEntityType;
            var keyDefine = entityType.DeclaredKey.First();
            string cmdSql = "select {0} from `{1}` where `{2}`=?{3}";
            string safep = Extensions.SafeSQLVar(keyDefine.Name);
            pars.Add(new MySqlParameter(safep, key.ChangeType(keyDefine.Type.PrimitiveKind())));
            return string.Format(cmdSql
                , options.ParseSelect()
                , entityType.Name
                , keyDefine.Name,
                safep);
        }

        protected override string BuildTVFTarget(IEdmFunction func, JObject parameterValues, List<DbParameter> sqlpars)
        {
            throw new NotImplementedException();
        }

        protected override string BuildMergeCmd(string key, IEdmEntityObject entity, List<DbParameter> pars)
        {
            string cmdTemplate = "update `{0}`.`{1}` set {2} where `{3}`=?{4} ";
            var entityType = entity.GetEdmType().Definition as EdmEntityType;
            var keyDefine = entityType.DeclaredKey.First();
            List<string> cols = new List<string>();
            string safep = string.Empty;
            object v = null;
            int index = 0;
            foreach (var p in (entity as Delta).GetChangedPropertyNames())
            {
                if (entity.TryGetPropertyValue(p, out v))
                {
                    safep = Extensions.SafeSQLVar(p) + index++;
                    cols.Add(string.Format("`{0}`=?{1}", p, safep));
                    pars.Add(new MySqlParameter("?" + safep, v));
                }
            }
            safep = Extensions.SafeSQLVar(keyDefine.Name) + index++;
            pars.Add(new MySqlParameter("?" + safep, key.ChangeType(keyDefine.Type.PrimitiveKind())));

            return string.Format(cmdTemplate, entityType.Namespace, entityType.Name, string.Join(", ", cols), keyDefine.Name, safep);
        }

        protected override string BuildReplaceCmd(string key, IEdmEntityObject entity, List<DbParameter> pars)
        {
            string cmdTemplate = "update `{0}`.`{1}` set {2} where `{3}`=?{4}  ";
            var entityType = entity.GetEdmType().Definition as EdmEntityType;
            var keyDefine = entityType.DeclaredKey.First();
            List<string> cols = new List<string>();
            string safep = string.Empty;
            object v = null;
            int index = 0;
            foreach (var p in entityType.Properties())
            {
                if (p.PropertyKind == EdmPropertyKind.Navigation) continue;
                if (entity.TryGetPropertyValue(p.Name, out v))
                {
                    if (keyDefine.Name == p.Name) continue;
                    safep = Extensions.SafeSQLVar(p.Name) + index++;
                    cols.Add(string.Format("`{0}`=?{1}", p.Name, safep));
                    pars.Add(new MySqlParameter("?" + safep, v));
                }
            }
            safep = Extensions.SafeSQLVar(keyDefine.Name) + index++;
            pars.Add(new MySqlParameter("?" + safep, key.ChangeType(keyDefine.Type.PrimitiveKind())));

            return string.Format(cmdTemplate, entityType.Namespace, entityType.Name, string.Join(", ", cols), keyDefine.Name, safep);
        }

        protected override string BuildCreateCmd(IEdmEntityObject entity, List<DbParameter> pars)
        {
            var edmType = entity.GetEdmType();
            var entityType = edmType.Definition as EdmEntityType;
            string cmdTemplate = "insert into `{0}`.`{1}` ({2}) values ({3}); select LAST_INSERT_ID() ";
            List<string> cols = new List<string>();
            List<string> ps = new List<string>();
            object v = null;
            string safevar = string.Empty;
            int index = 0;
            foreach (var p in (entity as Delta).GetChangedPropertyNames())
            {
                entity.TryGetPropertyValue(p, out v);
                cols.Add(string.Format("`{0}`", p));
                safevar = Extensions.SafeSQLVar(p) + index;
                ps.Add("?" + safevar);
                pars.Add(new MySqlParameter("?" + safevar, v));
                index++;
            }
            return string.Format(cmdTemplate, entityType.Namespace, entityType.Name, string.Join(", ", cols), string.Join(", ", ps));
        }

        protected override string BuildDeleteCmd(string key, IEdmType elementType, List<DbParameter> pars)
        {
            var entityType = elementType as EdmEntityType;
            var keyDefine = entityType.DeclaredKey.First();
            return string.Format("delete `{0}`.`{1}`  where `{2}`=?{2}", entityType.Namespace, entityType.Name, keyDefine.Name);
        }
    }
}
