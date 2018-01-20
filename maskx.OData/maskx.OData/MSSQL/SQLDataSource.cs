using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace maskx.OData.Sql
{
    public class SQLDataSource : IDataSource
    {
        #region members
        /// <summary>
        /// Get table/view schema
        /// </summary>
        public string ModelCommand { get; private set; }

        /// <summary>
        /// get stored procedures schema.(stored procedure name and parameters)
        /// </summary>
        public string ActionCommand { get; private set; }
        /// <summary>
        /// get the table-valued function information
        /// </summary>
        public string FunctionCommand { get; private set; }
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

        readonly string ConnectionString;

        Dictionary<string, Dictionary<string, ParameterInfo>> ParameterInfos = new Dictionary<string, Dictionary<string, ParameterInfo>>();
        #endregion

        #region construct
        public SQLDataSource(string name, string connectionString,
            string modelCommand = "GetEdmModelInfo",
            string actionCommand = "GetEdmSPInfo",
            string functionCommand = "GetEdmTVFInfo",
            string relationCommand = "GetEdmRelationship",
            string storedProcedureResultSetCommand = "GetEdmSPResultSet",
            string userDefinedTableCommand = "GetEdmUDTInfo",
            string tableValuedResultSetCommand = "GetEdmTVFResultSet")
        {
            this.Name = name;
            this.ConnectionString = connectionString;
            this.Configuration = new Configuration();
            ModelCommand = modelCommand;
            ActionCommand = actionCommand;
            FunctionCommand = functionCommand;
            RelationCommand = relationCommand;
            StoredProcedureResultSetCommand = storedProcedureResultSetCommand;
            UserDefinedTableCommand = userDefinedTableCommand;
            TableValuedResultSetCommand = tableValuedResultSetCommand;
        }
        #endregion

        #region method
        private EdmModel BuildEdmModel()
        {
            var model = new EdmModel();
            string dbName = string.Empty;
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                dbName = conn.Database;
                conn.Close();
            }
            var container = new EdmEntityContainer("ns", dbName);
            model.AddElement(container);
            AddEdmElement(model);
            AddEdmAction(model);
            AddTableValueFunction(model);
            BuildRelation(model);
            return model;
        }
        void AddEdmElement(EdmModel model)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string tableName = string.Empty;
            string schemaName = string.Empty;
            string entityName = string.Empty;
            EdmEntityType t = null;
            IEdmEntitySet edmSet = null;
            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(ModelCommand, (reader) =>
                {
                    tableName = reader["TABLE_NAME"].ToString();
                    schemaName = reader["SCHEMA_NAME"].ToString();
                    if (Configuration.LowerName)
                    {
                        tableName = tableName.ToLower();
                        schemaName = schemaName.ToLower();
                    }
                    entityName = schemaName == Configuration.DefaultSchema ? tableName : string.Format("{0}.{1}", schemaName, tableName);
                    if (t == null || t.Name != tableName || t.Namespace != schemaName)
                    {
                        var d = model.FindDeclaredType(string.Format("{0}.{1}", schemaName, tableName));
                        edmSet = container.FindEntitySet(entityName);
                        if (edmSet == null)
                        {
                            t = new EdmEntityType(schemaName, tableName);
                            model.AddElement(t);
                            container.AddEntitySet(entityName, t);
                        }
                        else
                            t = edmSet.EntityType() as EdmEntityType;
                    }
                    var et = Utility.DBType2EdmType(reader["DATA_TYPE"].ToString());
                    if (et.HasValue)
                    {
                        string col = reader["COLUMN_NAME"].ToString();
                        if (Configuration.LowerName)
                            col = col.ToLower();
                        EdmStructuralProperty key = t.AddStructuralProperty(col, et.Value, true);
                        if (col == reader["KEY_COLUMN_NAME"].ToString())
                        {
                            t.AddKeys(key);
                        }
                    }
                });
            }
        }
        IEdmTypeReference BuildSPReturnType(string nameSpace, string spName, EdmModel model, Dictionary<string, IEdmTypeReference> outPars)
        {
            string spRtvTypeName = string.Format("{0}_RtvType", spName);
            var t = BuildSPReturnType(nameSpace, spName, model);
            model.AddElement((t.Definition as EdmCollectionType).ElementType.Definition as IEdmSchemaElement);

            EdmComplexType root = new EdmComplexType(nameSpace, spRtvTypeName);
            model.AddElement(root);
            foreach (var item in outPars)
            {
                root.AddStructuralProperty(item.Key, item.Value);
            }
            root.AddStructuralProperty("$result", t);
            return new EdmComplexTypeReference(root, true);
        }
        IEdmTypeReference BuildSPReturnType(string nameSpace, string spName, EdmModel model)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string spRtvTypeName = string.Format("{0}_RtvCollectionType", spName);
            EdmComplexType t = null;
            t = new EdmComplexType(nameSpace, spRtvTypeName);

            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(StoredProcedureResultSetCommand, (reader) =>
                {
                    if (reader.IsDBNull("DATA_TYPE"))
                        return;
                    var et = Utility.DBType2EdmType(reader["DATA_TYPE"].ToString());
                    if (et.HasValue)
                    {
                        string col = reader["COLUMN_NAME"].ToString();
                        if (Configuration.LowerName)
                            col = col.ToLower();
                        if (string.IsNullOrEmpty(col))
                            throw new Exception(string.Format("{0} has wrong return type. see [exec GetEdmSPResultSet '{0}'] ", spName));
                        t.AddStructuralProperty(col, et.Value, true);
                    }
                }, (par) => { (par as SqlParameterCollection).AddWithValue("@Name", spName); });
            }
            var etr = new EdmComplexTypeReference(t, true);
            return new EdmCollectionTypeReference(new EdmCollectionType(etr));
        }
        /// <summary>
        /// Actions can have side-effects. For example, Actions can be used to modify data or to invoke custom operations
        /// </summary>
        /// <param name="model"></param>
        void AddEdmAction(EdmModel model)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string currentName = string.Empty;
            string currentNs = string.Empty;
            Dictionary<string, IEdmTypeReference> pars = new Dictionary<string, IEdmTypeReference>();
            Dictionary<string, IEdmTypeReference> outPars = new Dictionary<string, IEdmTypeReference>();
            Dictionary<string, ParameterInfo> parsDic = new Dictionary<string, ParameterInfo>();
            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(ActionCommand, (reader) =>
                {
                    string spName = reader["SPECIFIC_NAME"].ToString();
                    string ns = reader["SCHEMA_NAME"].ToString();
                    if (Configuration.LowerName)
                    {
                        spName = spName.ToLower();
                        ns = ns.ToLower();
                    }
                    if (currentName != spName || currentNs != ns)
                    {
                        if (!string.IsNullOrEmpty(currentName))
                        {
                            AddEdmAction(currentNs, currentName, model, pars, outPars);
                            this.ParameterInfos.Add(currentName, parsDic);
                            parsDic = new Dictionary<string, ParameterInfo>();
                        }
                        currentName = spName;
                        currentNs = ns;
                    }
                    if (!reader.IsDBNull("DATA_TYPE"))//some stored procedures have not parameters
                    {
                        var et = Utility.DBType2EdmType(reader["DATA_TYPE"].ToString());
                        if (et.HasValue)
                        {
                            var t = EdmCoreModel.Instance.GetPrimitive(et.Value, true);
                            var pname = reader["PARAMETER_NAME"].ToString().TrimStart('@');
                            if (Configuration.LowerName)
                                pname = pname.ToLower();
                            pars.Add(pname, t);
                            parsDic.Add(pname, new ParameterInfo()
                            {
                                Name = pname,
                                SqlDbType = Utility.SqlTypeString2SqlType(reader["DATA_TYPE"].ToString()),
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
                            string udt_name = reader["USER_DEFINED_TYPE_NAME"].ToString();
                            string udt_ns = reader["USER_DEFINED_TYPE_SCHEMA"].ToString();
                            if (Configuration.LowerName)
                            {
                                udt_name = udt_name.ToLower();
                                udt_ns = udt_ns.ToLower();
                            }
                            EdmComplexTypeReference t = null;
                            t = BuildUDTType(udt_ns, udt_name);
                            var pname = reader["PARAMETER_NAME"].ToString().TrimStart('@');
                            if (Configuration.LowerName)
                                pname = pname.ToLower();
                            pars.Add(pname, t);
                            if (reader["PARAMETER_MODE"].ToString() == "INOUT")
                                outPars.Add(pname, t);
                        }
                    }
                });
                AddEdmAction(currentNs, currentName, model, pars, outPars);
                this.ParameterInfos.Add(currentName, parsDic);
                parsDic = new Dictionary<string, ParameterInfo>();
            }
        }
        void AddEdmAction(string nameSpace, string spName,
           EdmModel model,
           Dictionary<string, IEdmTypeReference> pars,
           Dictionary<string, IEdmTypeReference> outPars)
        {
            IEdmTypeReference t = BuildSPReturnType(nameSpace, spName, model, outPars);
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            var action = new EdmAction(nameSpace, spName, t, false, null);
            foreach (var item in pars)
            {
                action.AddParameter(item.Key, item.Value);
            }
            model.AddElement(action);
            if (action.Namespace == Configuration.DefaultSchema)
                container.AddActionImport(action.Name, action, null);
            else
                container.AddActionImport(string.Format("{0}.{1}", action.Namespace, action.Name), action, null);
            pars.Clear();
            outPars.Clear();
        }

        void AddTableValueFunction(string nameSpace, string name, EdmModel model, Dictionary<string, IEdmTypeReference> pars)
        {
            var container = model.EntityContainer as EdmEntityContainer;
            var t = BuildTableValueType(nameSpace, name, model);
            var func = new EdmFunction(nameSpace, name, t, false, null, true);
            foreach (var item in pars)
            {
                func.AddParameter(item.Key, item.Value);
            }
            container.AddFunctionImport(func.Name, func, null, true);
            model.AddElement(func);
            pars.Clear();
        }
        /// <summary>
        /// Functions MUST NOT have side-effects. Functions can be invoked from a URL that addresses a resource or within an expression to a $filter or $orderby system query option.
        /// </summary>
        /// <param name="model"></param>
        void AddTableValueFunction(EdmModel model)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string currentName = string.Empty;
            string funcName = string.Empty;
            string ns = string.Empty;
            string currentNS = string.Empty;
            Dictionary<string, IEdmTypeReference> pars = new Dictionary<string, IEdmTypeReference>();

            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(this.FunctionCommand, (reader) =>
                {
                    funcName = reader["SPECIFIC_NAME"].ToString();
                    ns = reader["SCHEMA_NAME"].ToString();
                    if (Configuration.LowerName)
                    {
                        funcName = funcName.ToLower();
                        ns = ns.ToLower();
                    }
                    if (currentName != funcName || currentNS != ns)
                    {
                        if (!string.IsNullOrEmpty(currentName))
                        {
                            AddTableValueFunction(currentNS, currentName, model, pars);
                        }
                        currentName = funcName;
                        currentNS = ns;
                    }
                    if (!reader.IsDBNull("DATA_TYPE"))
                    {
                        var et = Utility.DBType2EdmType(reader["DATA_TYPE"].ToString());
                        if (et.HasValue)
                        {
                            var t = EdmCoreModel.Instance.GetPrimitive(et.Value, true);
                            var pname = reader["PARAMETER_NAME"].ToString().TrimStart('@');
                            if (Configuration.LowerName)
                                pname = pname.ToLower();
                            pars.Add(pname, t);
                        }
                    }
                });
                if (!string.IsNullOrEmpty(currentName))
                    AddTableValueFunction(currentNS, currentName, model, pars);
            }
        }

        IEdmTypeReference BuildTableValueType(string nameSpace, string name, EdmModel model)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string spRtvTypeName = string.Format("{0}_RtvCollectionType", name);
            EdmComplexType t = null;
            t = new EdmComplexType(nameSpace, spRtvTypeName);

            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(this.TableValuedResultSetCommand, (reader) =>
                {
                    var et = Utility.DBType2EdmType(reader["DATA_TYPE"].ToString());
                    if (et.HasValue)
                    {
                        string col = reader["COLUMN_NAME"].ToString();
                        if (Configuration.LowerName)
                            col = col.ToLower();
                        t.AddStructuralProperty(col, et.Value, true);
                    }
                }, (par) => { (par as SqlParameterCollection).AddWithValue("@Name", name); });
            }
            var etr = new EdmComplexTypeReference(t, true);
            var t1 = new EdmCollectionTypeReference(new EdmCollectionType(etr));
            model.AddElement((t1.Definition as EdmCollectionType).ElementType.Definition as IEdmSchemaElement);
            return t1;

        }

        void BuildRelation(EdmModel model)
        {
            string fk = string.Empty;
            string parentSchemaName = string.Empty;
            string referencedSchemaName = string.Empty;
            string parentName = string.Empty;
            string referencedName = string.Empty;
            string parentColName = string.Empty;
            string referencedColName = string.Empty;
            string parentEntityName = string.Empty;
            string referencedEntityName = string.Empty;
            EdmEntityType parent = null;
            EdmEntityType refrenced = null;
            EdmNavigationPropertyInfo parentNav = null;
            EdmNavigationPropertyInfo referencedNav = null;
            List<IEdmStructuralProperty> principalProperties = null;
            List<IEdmStructuralProperty> dependentProperties = null;

            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(this.RelationCommand, (reader) =>
                {
                    if (fk != reader["FK_NAME"].ToString())
                    {
                        parentName = reader["PARENT_NAME"].ToString();
                        referencedName = reader["REFRENCED_NAME"].ToString();
                        parentSchemaName = reader["PARENT_SCHEMA_NAME"].ToString();
                        referencedSchemaName = reader["REFRENCED_SCHEMA_NAME"].ToString();
                        if (Configuration.LowerName)
                        {
                            parentName = parentName.ToLower();
                            referencedName = referencedName.ToLower();
                            parentSchemaName = parentSchemaName.ToLower();
                            referencedSchemaName = referencedSchemaName.ToLower();
                        }
                        parentEntityName = parentSchemaName == Configuration.DefaultSchema ? parentName : string.Format("{0}.{1}", parentSchemaName, parentName);
                        referencedEntityName = referencedSchemaName == Configuration.DefaultSchema ? referencedName : string.Format("{0}.{1}", referencedSchemaName, referencedName);
                        if (!string.IsNullOrEmpty(fk))
                        {
                            parentNav.PrincipalProperties = principalProperties;
                            parentNav.DependentProperties = dependentProperties;
                            var np = refrenced.AddBidirectionalNavigation(parentNav, referencedNav);
                            var parentSet = model.EntityContainer.FindEntitySet(parentEntityName) as EdmEntitySet;
                            var referenceSet = model.EntityContainer.FindEntitySet(referencedEntityName) as EdmEntitySet;
                            referenceSet.AddNavigationTarget(np, parentSet);
                        }
                        fk = reader["FK_NAME"].ToString();

                        parent = model.FindDeclaredType(string.Format("{0}.{1}", parentSchemaName, parentName)) as EdmEntityType;
                        refrenced = model.FindDeclaredType(string.Format("{0}.{1}", referencedSchemaName, referencedName)) as EdmEntityType;
                        parentNav = new EdmNavigationPropertyInfo
                        {
                            Name = parentName,
                            TargetMultiplicity = EdmMultiplicity.Many,
                            Target = parent
                        };
                        referencedNav = new EdmNavigationPropertyInfo
                        {
                            Name = referencedName,
                            TargetMultiplicity = EdmMultiplicity.Many
                        };
                        principalProperties = new List<IEdmStructuralProperty>();
                        dependentProperties = new List<IEdmStructuralProperty>();
                    }
                    parentColName = reader["PARENT_COLUMN_NAME"].ToString();
                    referencedColName = reader["REFREANCED_COLUMN_NAME"].ToString();
                    if (Configuration.LowerName)
                    {
                        parentColName = parentColName.ToLower();
                        referencedColName = referencedColName.ToLower();
                    }
                    principalProperties.Add(parent.FindProperty(parentColName) as IEdmStructuralProperty);
                    dependentProperties.Add(refrenced.FindProperty(referencedColName) as IEdmStructuralProperty);
                }, null, CommandType.Text);
                if (referencedNav != null)
                {
                    parentNav.PrincipalProperties = principalProperties;
                    parentNav.DependentProperties = dependentProperties;
                    var np1 = refrenced.AddBidirectionalNavigation(parentNav, referencedNav);
                    var parentSet1 = model.EntityContainer.FindEntitySet(parentEntityName) as EdmEntitySet;
                    var referenceSet1 = model.EntityContainer.FindEntitySet(referencedEntityName) as EdmEntitySet;
                    referenceSet1.AddNavigationTarget(np1, parentSet1);
                }
            }
        }
        EdmComplexTypeReference BuildUDTType(string nameSpace, string name)
        {
            EdmComplexType root = new EdmComplexType(nameSpace, name);
            string cNmae = string.Empty;
            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(this.UserDefinedTableCommand, (reader) =>
                {
                    var et = Utility.DBType2EdmType(reader["DATA_TYPE"].ToString());
                    if (et.HasValue)
                    {
                        cNmae = reader["COLUMN_NAME"].ToString();
                        if (Configuration.LowerName)
                            cNmae = cNmae.ToLower();
                        root.AddStructuralProperty(cNmae, et.Value);
                    }

                }, (pars) =>
                {
                    pars.AddWithValue("NAME", name);
                    pars.AddWithValue("SCHEMA_NAME", nameSpace);
                });
            }
            return new EdmComplexTypeReference(root, true);
        }

        internal virtual string BuildSqlQueryCmd(ODataQueryOptions options, List<SqlParameter> pars, string target = "")
        {
            var cxt = options.Context;
            string table = target;
            if (string.IsNullOrEmpty(target))
            {
                var t = cxt.ElementType as EdmEntityType;
                table = string.Format("{0}.[{1}]", t.Namespace, t.Name);
            }

            string cmdSql = "select {0} {1} from {2} {3} {4} {5} {6}";
            string top = string.Empty;
            string skip = string.Empty;
            string fetch = string.Empty;
            string orderby = options.ParseOrderBy();
            if (options.Count == null && options.Top != null)
            {
                if (options.Skip != null)
                {
                    skip = string.Format("OFFSET {0} ROWS", options.Skip.RawValue);
                    fetch = string.Format("FETCH NEXT {0} ROWS ONLY", options.Top.RawValue);
                    top = string.Empty;
                    if (string.IsNullOrEmpty(orderby))
                    {
                        var entityType = cxt.ElementType as EdmEntityType;
                        var keyDefine = entityType.DeclaredKey.First();
                        orderby = string.Format(" order by [{0}] ", keyDefine.Name);
                    }
                }
                else
                    top = "top " + options.Top.RawValue;
            }

            var cmdtxt = string.Format(cmdSql
                , top
                , options.ParseSelect()
                , table
                , options.ParseFilter(pars)
                , orderby
                , skip
                , fetch);
            return cmdtxt;
        }
        internal virtual string BuildExpandQueryString(EdmEntityObject edmEntity, ExpandedNavigationSelectItem expanded, out List<SqlParameter> pars)
        {
            string cmdSql = "select {0} {1} from {2}.[{3}] where  {4} {5} {6} {7}";
            string schema = string.Empty;
            string table = string.Empty;
            string top = string.Empty;
            string skip = string.Empty;
            string fetch = string.Empty;
            string where = string.Empty;
            string safeVar = string.Empty;
            pars = new List<SqlParameter>();
            var wp = new List<string>();
            foreach (NavigationPropertySegment item2 in expanded.PathToNavigationProperty)
            {
                foreach (var p in item2.NavigationProperty.ReferentialConstraint.PropertyPairs)
                {
                    edmEntity.TryGetPropertyValue(p.DependentProperty.Name, out object v);
                    safeVar = Utility.SafeSQLVar(p.PrincipalProperty.Name) + pars.Count;
                    wp.Add(string.Format("[{0}]=@{1}", p.PrincipalProperty.Name, safeVar));
                    pars.Add(new SqlParameter(safeVar, v));
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
                , expanded.ParseFilter(pars)
                , expanded.ParseOrderBy()
                , skip
                , fetch);
        }

        static string BuildTVFTarget(IEdmFunction func, JObject parameterValues, out List<SqlParameter> sqlpars)
        {
            string templete = "{0}.[{1}]({2})";
            sqlpars = new List<SqlParameter>();
            List<string> pars = new List<string>();
            foreach (var p in func.Parameters)
            {
                var safeVar = Utility.SafeSQLVar(p.Name) + sqlpars.Count;
                var v = (parameterValues[p.Name] as JValue).Value.ChangeType(p.Type.PrimitiveKind());
                sqlpars.Add(new SqlParameter(safeVar, v));
            }
            return string.Format(templete, func.Namespace, func.Name, string.Join(",", sqlpars.ConvertAll<string>(p => "@" + p.ParameterName)));
        }
        void SetParameter(IEdmAction action, JObject parameterValues, SqlParameterCollection pars)
        {
            if (parameterValues == null)
                return;
            JToken token = null;
            Type colType = null;
            IEdmComplexType edmType = action.ReturnType.Definition as IEdmComplexType;
            foreach (var p in action.Parameters)
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
                                    dr.SetField(col.Name, col.Value.ToString() != "0");
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
                var d1 = this.ParameterInfos[action.Name];
                foreach (var outp in (edmType as EdmEntityType).Properties())
                {
                    if (outp.Name == "$result")
                        continue;
                    if (pars.Contains(outp.Name))
                    {
                        pars[outp.Name].Direction = ParameterDirection.Output;
                    }
                    else
                    {
                        var pp = d1[outp.Name];
                        pars.Add(new SqlParameter(outp.Name, pp.SqlDbType, pp.Length)
                        {
                            Direction = ParameterDirection.Output
                        });
                    }
                }
            }
        }

        #endregion

        #region IDataSource
        EdmModel _Model;
        readonly object _ModelLocker = new object();
        public Configuration Configuration { get; set; }
        public EdmModel Model
        {
            get
            {
                if (_Model == null)
                {
                    lock (_ModelLocker)
                    {
                        if (_Model == null)
                        {
                            _Model = BuildEdmModel();
                        }
                    }
                }
                return _Model;
            }
        }

        public string Name
        {
            get;
            private set;
        }

        public Action<RequestInfo> BeforeExcute { get; set; }
        public Func<RequestInfo, object, object> AfrerExcute { get; set; }
        public string Create(IEdmEntityObject entity)
        {
            var edmType = entity.GetEdmType();
            var entityType = edmType.Definition as EdmEntityType;

            object rtv = null;
            string cmdTemplate = "insert into {0}.[{1}] ({2}) values ({3}) select SCOPE_IDENTITY() ";
            List<string> cols = new List<string>();
            List<string> pars = new List<string>();
            List<SqlParameter> sqlpars = new List<SqlParameter>();
            object v = null;
            string safevar = string.Empty;
            int index = 0;
            foreach (var p in (entity as Delta).GetChangedPropertyNames())
            {
                entity.TryGetPropertyValue(p, out v);
                cols.Add(string.Format("[{0}]", p));
                safevar = Utility.SafeSQLVar(p) + index;
                pars.Add("@" + safevar);
                sqlpars.Add(new SqlParameter("@" + safevar, v));
                index++;
            }
            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteScalar(string.Format(cmdTemplate, entityType.Namespace, entityType.Name, string.Join(", ", cols), string.Join(", ", pars))
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
            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteNonQuery(string.Format("delete {0}.[{1}]  where [{2}]=@{2}", entityType.Namespace, entityType.Name, keyDefine.Name)
                      , (pars) =>
                      {
                          (pars as SqlParameterCollection).AddWithValue("@" + keyDefine.Name, key.ChangeType((keyDefine.Type.PrimitiveKind())));
                      }, CommandType.Text);
            }
            return rtv;
        }

        public EdmEntityObjectCollection Get(ODataQueryOptions queryOptions)
        {
            var cxt = queryOptions.Context;
            var entityType = cxt.ElementType as EdmEntityType;
            var table = entityType.Name;
            var edmType = cxt.Path.EdmType as IEdmCollectionType;
            List<SqlParameter> pars = new List<SqlParameter>();
            string sqlCmd = BuildSqlQueryCmd(queryOptions, pars);
            EdmEntityObjectCollection collection = new EdmEntityObjectCollection(new EdmCollectionTypeReference(edmType));
            bool needExpand = queryOptions.SelectExpand != null && !string.IsNullOrEmpty(queryOptions.SelectExpand.RawExpand);

            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(sqlCmd, (reader) =>
                {
                    EdmEntityObject entity = new EdmEntityObject(entityType);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        reader.SetEntityPropertyValue(i, entity);
                    }
                    if (needExpand)
                    {
                        Expand(entity, queryOptions.SelectExpand.SelectExpandClause);
                    }
                    collection.Add(entity);

                }, (parbuilder) =>
                {
                    parbuilder.AddRange(pars.ToArray());
                },
                CommandType.Text);
            }
            return collection;
        }

        public EdmEntityObject Get(string key, ODataQueryOptions queryOptions)
        {
            var cxt = queryOptions.Context;
            var entityType = cxt.ElementType as EdmEntityType;
            var keyDefine = entityType.DeclaredKey.First();
            string cmdSql = "select {0} from [{1}] where [{2}]=@{2}";
            var cmdTxt = string.Format(cmdSql
                , queryOptions.ParseSelect()
                , entityType.Name
                , keyDefine.Name);
            EdmEntityObject entity = new EdmEntityObject(entityType);
            bool needExpand = queryOptions.SelectExpand != null && !string.IsNullOrEmpty(queryOptions.SelectExpand.RawExpand);
            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(cmdTxt,
                    (reader) =>
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            reader.SetEntityPropertyValue(i, entity);
                        }
                        if (needExpand)
                        {
                            Expand(entity, queryOptions.SelectExpand.SelectExpandClause);
                        }
                    },
                    (par) =>
                    {
                        (par as SqlParameterCollection).AddWithValue("@" + keyDefine.Name, key.ChangeType(keyDefine.Type.PrimitiveKind()));
                    },
                    CommandType.Text);
            }
            return entity;
        }
        void Expand(EdmEntityObject edmEntity, SelectExpandClause expandClause)
        {
            foreach (var item1 in expandClause.SelectedItems)
            {
                var expanded = item1 as ExpandedNavigationSelectItem;
                if (expanded == null)
                    continue;
                CreateExpandEntity(edmEntity, expanded);
            }
        }
        private void CreateExpandEntity(EdmEntityObject edmEntity, ExpandedNavigationSelectItem expanded)
        {
            string cmdtxt = BuildExpandQueryString(edmEntity, expanded, out List<SqlParameter> pars);
            var edmType = expanded.NavigationSource.Type as IEdmCollectionType;
            var entityType = edmType.ElementType.AsEntity();
            EdmEntityObjectCollection collection = new EdmEntityObjectCollection(new EdmCollectionTypeReference(edmType));
            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(cmdtxt, (reader) =>
                {
                    EdmEntityObject entity = new EdmEntityObject(entityType);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        reader.SetEntityPropertyValue(i, entity);
                    }
                    Expand(entity, expanded.SelectAndExpand);
                    collection.Add(entity);
                }, (parbuilder) =>
                {
                    parbuilder.AddRange(pars.ToArray());
                },
                CommandType.Text);
            }
            edmEntity.TrySetPropertyValue(expanded.NavigationSource.Name, collection);
        }
        public int GetCount(ODataQueryOptions queryOptions)
        {
            var cxt = queryOptions.Context;
            var entityType = cxt.ElementType as EdmEntityType;

            object rtv = null;
            List<SqlParameter> pars = new List<SqlParameter>();
            string sqlCmd = BuildSqlQueryCmd(queryOptions, pars);
            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteScalar(sqlCmd,
                    (parBuilder) => { parBuilder.AddRange(pars.ToArray()); },
                    CommandType.Text);
            }
            if (rtv == null)
                return 0;
            return (int)rtv;
        }

        public int GetFuncResultCount(IEdmFunction func, JObject parameterValues, ODataQueryOptions queryOptions)
        {
            int count = 0;
            IEdmType edmType = func.ReturnType.Definition;
            var target = BuildTVFTarget(func, parameterValues, out List<SqlParameter> sqlpars);

            var cmd = BuildSqlQueryCmd(queryOptions, sqlpars, target);
            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                var r = db.ExecuteScalar(
                    cmd,
                    (parBuider) => { parBuider.AddRange(sqlpars.ToArray()); },
                    CommandType.Text);
                if (r != null)
                    count = (int)r;
            }
            return count;
        }

        public IEdmObject InvokeFunction(IEdmFunction func, JObject parameterValues, ODataQueryOptions queryOptions = null)
        {
            IEdmType edmType = func.ReturnType.Definition;
            IEdmType elementType = (edmType as IEdmCollectionType).ElementType.Definition;
            EdmComplexObjectCollection collection = new EdmComplexObjectCollection(new EdmCollectionTypeReference(edmType as IEdmCollectionType));
            var target = BuildTVFTarget(func, parameterValues, out List<SqlParameter> sqlpars);
            var cmd = BuildSqlQueryCmd(queryOptions, sqlpars, target);
            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(cmd, (reader) =>
                {
                    EdmComplexObject entity = new EdmComplexObject(elementType as IEdmComplexType);
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        reader.SetEntityPropertyValue(i, entity);
                    }
                    collection.Add(entity);
                },
                (parBuilder) => { parBuilder.AddRange(sqlpars.ToArray()); },
                CommandType.Text);
            }
            return collection;

        }

        public int Merge(string key, IEdmEntityObject entity)
        {
            string cmdTemplate = "update {0}.[{1}] set {2} where [{3}]=@{3} ";
            var entityType = entity.GetEdmType().Definition as EdmEntityType;

            var keyDefine = entityType.DeclaredKey.First();
            List<string> cols = new List<string>();
            List<SqlParameter> sqlpars = new List<SqlParameter>();
            object v = null;
            foreach (var p in (entity as Delta).GetChangedPropertyNames())
            {
                if (entity.TryGetPropertyValue(p, out v))
                {
                    cols.Add(string.Format("[{0}]=@{0}", p));
                    sqlpars.Add(new SqlParameter("@" + p, v));
                }
            }
            if (cols.Count == 0)
                return 0;
            sqlpars.Add(new SqlParameter("@" + keyDefine.Name, key.ChangeType(keyDefine.Type.PrimitiveKind())));
            int rtv;
            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteNonQuery(string.Format(cmdTemplate, entityType.Namespace, entityType.Name, string.Join(", ", cols), keyDefine.Name)
                    , (dbpars) =>
                    {
                        dbpars.AddRange(sqlpars.ToArray());
                    }, CommandType.Text);
            }
            return rtv;
        }

        public int Replace(string key, IEdmEntityObject entity)
        {
            string cmdTemplate = "update {0}.[{1}] set {2} where [{3}]=@{3}  ";
            var entityType = entity.GetEdmType().Definition as EdmEntityType;

            var keyDefine = entityType.DeclaredKey.First();
            List<string> cols = new List<string>();
            List<string> pars = new List<string>();
            List<SqlParameter> sqlpars = new List<SqlParameter>();
            object v = null;

            foreach (var p in entityType.Properties())
            {
                if (p.PropertyKind == EdmPropertyKind.Navigation) continue;
                if (entity.TryGetPropertyValue(p.Name, out v))
                {
                    if (keyDefine.Name == p.Name) continue;
                    cols.Add(string.Format("[{0}]=@{0}", p.Name));
                    sqlpars.Add(new SqlParameter("@" + p.Name, v));
                }
            }
            if (cols.Count == 0)
                return 0;
            sqlpars.Add(new SqlParameter("@" + keyDefine.Name, key.ChangeType(keyDefine.Type.PrimitiveKind())));

            int rtv;
            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteNonQuery(string.Format(cmdTemplate, entityType.Namespace, entityType.Name, string.Join(", ", cols), keyDefine.Name)
                    , (dbpars) =>
                    {
                        dbpars.AddRange(sqlpars.ToArray());
                    }, CommandType.Text);
            }
            return rtv;
        }
        public IEdmObject DoAction(IEdmAction action, JObject parameterValues)
        {
            IEdmComplexType edmType = action.ReturnType.Definition as IEdmComplexType;
            var colltype = edmType.FindProperty("$result").Type.Definition;
            IEdmComplexType elementType = (colltype as IEdmCollectionType).ElementType.Definition as IEdmComplexType;

            EdmComplexObject rtv = new EdmComplexObject(edmType);
            rtv.TryGetPropertyValue("$result", out object obj);
            EdmComplexObjectCollection collection = obj as EdmComplexObjectCollection;
            using (var db = new MSSQLDbAccess(this.ConnectionString))
            {
                var par = db.ExecuteReader(string.Format("{0}.[{1}]", action.Namespace, action.Name), (reader) =>
                    {
                        EdmComplexObject entity = new EdmComplexObject(elementType);
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            reader.SetEntityPropertyValue(i, entity);
                        }
                        collection.Add(entity);
                    }, (pars) =>
                    {
                        SetParameter(action, parameterValues, pars);
                    });
                foreach (var outp in edmType.Properties())
                {
                    if (outp.Name == "$result")
                        continue;
                    var v = par[outp.Name].Value;
                    if (DBNull.Value != v)
                        rtv.TrySetPropertyValue(outp.Name, v);
                }
            }
            return rtv;
        }

        #endregion
    }
}
