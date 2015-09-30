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
        /// <summary>
        /// Get table/view schema
        /// </summary>
        public string ModelCommand { get; private set; }
        /// <summary>
        /// get stored procedures schema.(stored procedure name and parameters)
        /// </summary>
        public string FuncCommand { get; private set; }
        /// <summary>
        /// get the table-valued function information
        /// </summary>
        public string TableValuedCommand { get; private set; }
        /// <summary>
        /// get the relations of table/view
        /// </summary>
        public string RelationCommand { get; private set; }
        /// <summary>
        /// get the first result set of stored procedures
        /// </summary>
        public string StoredProcedureResultSetCommand { get; private set; }
        /// <summary>
        /// get table valued function result set
        /// </summary>
        public string TableValuedResultSetCommand { get; private set; }
        public string UserDefinedTableCommand { get; private set; }
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
            string funcCommand = "GetEdmSPInfo",
            string tvfCommand = "GetEdmTVFInfo",
            string relationCommand = "GetEdmRelationship",
            string storedProcedureResultSetCommand = "GetEdmSPResultSet",
            string userDefinedTableCommand = "GetEdmUDTInfo",
            string tableValuedResultSetCommand = "GetEdmTVFResultSet")
        {
            this.Name = name;
            this.ConnectionString = connectionString;
            _Model = new Lazy<EdmModel>(() =>
            {
                ModelCommand = modelCommand;
                FuncCommand = funcCommand;
                TableValuedCommand = tvfCommand;
                RelationCommand = relationCommand;
                StoredProcedureResultSetCommand = storedProcedureResultSetCommand;
                UserDefinedTableCommand = userDefinedTableCommand;
                TableValuedResultSetCommand = tableValuedResultSetCommand;
                var model = new EdmModel();
                var container = new EdmEntityContainer("ns", "container");
                model.AddElement(container);
                AddEdmElement(model);
                AddEdmFunction(model);
                AddTableValueFunction(model);
                BuildRelation(model);
                return model;

            });
        }
        #endregion

        #region method
        void AddEdmElement(EdmModel model)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string tableName = string.Empty;
            EdmEntityType t = null;
            IEdmEntitySet edmSet = null;
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(ModelCommand, (reader) =>
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
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string spRtvTypeName = string.Format("{0}_RtvCollectionType", spName);
            EdmComplexType t = null;
            t = new EdmComplexType("ns", spRtvTypeName);

            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(StoredProcedureResultSetCommand, (reader) =>
                 {
                     var et = DBType2EdmType(reader["DATA_TYPE"].ToString());
                     if (et.HasValue)
                     {
                         string col = reader["COLUMN_NAME"].ToString();
                         t.AddStructuralProperty(col, et.Value, true);
                     }
                 }, (par) => { par.AddWithValue("@Name", spName); });
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

        void AddEdmFunction(EdmModel model)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string currentName = string.Empty;
            Dictionary<string, IEdmTypeReference> pars = new Dictionary<string, IEdmTypeReference>();
            Dictionary<string, IEdmTypeReference> outPars = new Dictionary<string, IEdmTypeReference>();
            Dictionary<string, ParameterInfo> parsDic = new Dictionary<string, ParameterInfo>();
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(FuncCommand, (reader) =>
                {
                    string spName = reader["SPECIFIC_NAME"].ToString();
                    if (currentName != spName)
                    {
                        if (!string.IsNullOrEmpty(currentName))
                        {
                            AddEdmFunction(currentName, model, pars, outPars);
                            this.ParameterInfos.Add(currentName, parsDic);
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
                            string name = reader["USER_DEFINED_TYPE_NAME"].ToString();
                            EdmComplexTypeReference t = null;
                            t = BuildUDTType(name);
                            var pname = reader["PARAMETER_NAME"].ToString().TrimStart('@');
                            pars.Add(pname, t);
                            if (reader["PARAMETER_MODE"].ToString() == "INOUT")
                                outPars.Add(pname, t);
                        }
                    }
                });
                AddEdmFunction(currentName, model, pars, outPars);
                this.ParameterInfos.Add(currentName, parsDic);
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
        void AddTableValueFunction(EdmModel model)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string currentName = string.Empty;
            string funcName = string.Empty;
            Dictionary<string, IEdmTypeReference> pars = new Dictionary<string, IEdmTypeReference>();

            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(this.TableValuedCommand, (reader) =>
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
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string spRtvTypeName = string.Format("{0}_RtvCollectionType", name);
            EdmComplexType t = null;
            t = new EdmComplexType("ns", spRtvTypeName);

            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(this.TableValuedResultSetCommand, (reader) =>
                {
                    var et = DBType2EdmType(reader["DATA_TYPE"].ToString());
                    if (et.HasValue)
                    {
                        string col = reader["COLUMN_NAME"].ToString();
                        t.AddStructuralProperty(col, et.Value, true);
                    }
                }, (par) => { par.AddWithValue("@Name", name); });
            }
            var etr = new EdmComplexTypeReference(t, true);
            var t1 = new EdmCollectionTypeReference(new EdmCollectionType(etr));
            model.AddElement((t1.Definition as EdmCollectionType).ElementType.Definition as IEdmSchemaElement);
            return t1;

        }

        void BuildRelation(EdmModel model)
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
                db.ExecuteReader(this.RelationCommand, (reader) =>
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
                if (refrenceNav != null)
                {
                    refrenceNav.PrincipalProperties = principalProperties;
                    refrenceNav.DependentProperties = dependentProperties;

                    var np1 = parent.AddBidirectionalNavigation(refrenceNav, parentNav);
                    var parentSet1 = model.EntityContainer.FindEntitySet(parentName) as EdmEntitySet;
                    var referenceSet1 = model.EntityContainer.FindEntitySet(refrenceName) as EdmEntitySet;
                    parentSet1.AddNavigationTarget(np1, referenceSet1);
                }

            }
        }
        EdmComplexTypeReference BuildUDTType(string name)
        {
            EdmComplexType root = new EdmComplexType("ns", name);

            string cNmae = string.Empty;

            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(this.UserDefinedTableCommand, (reader) =>
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
                });
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
        static string BuildTVFTarget(IEdmFunction func, JObject parameterValues)
        {
            string templete = "{0}({1})";
            List<string> ps = new List<string>();
            foreach (var p in func.Parameters)
            {
                ps.Add(parameterValues[p.Name].ToString());
            }
            return string.Format(templete, func.Name, string.Join(",", ps));
        }
        static void SetParameter(IEdmFunction func, JObject parameterValues, IEdmType edmType, SqlParameterCollection pars)
        {
            if (parameterValues == null)
                return;
            JToken token = null;
            Type colType = null;
            foreach (var p in func.Parameters)
            {
                if (parameterValues.TryGetValue(p.Name, out token))
                {
                    if (p.Type.Definition.TypeKind == EdmTypeKind.Complex)
                    {
                        DataTable dt = new DataTable();
                        var c = p.Type.AsComplex();
                        foreach (var item in c.StructuralProperties())
                        {
                            dt.Columns.Add(item.Name, item.Type.PrimitiveKind().ToClrType());
                        }
                        foreach (var item in token)
                        {
                            DataRow dr = dt.NewRow();
                            foreach (JProperty col in item)
                            {
                                if (!dt.Columns.Contains(col.Name))
                                    continue;
                                colType = dt.Columns[col.Name].DataType;
                                if (colType == typeof(Boolean))
                                {
                                    dr.SetField(col.Name, col.Value.ToString() == "0" ? false : true);
                                }
                                else
                                    dr.SetField(col.Name, col.Value.ToString().ChangeType(colType));
                            }
                            dt.Rows.Add(dr);
                        }
                        pars.AddWithValue(p.Name, dt);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(token.ToString()))
                            pars.AddWithValue(p.Name, DBNull.Value);
                        else
                            pars.AddWithValue(p.Name, token.ToString().ChangeType(p.Type.PrimitiveKind()));
                    }
                }
            }
            if (edmType.TypeKind == EdmTypeKind.Entity)
            {
                foreach (var outp in (edmType as EdmEntityType).Properties())
                {
                    if (outp.Name == "$Results")
                        continue;
                    if (pars.Contains(outp.Name))
                    {
                        pars[outp.Name].Direction = ParameterDirection.Output;
                    }
                    else
                    {
                        pars.AddWithValue(outp.Name, DBNull.Value);
                    }
                }
            }
        }
        IEdmObject InvokeFuncCollection(IEdmFunction func, JObject parameterValues, ODataQueryOptions queryOptions = null)
        {
            IEdmType edmType = func.ReturnType.Definition;
            IEdmType elementType = (edmType as IEdmCollectionType).ElementType.Definition;
            EdmComplexObjectCollection collection = new EdmComplexObjectCollection(new EdmCollectionTypeReference(edmType as IEdmCollectionType));
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(func.Name, (reader) =>
                {
                    EdmComplexObject entity = new EdmComplexObject(elementType as IEdmComplexType);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        reader.SetEntityPropertyValue(i, entity);
                    }
                    collection.Add(entity);
                }, (pars) =>
                {
                    SetParameter(func, parameterValues, edmType, pars);
                });
            }
            return collection;
        }
        IEdmObject InvokeFuncComplex(IEdmFunction func, JObject parameterValues, ODataQueryOptions queryOptions = null)
        {
            IEdmType edmType = func.ReturnType.Definition;
            IEdmType elementType = null;
            var rtv = new EdmComplexObject(edmType as IEdmComplexType);
            object obj;
            rtv.TryGetPropertyValue("$Results", out obj);
            EdmComplexObjectCollection collection = obj as EdmComplexObjectCollection;
            var colltype = (edmType as IEdmComplexType).FindProperty("$Results").Type.Definition;
            elementType = (colltype as IEdmCollectionType).ElementType.Definition;
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                var par = db.ExecuteReader(func.Name, (reader) =>
                  {
                      EdmComplexObject entity = new EdmComplexObject(elementType as IEdmComplexType);
                      for (int i = 0; i < reader.FieldCount; i++)
                      {
                          reader.SetEntityPropertyValue(i, entity);
                      }
                      collection.Add(entity);
                  }, (pars) =>
                  {
                      SetParameter(func, parameterValues, edmType, pars);
                      var d1 = this.ParameterInfos[func.Name];
                      foreach (var p in (edmType as IEdmComplexType).Properties())
                      {
                          if (p.Name == "$Results")
                              continue;
                          var pp = d1[p.Name];
                          pars.Add(new SqlParameter(p.Name, pp.SqlDbType, pp.Length)
                          {
                              Direction = ParameterDirection.Output
                          });
                      }

                  });
                foreach (var outp in (edmType as IEdmComplexType).Properties())
                {
                    if (outp.Name == "$Results")
                        continue;
                    var v = par[outp.Name].Value;
                    if (DBNull.Value != v)
                        rtv.TrySetPropertyValue(outp.Name, v);
                }
            }
            return rtv;
        }
        IEdmObject InvokeTVF(IEdmFunction func, JObject parameterValues, ODataQueryOptions queryOptions = null)
        {
            IEdmType edmType = func.ReturnType.Definition;
            IEdmType elementType = (edmType as IEdmCollectionType).ElementType.Definition;
            EdmComplexObjectCollection collection = new EdmComplexObjectCollection(new EdmCollectionTypeReference(edmType as IEdmCollectionType));
            var target = BuildTVFTarget(func, parameterValues);
            var cmd = BuildSqlQueryCmd(queryOptions, target);
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(cmd, (reader) =>
                {
                    EdmComplexObject entity = new EdmComplexObject(elementType as IEdmComplexType);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        reader.SetEntityPropertyValue(i, entity);
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
            var keyDefine = entityType.DeclaredKey.First();
            int rtv = 0;
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteNonQuery(string.Format("delete {0}  where [{1}]=@{1}", entityType.Name, keyDefine.Name)
                      , (dbpars) =>
                      {
                          dbpars.AddWithValue("@" + keyDefine.Name, key.ChangeType((keyDefine.Type.PrimitiveKind())));
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
            var keyDefine = entityType.DeclaredKey.First();
            string cmdSql = "select {0} from [{1}] where [{2}]=@{2}";
            var cmdTxt = string.Format(cmdSql
                , queryOptions.ParseSelect()
                , cxt.Path.Segments[0].ToString()
                , keyDefine.Name);
            EdmEntityObject entity = new EdmEntityObject(entityType);
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(cmdTxt,
                    (reader) =>
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            reader.SetEntityPropertyValue(i, entity);
                        }
                    },
                    (par) => { par.AddWithValue("@" + keyDefine.Name, key.ChangeType(keyDefine.Type.PrimitiveKind())); },
                    CommandType.Text);
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

        public int GetFuncResultCount(IEdmFunction func, JObject parameterValues, ODataQueryOptions queryOptions)
        {
            int count = 0;
            IEdmType edmType = func.ReturnType.Definition;
            if (TVFList.Contains(func.Name))
            {
                var target = BuildTVFTarget(func, parameterValues);
                var cmd = BuildSqlQueryCmd(queryOptions, target);
                using (DbAccess db = new DbAccess(this.ConnectionString))
                {
                    var r = db.ExecuteScalar(cmd, null, CommandType.Text);
                    if (r != null)
                        count = (int)r;
                }
            }
            return count;
        }

        public IEdmObject InvokeFunction(IEdmFunction func, JObject parameterValues, ODataQueryOptions queryOptions = null)
        {
            if (TVFList.Contains(func.Name))
                return InvokeTVF(func, parameterValues, queryOptions);
            IEdmType edmType = func.ReturnType.Definition;
            if (edmType.TypeKind == EdmTypeKind.Collection)
                return InvokeFuncCollection(func, parameterValues, queryOptions);
            return InvokeFuncComplex(func, parameterValues, queryOptions);
        }

        public int Merge(string key, IEdmEntityObject entity)
        {
            string cmdTemplate = "update [{0}] set {1} where [{2}]=@{2} ";
            var entityType = entity.GetEdmType().Definition as EdmEntityType;
            var keyDefine = entityType.DeclaredKey.First();
            List<string> cols = new List<string>();
            List<SqlParameter> sqlpars = new List<SqlParameter>();
            object v = null;
            foreach (var p in (entity as Delta).GetChangedPropertyNames())
            {
                entity.TryGetPropertyValue(p, out v);
                cols.Add(string.Format("[{0}]=@{0}", p));
                sqlpars.Add(new SqlParameter("@" + p, v));
            }
            if (cols.Count == 0)
                return 0;
            sqlpars.Add(new SqlParameter("@" + keyDefine.Name, key.ChangeType(keyDefine.Type.PrimitiveKind())));
            int rtv;
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteNonQuery(string.Format(cmdTemplate, entityType.Name, string.Join(", ", cols), keyDefine.Name)
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
