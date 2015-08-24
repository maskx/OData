using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.OData;
using System.Web.OData.Query;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Library;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Configuration;
using System.Data.SqlClient;
using Microsoft.OData.Core.UriParser.Semantic;

namespace maskx.OData.Sql
{
    public class SQLDataSource : IDataSource
    {
        #region inner type define
        class ParameterInfo
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
                    return SqlType2CsharpType(SqlDbType);
                }
            }
            public ParameterDirection Direction
            {
                get;
                set;
            }
        }
        #endregion

        #region members
        string ConnectionString;
        List<string> TVFList = new List<string>();
        Dictionary<string, Dictionary<string, ParameterInfo>> ParameterInfos = new Dictionary<string, Dictionary<string, ParameterInfo>>();
        #endregion

        #region construct
        public SQLDataSource(string name)
            : this(name, ConfigurationManager.ConnectionStrings[name].ConnectionString)
        {

        }
        public SQLDataSource(string name, string connectionString,
            string modelCommand = "GetEdmModelInfo",
            string funcCommand = "GetEdmFuncInfo",
            string tvfCommand = "GetEdmTVFInfo",
            string relationCommand = "GetEdmRelationship")
        {
            this.Name = name;
            this.ConnectionString = connectionString;
            _Model = new Lazy<EdmModel>(() =>
            {
                var model = new EdmModel();
                var container = new EdmEntityContainer("ns", "container");
                model.AddElement(container);
                AddEdmElement(modelCommand, model);
                AddEdmFunction(funcCommand, model);
                AddTableValueFunction(tvfCommand, model);
                BuildRelation(model, relationCommand);
                return model;

            });
        }
        #endregion

        #region method
        void AddEdmElement(string modelCommand, EdmModel model)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string tableName = string.Empty;
            EdmEntityType t = null;
            IEdmEntitySet edmSet = null;
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(modelCommand, (reader) =>
                {
                    tableName = reader["TABLE_NAME"].ToString();
                    if (t == null || t.Name != tableName)
                    {
                        edmSet = container.FindEntitySet(tableName);
                        if (edmSet == null)
                        {
                            t = new EdmEntityType("ns", tableName);
                            model.AddElement(t);
                            container.AddEntitySet(tableName, t);
                        }
                        else
                            t = edmSet.EntityType() as EdmEntityType;
                    }
                    var et = DBType2EdmType(reader["DATA_TYPE"].ToString());
                    if (et.HasValue)
                    {
                        string col = reader["COLUMN_NAME"].ToString();
                        EdmStructuralProperty key = t.AddStructuralProperty(col, et.Value, true);
                        if (col == reader["KEY_COLUMN_NAME"].ToString())
                        {
                            t.AddKeys(key);
                        }
                    }
                });
            }
        }
        IEdmTypeReference BuildSPReturnType(string spName, EdmModel model, Dictionary<string, IEdmTypeReference> outPars)
        {
            string spRtvTypeName = string.Format("{0}_RtvType", spName);
            var t = BuildSPReturnType(spName, model);
            model.AddElement((t.Definition as EdmCollectionType).ElementType.Definition as IEdmSchemaElement);

            if (outPars.Count == 0)
                return t;
            EdmComplexType root = new EdmComplexType("ns", spRtvTypeName);
            model.AddElement(root);
            foreach (var item in outPars)
            {
                root.AddStructuralProperty(item.Key, item.Value);
            }
            root.AddStructuralProperty("$Results", t);
            return new EdmComplexTypeReference(root, true);
        }
        IEdmTypeReference BuildSPReturnType(string spName, EdmModel model)
        {
            string querystring =
@"SELECT 
name as COLUMN_NAME
,TYPE_NAME(system_type_id) as DATA_TYPE
FROM sys.dm_exec_describe_first_result_set_for_object 
(
  OBJECT_ID('dbo.{0}'), 
  NULL
)";
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string spRtvTypeName = string.Format("{0}_RtvCollectionType", spName);
            EdmComplexType t = null;
            string getResultTypeCmd = querystring;
            t = new EdmComplexType("ns", spRtvTypeName);

            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(string.Format(getResultTypeCmd, spName), (reader) =>
                {
                    var et = DBType2EdmType(reader["DATA_TYPE"].ToString());
                    if (et.HasValue)
                    {
                        string col = reader["COLUMN_NAME"].ToString();
                        t.AddStructuralProperty(col, et.Value, true);
                    }
                }, null, CommandType.Text);
            }
            var etr = new EdmComplexTypeReference(t, true);
            return new EdmCollectionTypeReference(new EdmCollectionType(etr));
        }

        void AddEdmFunction(string spName,
            EdmModel model,
            Dictionary<string, IEdmTypeReference> pars,
            Dictionary<string, IEdmTypeReference> outPars)
        {
            IEdmTypeReference t = BuildSPReturnType(spName, model, outPars);
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            var func = new EdmFunction(container.Namespace, spName, t, false, null, false);
            foreach (var item in pars)
            {
                func.AddParameter(item.Key, item.Value);
            }
            container.AddFunctionImport(func.Name, func, null, true);
            model.AddElement(func);
            pars.Clear();
            outPars.Clear();
        }

        void AddEdmFunction(string funcCommand, EdmModel model)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            EdmFunction func = null;
            string currentName = string.Empty;
            Dictionary<string, IEdmTypeReference> pars = new Dictionary<string, IEdmTypeReference>();
            Dictionary<string, IEdmTypeReference> outPars = new Dictionary<string, IEdmTypeReference>();
            Dictionary<string, ParameterInfo> parsDic = new Dictionary<string, ParameterInfo>();
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(funcCommand, (reader) =>
                {
                    string spName = reader["SPECIFIC_NAME"].ToString();
                    if (currentName != spName)
                    {
                        if (!string.IsNullOrEmpty(currentName))
                        {
                            AddEdmFunction(currentName, model, pars, outPars);
                            this.ParameterInfos.Add(func.Name, parsDic);
                            parsDic = new Dictionary<string, ParameterInfo>();
                        }
                        currentName = spName;
                    }
                    if (!reader.IsDBNull("DATA_TYPE"))
                    {
                        var et = DBType2EdmType(reader["DATA_TYPE"].ToString());
                        if (et.HasValue)
                        {
                            var t = EdmCoreModel.Instance.GetPrimitive(et.Value, true);
                            var pname = reader["PARAMETER_NAME"].ToString().TrimStart('@');
                            pars.Add(pname, t);
                            parsDic.Add(pname, new ParameterInfo()
                            {
                                Name = pname,
                                SqlDbType = SqlTypeString2SqlType(reader["DATA_TYPE"].ToString()),
                                Length = reader.IsDBNull("MAX_LENGTH") ? 0 : (int)reader["MAX_LENGTH"],
                                Direction = reader["PARAMETER_MODE"].ToString() == "INOUT" ? ParameterDirection.Input : ParameterDirection.Output
                            });
                            if (reader["PARAMETER_MODE"].ToString() == "INOUT")
                            {
                                outPars.Add(pname, t);
                            }
                        }
                        else
                        {
                            //UDT
                            var t = BuildUDTType(reader["USER_DEFINED_TYPE_NAME"].ToString());
                            var pname = reader["PARAMETER_NAME"].ToString().TrimStart('@');
                            pars.Add(pname, t);
                            if (reader["PARAMETER_MODE"].ToString() == "INOUT")
                                outPars.Add(pname, t);
                        }
                    }
                });
                AddEdmFunction(currentName, model, pars, outPars);
                this.ParameterInfos.Add(func.Name, parsDic);
                parsDic = new Dictionary<string, ParameterInfo>();
            }
        }

        void AddTableValueFunction(string name, EdmModel model, Dictionary<string, IEdmTypeReference> pars)
        {
            var container = model.EntityContainer as EdmEntityContainer;
            var t = BuildTableValueType(name, model);
            var func = new EdmFunction(container.Namespace, name, t, false, null, true);
            foreach (var item in pars)
            {
                func.AddParameter(item.Key, item.Value);
            }
            container.AddFunctionImport(func.Name, func, null, true);
            model.AddElement(func);
            TVFList.Add(func.Name);
            pars.Clear();
        }
        void AddTableValueFunction(string TVFCommand, EdmModel model)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string currentName = string.Empty;
            string funcName = string.Empty;
            Dictionary<string, IEdmTypeReference> pars = new Dictionary<string, IEdmTypeReference>();

            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(TVFCommand, (reader) =>
                {
                    funcName = reader["SPECIFIC_NAME"].ToString();
                    if (currentName != funcName)
                    {
                        if (!string.IsNullOrEmpty(currentName))
                        {
                            AddTableValueFunction(currentName, model, pars);
                        }
                        currentName = funcName;
                    }
                    if (!reader.IsDBNull("DATA_TYPE"))
                    {
                        var et = DBType2EdmType(reader["DATA_TYPE"].ToString());
                        if (et.HasValue)
                        {
                            var t = EdmCoreModel.Instance.GetPrimitive(et.Value, true);
                            var pname = reader["PARAMETER_NAME"].ToString().TrimStart('@');
                            pars.Add(pname, t);
                        }
                    }
                });
                AddTableValueFunction(currentName, model, pars);
            }
        }

        IEdmTypeReference BuildTableValueType(string name, EdmModel model)
        {
            string querystring =
@"select
	col.name as COLUMN_NAME
	,type_name(col.user_type_id) as DATA_TYPE
from sys.all_columns as col
where col.object_id=object_id(N'{0}')";
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string spRtvTypeName = string.Format("{0}_RtvCollectionType", name);
            EdmComplexType t = null;
            string getResultTypeCmd = querystring;
            t = new EdmComplexType("ns", spRtvTypeName);

            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(string.Format(getResultTypeCmd, name), (reader) =>
                {
                    var et = DBType2EdmType(reader["DATA_TYPE"].ToString());
                    if (et.HasValue)
                    {
                        string col = reader["COLUMN_NAME"].ToString();
                        t.AddStructuralProperty(col, et.Value, true);
                    }
                }, null, CommandType.Text);
            }
            var etr = new EdmComplexTypeReference(t, true);
            return new EdmCollectionTypeReference(new EdmCollectionType(etr));
        }

        void BuildRelation(EdmModel model, string cmd)
        {
            string parentName = string.Empty;
            string refrenceName = string.Empty;
            string parentColName = string.Empty;
            string refrenceColName = string.Empty;
            EdmEntityType parent = null;
            EdmEntityType refrence = null;
            EdmNavigationPropertyInfo parentNav = null;
            EdmNavigationPropertyInfo refrenceNav = null;
            List<IEdmStructuralProperty> principalProperties = null;
            List<IEdmStructuralProperty> dependentProperties = null;

            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(cmd, (reader) =>
                {
                    if (parentName != reader["ParentName"].ToString() || refrenceName != reader["RefrencedName"].ToString())
                    {
                        if (!string.IsNullOrEmpty(refrenceName))
                        {
                            refrenceNav.PrincipalProperties = principalProperties;
                            refrenceNav.DependentProperties = dependentProperties;
                            var np = parent.AddBidirectionalNavigation(refrenceNav, parentNav);
                            var parentSet = model.EntityContainer.FindEntitySet(parentName) as EdmEntitySet;
                            var referenceSet = model.EntityContainer.FindEntitySet(refrenceName) as EdmEntitySet;
                            parentSet.AddNavigationTarget(np, referenceSet);
                        }
                        parentName = reader["ParentName"].ToString();
                        refrenceName = reader["RefrencedName"].ToString();
                        parent = model.FindDeclaredType(string.Format("ns.{0}", parentName)) as EdmEntityType;
                        refrence = model.FindDeclaredType(string.Format("ns.{0}", refrenceName)) as EdmEntityType;
                        parentNav = new EdmNavigationPropertyInfo();
                        parentNav.Name = parentName;
                        parentNav.TargetMultiplicity = EdmMultiplicity.ZeroOrOne;
                        refrenceNav = new EdmNavigationPropertyInfo();
                        refrenceNav.Name = refrenceName;
                        refrenceNav.TargetMultiplicity = EdmMultiplicity.Many;
                        refrenceNav.Target = refrence;
                        principalProperties = new List<IEdmStructuralProperty>();
                        dependentProperties = new List<IEdmStructuralProperty>();
                    }
                    principalProperties.Add(parent.FindProperty(reader["ParentColumnName"].ToString()) as IEdmStructuralProperty);
                    dependentProperties.Add(refrence.FindProperty(reader["RefreancedColumnName"].ToString()) as IEdmStructuralProperty);
                }, null, CommandType.Text);
                refrenceNav.PrincipalProperties = principalProperties;
                refrenceNav.DependentProperties = dependentProperties;

                var np1 = parent.AddBidirectionalNavigation(refrenceNav, parentNav);
                var parentSet1 = model.EntityContainer.FindEntitySet(parentName) as EdmEntitySet;
                var referenceSet1 = model.EntityContainer.FindEntitySet(refrenceName) as EdmEntitySet;
                parentSet1.AddNavigationTarget(np1, referenceSet1);
            }
        }
        EdmComplexTypeReference BuildUDTType(string name)
        {
            EdmComplexType root = new EdmComplexType("ns", name);
            string cmdText =
@"select  
	c.name
	,type_name(c.user_type_id) as ColumnType
	,c.max_length as ColumnLength
	,c.is_nullable as ColumnIsNullable
from sys.table_types tt
	inner join sys.columns c on c.object_id = tt.type_table_object_id
where tt.name =@name";
            string cNmae = string.Empty;

            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(cmdText, (reader) =>
                {
                    var et = DBType2EdmType(reader["ColumnType"].ToString());
                    if (et.HasValue)
                    {
                        cNmae = reader["name"].ToString();
                        root.AddStructuralProperty(cNmae, et.Value);
                    }

                }, (pars) =>
                {
                    pars.AddWithValue("name", name);
                }, CommandType.Text);
            }
            return new EdmComplexTypeReference(root, true);
        }
        static Type SqlType2CsharpType(SqlDbType sqlType)
        {
            switch (sqlType)
            {
                case SqlDbType.BigInt:
                    return typeof(Int64);
                case SqlDbType.Binary:
                    return typeof(Object);
                case SqlDbType.Bit:
                    return typeof(Boolean);
                case SqlDbType.Char:
                    return typeof(String);
                case SqlDbType.DateTime:
                    return typeof(DateTime);
                case SqlDbType.Decimal:
                    return typeof(Decimal);
                case SqlDbType.Float:
                    return typeof(Double);
                case SqlDbType.Image:
                    return typeof(Object);
                case SqlDbType.Int:
                    return typeof(Int32);
                case SqlDbType.Money:
                    return typeof(Decimal);
                case SqlDbType.NChar:
                    return typeof(String);
                case SqlDbType.NText:
                    return typeof(String);
                case SqlDbType.NVarChar:
                    return typeof(String);
                case SqlDbType.Real:
                    return typeof(Single);
                case SqlDbType.SmallDateTime:
                    return typeof(DateTime);
                case SqlDbType.SmallInt:
                    return typeof(Int16);
                case SqlDbType.SmallMoney:
                    return typeof(Decimal);
                case SqlDbType.Text:
                    return typeof(String);
                case SqlDbType.Timestamp:
                    return typeof(Object);
                case SqlDbType.TinyInt:
                    return typeof(Byte);
                case SqlDbType.Udt:
                    return typeof(Object);
                case SqlDbType.UniqueIdentifier:
                    return typeof(Guid);
                case SqlDbType.VarBinary:
                    return typeof(Object);
                case SqlDbType.VarChar:
                    return typeof(String);
                case SqlDbType.Variant:
                    return typeof(Object);
                case SqlDbType.Xml:
                    return typeof(Object);
                default:
                    return null;
            }
        }
        static EdmPrimitiveTypeKind? DBType2EdmType(string dbType)
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
        static SqlDbType SqlTypeString2SqlType(string sqlTypeString)
        {
            SqlDbType dbType = SqlDbType.Variant;//默认为Object

            switch (sqlTypeString)
            {
                case "int":
                    dbType = SqlDbType.Int;
                    break;
                case "varchar":
                    dbType = SqlDbType.VarChar;
                    break;
                case "bit":
                    dbType = SqlDbType.Bit;
                    break;
                case "datetime":
                    dbType = SqlDbType.DateTime;
                    break;
                case "decimal":
                    dbType = SqlDbType.Decimal;
                    break;
                case "float":
                    dbType = SqlDbType.Float;
                    break;
                case "image":
                    dbType = SqlDbType.Image;
                    break;
                case "money":
                    dbType = SqlDbType.Money;
                    break;
                case "ntext":
                    dbType = SqlDbType.NText;
                    break;
                case "nvarchar":
                    dbType = SqlDbType.NVarChar;
                    break;
                case "smalldatetime":
                    dbType = SqlDbType.SmallDateTime;
                    break;
                case "smallint":
                    dbType = SqlDbType.SmallInt;
                    break;
                case "text":
                    dbType = SqlDbType.Text;
                    break;
                case "bigint":
                    dbType = SqlDbType.BigInt;
                    break;
                case "binary":
                    dbType = SqlDbType.Binary;
                    break;
                case "char":
                    dbType = SqlDbType.Char;
                    break;
                case "nchar":
                    dbType = SqlDbType.NChar;
                    break;
                case "numeric":
                    dbType = SqlDbType.Decimal;
                    break;
                case "real":
                    dbType = SqlDbType.Real;
                    break;
                case "smallmoney":
                    dbType = SqlDbType.SmallMoney;
                    break;
                case "sql_variant":
                    dbType = SqlDbType.Variant;
                    break;
                case "timestamp":
                    dbType = SqlDbType.Timestamp;
                    break;
                case "tinyint":
                    dbType = SqlDbType.TinyInt;
                    break;
                case "uniqueidentifier":
                    dbType = SqlDbType.UniqueIdentifier;
                    break;
                case "varbinary":
                    dbType = SqlDbType.VarBinary;
                    break;
                case "xml":
                    dbType = SqlDbType.Xml;
                    break;
            }
            return dbType;
        }
        static Type EdmType2ClrType(EdmPrimitiveTypeKind kind)
        {
            switch (kind)
            {
                case EdmPrimitiveTypeKind.Binary:
                    break;
                case EdmPrimitiveTypeKind.Boolean:
                    return typeof(bool);
                case EdmPrimitiveTypeKind.Byte:
                    return typeof(Byte);
                case EdmPrimitiveTypeKind.Date:
                    break;
                case EdmPrimitiveTypeKind.DateTimeOffset:
                    return typeof(DateTime);
                case EdmPrimitiveTypeKind.Decimal:
                    return typeof(decimal);
                case EdmPrimitiveTypeKind.Double:
                    return typeof(double);
                case EdmPrimitiveTypeKind.Duration:
                    break;
                case EdmPrimitiveTypeKind.Geography:
                    break;
                case EdmPrimitiveTypeKind.GeographyCollection:
                    break;
                case EdmPrimitiveTypeKind.GeographyLineString:
                    break;
                case EdmPrimitiveTypeKind.GeographyMultiLineString:
                    break;
                case EdmPrimitiveTypeKind.GeographyMultiPoint:
                    break;
                case EdmPrimitiveTypeKind.GeographyMultiPolygon:
                    break;
                case EdmPrimitiveTypeKind.GeographyPoint:
                    break;
                case EdmPrimitiveTypeKind.GeographyPolygon:
                    break;
                case EdmPrimitiveTypeKind.Geometry:
                    break;
                case EdmPrimitiveTypeKind.GeometryCollection:
                    break;
                case EdmPrimitiveTypeKind.GeometryLineString:
                    break;
                case EdmPrimitiveTypeKind.GeometryMultiLineString:
                    break;
                case EdmPrimitiveTypeKind.GeometryMultiPoint:
                    break;
                case EdmPrimitiveTypeKind.GeometryMultiPolygon:
                    break;
                case EdmPrimitiveTypeKind.GeometryPoint:
                    break;
                case EdmPrimitiveTypeKind.GeometryPolygon:
                    break;
                case EdmPrimitiveTypeKind.Guid:
                    return typeof(Guid);
                case EdmPrimitiveTypeKind.Int16:
                    return typeof(Int16);
                case EdmPrimitiveTypeKind.Int32:
                    return typeof(Int32);
                case EdmPrimitiveTypeKind.Int64:
                    return typeof(Int64);
                case EdmPrimitiveTypeKind.None:
                    break;
                case EdmPrimitiveTypeKind.SByte:
                    break;
                case EdmPrimitiveTypeKind.Single:
                    break;
                case EdmPrimitiveTypeKind.Stream:
                    break;
                case EdmPrimitiveTypeKind.String:
                    return typeof(string);
                case EdmPrimitiveTypeKind.TimeOfDay:
                    break;
                default:
                    break;
            }
            return typeof(object);
        }
        static string BuildSqlQueryCmd(ODataQueryOptions options, string target = "")
        {
            var cxt = options.Context;
            string table = target;
            if (string.IsNullOrEmpty(target))
                table = string.Format("[{0}]", cxt.Path.Segments[0].ToString());
            string cmdSql = "select {0} {1} from {2} {3} {4} {5} {6}";
            string top = string.Empty;
            string skip = string.Empty;
            string fetch = string.Empty;

            if (options.Count == null && options.Top != null)
            {
                if (options.Skip != null)
                {
                    skip = string.Format("OFFSET {0} ROWS", options.Skip.RawValue); ;
                    fetch = string.Format("FETCH NEXT {0} ROWS ONLY", options.Top.RawValue);
                    top = string.Empty;
                }
                else
                    top = "top " + options.Top.RawValue;
            }
            var cmdtxt = string.Format(cmdSql
                , top
                , options.ParseSelect()
                , table
                , options.ParseWhere()
                , options.ParseOrderBy()
                , skip
                , fetch);
            return cmdtxt;
        }
        string BuildSqlQueryCmd(ExpandedNavigationSelectItem expanded, string condition)
        {
            string table = string.Format("[{0}]", expanded.NavigationSource.Name);
            string cmdSql = "select {0} {1} from {2} {3} {4} {5} {6}";
            string top = string.Empty;
            string skip = string.Empty;
            string fetch = string.Empty;

            if (!expanded.CountOption.HasValue && expanded.TopOption.HasValue)
            {
                if (expanded.SkipOption.HasValue)
                {
                    skip = string.Format("OFFSET {0} ROWS", expanded.SkipOption.Value); ;
                    fetch = string.Format("FETCH NEXT {0} ROWS ONLY", expanded.TopOption.Value);
                    top = string.Empty;
                }
                else
                    top = "top " + expanded.TopOption.Value;
            }

            var cmdtxt = string.Format(cmdSql
                , top
                , expanded.ParseSelect()
                , table
                , expanded.ParseWhere(condition, this.Model)
                , expanded.ParseOrderBy()
                , skip
                , fetch);
            return cmdtxt;
        }
        EdmEntityObjectCollection Get(IEdmCollectionType edmType, string sqlCmd, List<ExpandedNavigationSelectItem> expands = null)
        {
            var entityType = edmType.ElementType.AsEntity();
            EdmEntityObjectCollection collection = new EdmEntityObjectCollection(new EdmCollectionTypeReference(edmType));
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(sqlCmd, (reader) =>
                {
                    EdmEntityObject entity = new EdmEntityObject(entityType);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        reader.SetEntityPropertyValue(i, entity);
                    }
                    if (expands != null)
                    {
                        foreach (var expanded in expands)
                        {
                            List<string> condition = new List<string>();
                            string w = "{0}={1}";
                            foreach (NavigationPropertySegment item in expanded.PathToNavigationProperty)
                            {
                                foreach (var p in item.NavigationProperty.ReferentialConstraint.PropertyPairs)
                                {
                                    var b = EdmType2ClrType(p.PrincipalProperty.Type.PrimitiveKind());
                                    var v = reader[p.PrincipalProperty.Name].ToString();
                                    condition.Add(string.Format(w, p.DependentProperty.Name, v));
                                }
                            }
                            var ss = Get(expanded.NavigationSource.Type as IEdmCollectionType, BuildSqlQueryCmd(expanded, string.Join(" and ", condition)));
                            object obj;
                            var dd = entity.TryGetPropertyValue(expanded.NavigationSource.Name, out obj);
                            bool t = entity.TrySetPropertyValue(expanded.NavigationSource.Name, 1231);
                        }
                    }
                    collection.Add(entity);

                }, null, CommandType.Text);
            }
            return collection;
        }
        #endregion

        #region IDataSource
        Lazy<EdmModel> _Model;
        public EdmModel Model
        {
            get
            {
                return _Model.Value;
            }
        }

        public string Name
        {
            get;
            private set;
        }

        public string Create(IEdmEntityObject entity)
        {
            var edmType = entity.GetEdmType();
            var table = (edmType.Definition as EdmEntityType).Name;
            object rtv = null;
            string cmdTemplate = "insert into [{0}] ({1}) values ({2}) select SCOPE_IDENTITY() ";
            List<string> cols = new List<string>();
            List<string> pars = new List<string>();
            List<SqlParameter> sqlpars = new List<SqlParameter>();
            object v = null;
            foreach (var p in (entity as Delta).GetChangedPropertyNames())
            {
                entity.TryGetPropertyValue(p, out v);
                cols.Add(string.Format("[{0}]", p));
                pars.Add("@" + p);
                sqlpars.Add(new SqlParameter("@" + p, v));
            }
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteScalar(string.Format(cmdTemplate, table, string.Join(", ", cols), string.Join(", ", pars))
                    , (dbpars) =>
                    {
                        dbpars.AddRange(sqlpars.ToArray());
                    }, CommandType.Text);
            }
            return rtv.ToString();
        }

        public int Delete(string key, IEdmType elementType)
        {
            var entityType = elementType as EdmEntityType;
            var keyName = entityType.DeclaredKey.First().Name;
            int rtv = 0;
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteNonQuery(string.Format("delete {0}  where [{1}]=@{1}", entityType.Name, keyName)
                      , (dbpars) =>
                      {
                          dbpars.AddWithValue("@" + keyName, key);
                      }, CommandType.Text);
            }
            return rtv;
        }

        public EdmEntityObjectCollection Get(ODataQueryOptions queryOptions)
        {
            var edmType = queryOptions.Context.Path.GetEdmType() as IEdmCollectionType;
            var entityType = (edmType as IEdmCollectionType).ElementType.AsEntity();
            List<ExpandedNavigationSelectItem> expands = new List<ExpandedNavigationSelectItem>();
            if (queryOptions.SelectExpand != null)
            {
                foreach (var item in queryOptions.SelectExpand.SelectExpandClause.SelectedItems)
                {
                    var expande = item as ExpandedNavigationSelectItem;
                    if (expande == null)
                        continue;
                    expands.Add(expande);
                }
            }
            return Get(edmType, BuildSqlQueryCmd(queryOptions), expands);
        }

        public EdmEntityObject Get(string key, ODataQueryOptions queryOptions)
        {
            var cxt = queryOptions.Context;
            var entityType = cxt.ElementType as EdmEntityType;
            var keyName = entityType.DeclaredKey.First().Name;
            string cmdSql = "select {0} from [{1}] where [{2}]={3}";
            var cmdTxt = string.Format(cmdSql
                , queryOptions.ParseSelect()
                , cxt.Path.Segments[0].ToString()
                , keyName
                , key);
            EdmEntityObject entity = new EdmEntityObject(entityType);
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(cmdTxt, (reader) =>
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        reader.SetEntityPropertyValue(i, entity);
                    }
                }, null, CommandType.Text);
            }
            return entity;
        }

        public int GetCount(ODataQueryOptions queryOptions)
        {
            object rtv = null;
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteScalar(BuildSqlQueryCmd(queryOptions), null, CommandType.Text);
            }
            if (rtv == null)
                return 0;
            return (int)rtv;
        }

        public int GetFuncResultCount(IEdmFunction action, JObject parameterValues, ODataQueryOptions queryOptions)
        {
            throw new NotImplementedException();
        }

        public IEdmObject InvokeFunction(IEdmFunction action, JObject parameterValues, ODataQueryOptions queryOptions = null)
        {
            throw new NotImplementedException();
        }

        public int Merge(string key, IEdmEntityObject entity)
        {
            string cmdTemplate = "update [{0}] set {1} where {2} ";
            var entityType = entity.GetEdmType().Definition as EdmEntityType;
            var keyName = entityType.DeclaredKey.First().Name;
            List<string> cols = new List<string>();
            List<string> pars = new List<string>();
            List<SqlParameter> sqlpars = new List<SqlParameter>();
            object v = null;
            foreach (var p in (entity as Delta).GetChangedPropertyNames())
            {
                entity.TryGetPropertyValue(p, out v);
                cols.Add(string.Format("[{0}]", p));
                pars.Add("@" + p);
                sqlpars.Add(new SqlParameter("@" + p, v));
            }
            int rtv;
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteNonQuery(string.Format(cmdTemplate, entityType.Name, string.Join(", ", cols), string.Join(", ", pars))
                    , (dbpars) =>
                    {
                        dbpars.AddRange(sqlpars.ToArray());
                    }, CommandType.Text);
            }
            return rtv;
        }

        public int Replace(string key, IEdmEntityObject entity)
        {
            string cmdTemplate = "update [{0}] set {1} where {2} ";
            var entityType = entity.GetEdmType().Definition as EdmEntityType;
            var keyName = entityType.DeclaredKey.First().Name;
            List<string> cols = new List<string>();
            List<string> pars = new List<string>();
            List<SqlParameter> sqlpars = new List<SqlParameter>();
            object v = null;

            foreach (var p in entityType.Properties())
            {
                entity.TryGetPropertyValue(p.Name, out v);
                cols.Add(string.Format("[{0}]", p.Name));
                pars.Add("@" + p.Name);
                sqlpars.Add(new SqlParameter("@" + p.Name, v == null ? DBNull.Value : v));
            }
            int rtv;
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteNonQuery(string.Format(cmdTemplate, entityType.Name, string.Join(", ", cols), string.Join(", ", pars))
                    , (dbpars) =>
                    {
                        dbpars.AddRange(sqlpars.ToArray());
                    }, CommandType.Text);
            }
            return rtv;
        }

        #endregion
    }
}
