using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using maskx.Database;
using maskx.OData.SQLSource;
using Microsoft.AspNet.OData.Query;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

namespace maskx.OData.SQLSource
{
    public class SQLServer : SQLBase
    {
        int SQLVersion = 11;
        public SQLServer(string name, string connectionString) : base(name, connectionString)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                Version.TryParse(conn.ServerVersion, out Version v);
                SQLVersion = v.Major;
            }

        }

        SQLServerDbUtility _SQLServerDbUtility = new SQLServerDbUtility();
        protected override DbUtility _DbUtility { get { return _SQLServerDbUtility; } }

        protected override DbAccess CreateDbAccess(string connectionString)
        {
            return new DbAccess(System.Data.SqlClient.SqlClientFactory.Instance, connectionString);
        }

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

        protected override IEnumerable<(
            string SchemaName,
            string FunctionName,
            string ParameterName,
            string ParameterDataType,
            string UserDefinedTypeSchema,
            string UserDefinedTypeName,
            int MaxLength,
            int NumericScale)>
            GetFunctions()
        {
            String cmdtxt = @"
select
		obj.name as [FUNCTION_NAME]
		,[s].name as [SCHEMA_NAME]
		,par.name as PARAMETER_NAME
		,type_name(par.user_type_id) as DATA_TYPE
		,par.max_length as MAX_LENGTH
		,par.scale as SCALE
	from sys.all_objects as obj
		INNER JOIN sys.all_parameters  as par on par.[object_id]=obj.[object_id] 
		INNER JOIN [sys].[schemas] as [s] on [s].[schema_id]=[obj].[schema_id]
	where obj.[type] = N'IF' and obj.is_ms_shipped=0
	order by obj.object_id
";
            using (var conn = new SqlConnection(this.ConnectionString))
            {
                SqlCommand cmd = new SqlCommand(cmdtxt, conn);
                conn.Open();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    yield return (reader["SCHEMA_NAME"].ToString(),
                                       reader["FUNCTION_NAME"].ToString(),
                                        reader["PARAMETER_NAME"].ToString(),
                                        reader["DATA_TYPE"].ToString(),
                                        string.Empty,
                                        string.Empty,
                                        0,
                                        0);
                }
                conn.Close();
            }
        }

        protected override IEnumerable<(
            string ForeignKeyName,
            string ParentSchemaName,
            string ParentName,
            string ParentColumnName,
            string RefrencedName,
            string RefrencedSchemaName,
            string RefrencedColumnName)>
            GetRelationship()
        {
            String cmdtxt = @"
SELECT
		fk.[name] as [FK_NAME],
		tp.[name] as [PARENT_NAME],
		[st].[name] as [PARENT_SCHEMA_NAME],
		cp.[name] [PARENT_COLUMN_NAME],
		tr.[name] [REFRENCED_NAME],
		[sr].[name] as [REFRENCED_SCHEMA_NAME],
		cr.[name] as [REFREANCED_COLUMN_NAME]
	FROM   sys.foreign_keys fk
	INNER JOIN sys.tables tp ON fk.parent_object_id = tp.[object_id]
	INNER JOIN [sys].[schemas] as [st] on [st].[schema_id]=[tp].[schema_id] 
	INNER JOIN sys.tables tr ON fk.referenced_object_id = tr.[object_id]
	inner join [sys].[schemas] as [sr] on [sr].[schema_id]=[tr].[schema_id] 
	INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
	INNER JOIN sys.columns cp ON fkc.parent_column_id = cp.column_id AND fkc.parent_object_id = cp.object_id
	INNER JOIN sys.columns cr ON fkc.referenced_column_id = cr.column_id AND fkc.referenced_object_id = cr.object_id
	ORDER BY
		tp.[name], cp.column_id
";
            using (var conn = new SqlConnection(this.ConnectionString))
            {
                SqlCommand cmd = new SqlCommand(cmdtxt, conn);
                conn.Open();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    yield return (reader["FK_NAME"].ToString(),
                                       reader["PARENT_SCHEMA_NAME"].ToString(),
                                        reader["PARENT_NAME"].ToString(),
                                        reader["PARENT_COLUMN_NAME"].ToString(),
                                        reader["REFRENCED_NAME"].ToString(),
                                         reader["REFRENCED_SCHEMA_NAME"].ToString(),
                                          reader["REFREANCED_COLUMN_NAME"].ToString());
                }
                conn.Close();
            }
        }

        protected override IEnumerable<(
            string SchemaName,
            string StoredProcedureName,
            string ParameterName,
            string ParameterDataType,
            string ParemeterMode,
            string UserDefinedTypeSchema,
            string UserDefinedTypeName,
            int MaxLength,
            int NumericScale)>
            GetStoredProcedures()
        {
            String cmdtxt = @"
select
		p.[name] as ROUTINE_NAME
		,[s].[name] as ROUTINE_SCHEMA
		,par.PARAMETER_NAME
		,par.PARAMETER_MODE
		,par.DATA_TYPE
		,par.USER_DEFINED_TYPE_NAME
		,par.USER_DEFINED_TYPE_SCHEMA
		,isnull(par.CHARACTER_MAXIMUM_LENGTH,par.NUMERIC_PRECISION) as CHARACTER_MAXIMUM_LENGTH
		,par.NUMERIC_SCALE as NUMERIC_SCALE
	from sys.procedures as p
		JOIN [sys].[schemas] as [s] on [s].[schema_id]=[p].[schema_id]
		LEFT JOIN INFORMATION_SCHEMA.PARAMETERS as par on par.SPECIFIC_NAME=p.[name]
	order by p.[name]
";
            using (SqlConnection conn = new SqlConnection(this.ConnectionString))
            {

                SqlCommand cmd = new SqlCommand(cmdtxt, conn);
                conn.Open();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    yield return (reader["ROUTINE_SCHEMA"].ToString(),
                                       reader["ROUTINE_NAME"].ToString(),
                                       reader.IsDBNull("PARAMETER_NAME") ? string.Empty : reader["PARAMETER_NAME"].ToString(),
                                       reader.IsDBNull("DATA_TYPE") ? string.Empty : reader["DATA_TYPE"].ToString(),
                                       reader.IsDBNull("PARAMETER_MODE") ? string.Empty : reader["PARAMETER_MODE"].ToString(),
                                       reader.IsDBNull("USER_DEFINED_TYPE_SCHEMA") ? string.Empty : reader["USER_DEFINED_TYPE_SCHEMA"].ToString(),
                                       reader.IsDBNull("USER_DEFINED_TYPE_NAME") ? string.Empty : reader["USER_DEFINED_TYPE_NAME"].ToString(),
                                       reader.IsDBNull("CHARACTER_MAXIMUM_LENGTH") ? 0 : (Int32)reader["CHARACTER_MAXIMUM_LENGTH"],
                                       reader.IsDBNull("NUMERIC_SCALE") ? 0 : (Int32)reader["NUMERIC_SCALE"]);
                }
                conn.Close();
            }
        }

        protected override IEnumerable<(
            string SchemaName,
            string TableName,
            string ColumnName,
            string DataType,
            bool isKey)> GetTables()
        {
            String cmdtxt = @"with pk as (
		SELECT
			[i].[object_id],
			COL_NAME([ic].[object_id], [ic].[column_id]) AS [column_name]
		FROM [sys].[indexes] AS [i]
			JOIN [sys].[tables] AS [t] ON [i].[object_id] = [t].[object_id]
			JOIN [sys].[index_columns] AS [ic] ON [i].[object_id] = [ic].[object_id] AND [i].[index_id] = [ic].[index_id]
		where [i].[is_primary_key]=1
	)
	SELECT
		[t].[name] AS [TABLE_NAME],
		[s].[name] as [SCHEMA_NAME],
		[c].[name] AS [COLUMN_NAME],
		[tp].[name] AS [DATA_TYPE],
		[c].[IS_NULLABLE],
		pk.column_name as [COLUMN_KEY],
		CAST([c].[max_length] AS int) AS [MAX_LENGTH],
		CAST([c].[precision] AS int) AS [PRECISION],
		CAST([c].[scale] AS int) AS [SCALE],
		[c].[IS_IDENTITY]
	FROM [sys].[columns] AS [c]
		JOIN [sys].[tables] AS [t] ON [c].[object_id] = [t].[object_id]
		JOIN [sys].[types] AS [tp] ON [c].[user_type_id] = [tp].[user_type_id]
		JOIN [sys].[schemas] as [s] on [s].[schema_id]=[t].[schema_id]
		LEFT JOIN pk on pk.[object_id]=t.[object_id] and pk.column_name=c.[name]
	order by [TABLE_NAME],[SCHEMA_NAME]
   ";
            using (SqlConnection conn = new SqlConnection(this.ConnectionString))
            {
                SqlCommand cmd = new SqlCommand(cmdtxt, conn);
                conn.Open();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    yield return (reader["SCHEMA_NAME"].ToString(),
                                       reader["TABLE_NAME"].ToString(),
                                        reader["COLUMN_NAME"].ToString(),
                                        reader["DATA_TYPE"].ToString(),
                                        !reader.IsDBNull("COLUMN_KEY"));
                }
                conn.Close();
            }
        }

        protected override IEnumerable<(
            string ColumnName,
            string DataType,
            int Length,
            bool isNullable)>
            GetTableValueType(string schema, string name)
        {
            String cmdtxt = @"
    select
		col.name as COLUMN_NAME
		,type_name(col.user_type_id) as DATA_TYPE
        ,MAX_LENGTH AS COLUMN_LENGTH
        ,IS_NULLABLE
	from sys.all_columns as col
	where col.object_id=object_id(@NAME)
";
            using (SqlConnection conn = new SqlConnection(this.ConnectionString))
            {
                SqlCommand cmd = new SqlCommand(cmdtxt, conn);
                cmd.Parameters.Add(new SqlParameter("@NAME", string.Format("{0}.{1}", schema, name)));
                conn.Open();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    yield return (reader["COLUMN_NAME"].ToString(),
                                        reader["DATA_TYPE"].ToString(),
                                        (Int16)reader["COLUMN_LENGTH"],
                                        (bool)reader["IS_NULLABLE"]
                                       );
                }
                conn.Close();
            }
        }

        protected override IEnumerable<(
            string ColumnName,
            string DataType,
            int Length,
            bool isNullable)>
            GetUserDefinedType(string schema, string name)
        {
            String cmdtxt = @"
select  
		c.[name] as [COLUMN_NAME]
		,s.[name] as [SCHEMA_NAME]
		,type_name(c.user_type_id) as [DATA_TYPE]
		,c.max_length as [COLUMN_LENGTH]
		,c.is_nullable as [IS_NULLABLE]
	from sys.table_types tt
		INNER JOIN sys.columns c on c.object_id = tt.type_table_object_id
		INNER JOIN [sys].[schemas] as [s] on [s].[schema_id]=[tt].[schema_id]
	where tt.[name] =@NAME
		and s.[name]=@SCHEMA_NAME
   ";
            using (SqlConnection conn = new SqlConnection(this.ConnectionString))
            {
                SqlCommand cmd = new SqlCommand(cmdtxt, conn);
                cmd.Parameters.Add(new SqlParameter("@NAME", name));
                cmd.Parameters.Add(new SqlParameter("@SCHEMA_NAME", schema));
                conn.Open();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    yield return (reader["COLUMN_NAME"].ToString(),
                                        reader["DATA_TYPE"].ToString(),
                                        (Int16)reader["COLUMN_LENGTH"],
                                        (bool)reader["IS_NULLABLE"]
                                       );
                }
                conn.Close();
            }
        }

        protected override IEnumerable<(
            string SchemaName,
            string ViewName,
            string ColumnName,
            string DataType,
            bool isKey)> GetViews()
        {
            String cmdtxt = @"with pk as (
		SELECT
			[i].[object_id],
			COL_NAME([ic].[object_id], [ic].[column_id]) AS [column_name]
		FROM [sys].[indexes] AS [i]
			JOIN [sys].[tables] AS [t] ON [i].[object_id] = [t].[object_id]
			JOIN [sys].[index_columns] AS [ic] ON [i].[object_id] = [ic].[object_id] AND [i].[index_id] = [ic].[index_id]
		where [i].[is_primary_key]=1
	)
	SELECT
		[t].[name] AS [TABLE_NAME],
		[s].[name] as [SCHEMA_NAME],
		[c].[name] AS [COLUMN_NAME],
		[tp].[name] AS [DATA_TYPE],
		[c].[IS_NULLABLE],
		null as [COLUMN_KEY],
		CAST([c].[max_length] AS int) AS [MAX_LENGTH],
		CAST([c].[precision] AS int) AS [PRECISION],
		CAST([c].[scale] AS int) AS [SCALE],
		[c].[IS_IDENTITY]
	FROM [sys].[columns] AS [c]
		JOIN [sys].[views] AS [t] ON [c].[object_id] = [t].[object_id]
		JOIN [sys].[types] AS [tp] ON [c].[user_type_id] = [tp].[user_type_id]
		JOIN [sys].[schemas] as [s] on [s].[schema_id]=[t].[schema_id]
	order by [TABLE_NAME],[SCHEMA_NAME]
   ";
            using (SqlConnection conn = new SqlConnection(this.ConnectionString))
            {
                SqlCommand cmd = new SqlCommand(cmdtxt, conn);
                conn.Open();
                var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    yield return (reader["SCHEMA_NAME"].ToString(),
                                       reader["TABLE_NAME"].ToString(),
                                        reader["COLUMN_NAME"].ToString(),
                                        reader["DATA_TYPE"].ToString(),
                                        !reader.IsDBNull("COLUMN_KEY"));
                }
                conn.Close();
            }
        }
        protected override string QueryPagingCommandTemplete
        {
            //https://support.microsoft.com/en-us/help/321185/how-to-determine-the-version-edition-and-update-level-of-sql-server-an
            get
            {
                if (SQLVersion >= 11)//2012
                    return "select {1} from {2}.{3} {4} order by {5} OFFSET {6} rows FETCH NEXT {0} rows only";
                if (SQLVersion >= 9)//2005
                    return @"
select top {0} t.* from(
select ROW_NUMBER() over ( order by {5}) as rowIndex,{1} from {2}.{3}
) as t
where t.rowIndex > {6}";
                //low version not supported
                return base.QueryPagingCommandTemplete;
            }
        }
    }
}
