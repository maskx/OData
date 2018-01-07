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
            var container = new EdmEntityContainer("ns", "container");
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
                    var et = Utility.DBType2EdmType(reader["DATA_TYPE"].ToString());
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

            EdmComplexType root = new EdmComplexType("ns", spRtvTypeName);
            model.AddElement(root);
            foreach (var item in outPars)
            {
                root.AddStructuralProperty(item.Key, item.Value);
            }
            root.AddStructuralProperty("$result", t);
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
                    if (reader.IsDBNull("DATA_TYPE"))
                        return;
                    var et = Utility.DBType2EdmType(reader["DATA_TYPE"].ToString());
                    if (et.HasValue)
                    {
                        string col = reader["COLUMN_NAME"].ToString();
                        if (string.IsNullOrEmpty(col))
                            throw new Exception(string.Format("{0} has wrong return type. see [exec GetEdmSPResultSet '{0}'] ", spName));
                        t.AddStructuralProperty(col, et.Value, true);
                    }
                }, (par) => { par.AddWithValue("@Name", spName); });
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
            Dictionary<string, IEdmTypeReference> pars = new Dictionary<string, IEdmTypeReference>();
            Dictionary<string, IEdmTypeReference> outPars = new Dictionary<string, IEdmTypeReference>();
            Dictionary<string, ParameterInfo> parsDic = new Dictionary<string, ParameterInfo>();
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(ActionCommand, (reader) =>
                {
                    string spName = reader["SPECIFIC_NAME"].ToString();
                    if (currentName != spName)
                    {
                        if (!string.IsNullOrEmpty(currentName))
                        {
                            AddEdmAction(currentName, model, pars, outPars);
                            this.ParameterInfos.Add(currentName, parsDic);
                            parsDic = new Dictionary<string, ParameterInfo>();
                        }
                        currentName = spName;
                    }
                    if (!reader.IsDBNull("DATA_TYPE"))
                    {
                        var et = Utility.DBType2EdmType(reader["DATA_TYPE"].ToString());
                        if (et.HasValue)
                        {
                            var t = EdmCoreModel.Instance.GetPrimitive(et.Value, true);
                            var pname = reader["PARAMETER_NAME"].ToString().TrimStart('@');
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
                AddEdmAction(currentName, model, pars, outPars);
                this.ParameterInfos.Add(currentName, parsDic);
                parsDic = new Dictionary<string, ParameterInfo>();
            }
        }
        void AddEdmAction(string spName,
           EdmModel model,
           Dictionary<string, IEdmTypeReference> pars,
           Dictionary<string, IEdmTypeReference> outPars)
        {
            IEdmTypeReference t = BuildSPReturnType(spName, model, outPars);
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            var action = new EdmAction(container.Namespace, spName, t, false, null);
            foreach (var item in pars)
            {
                action.AddParameter(item.Key, item.Value);
            }
            model.AddElement(action);
            container.AddActionImport(action.Name, action, null);

            pars.Clear();
            outPars.Clear();
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
            Dictionary<string, IEdmTypeReference> pars = new Dictionary<string, IEdmTypeReference>();

            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                db.ExecuteReader(this.FunctionCommand, (reader) =>
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
                        var et = Utility.DBType2EdmType(reader["DATA_TYPE"].ToString());
                        if (et.HasValue)
                        {
                            var t = EdmCoreModel.Instance.GetPrimitive(et.Value, true);
                            var pname = reader["PARAMETER_NAME"].ToString().TrimStart('@');
                            pars.Add(pname, t);
                        }
                    }
                });
                if (!string.IsNullOrEmpty(currentName))
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
                    var et = Utility.DBType2EdmType(reader["DATA_TYPE"].ToString());
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
                            parentNav.PrincipalProperties = principalProperties;
                            parentNav.DependentProperties = dependentProperties;
                            //var np = parent.AddBidirectionalNavigation(refrenceNav, parentNav);
                            //var parentSet = model.EntityContainer.FindEntitySet(parentName) as EdmEntitySet;
                            //var referenceSet = model.EntityContainer.FindEntitySet(refrenceName) as EdmEntitySet;
                            //parentSet.AddNavigationTarget(np, referenceSet);
                            var np = refrence.AddBidirectionalNavigation(parentNav, refrenceNav);
                            var parentSet = model.EntityContainer.FindEntitySet(parentName) as EdmEntitySet;
                            var referenceSet = model.EntityContainer.FindEntitySet(refrenceName) as EdmEntitySet;
                            referenceSet.AddNavigationTarget(np, parentSet);


                        }
                        parentName = reader["ParentName"].ToString();
                        refrenceName = reader["RefrencedName"].ToString();
                        parent = model.FindDeclaredType(string.Format("ns.{0}", parentName)) as EdmEntityType;
                        refrence = model.FindDeclaredType(string.Format("ns.{0}", refrenceName)) as EdmEntityType;
                        parentNav = new EdmNavigationPropertyInfo
                        {
                            Name = parentName,
                            TargetMultiplicity = EdmMultiplicity.Many,
                            Target = parent
                        };
                        refrenceNav = new EdmNavigationPropertyInfo
                        {
                            Name = refrenceName,
                            TargetMultiplicity = EdmMultiplicity.Many
                        };
                        //refrenceNav.Target = refrence;
                        principalProperties = new List<IEdmStructuralProperty>();
                        dependentProperties = new List<IEdmStructuralProperty>();
                    }
                    principalProperties.Add(parent.FindProperty(reader["ParentColumnName"].ToString()) as IEdmStructuralProperty);
                    dependentProperties.Add(refrence.FindProperty(reader["RefreancedColumnName"].ToString()) as IEdmStructuralProperty);
                }, null, CommandType.Text);
                if (refrenceNav != null)
                {
                    parentNav.PrincipalProperties = principalProperties;
                    parentNav.DependentProperties = dependentProperties;

                    //var np1 = parent.AddBidirectionalNavigation(refrenceNav, parentNav);
                    //var parentSet1 = model.EntityContainer.FindEntitySet(parentName) as EdmEntitySet;
                    //var referenceSet1 = model.EntityContainer.FindEntitySet(refrenceName) as EdmEntitySet;
                    //parentSet1.AddNavigationTarget(np1, referenceSet1);

                    var np1 = refrence.AddBidirectionalNavigation(parentNav, refrenceNav);
                    var parentSet1 = model.EntityContainer.FindEntitySet(parentName) as EdmEntitySet;
                    var referenceSet1 = model.EntityContainer.FindEntitySet(refrenceName) as EdmEntitySet;
                    referenceSet1.AddNavigationTarget(np1, parentSet1);
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
                    var et = Utility.DBType2EdmType(reader["ColumnType"].ToString());
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

        internal virtual string BuildSqlQueryCmd(ODataQueryOptions options, List<SqlParameter> pars, string target = "")
        {
            var cxt = options.Context;
            string table = target;
            if (string.IsNullOrEmpty(target))
                table = string.Format("[{0}]", cxt.Path.Segments[0].Identifier);
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
        internal virtual string BuildSqlQueryCmd(ExpandedNavigationSelectItem expanded, string condition, List<SqlParameter> pars)
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
                    skip = string.Format("OFFSET {0} ROWS", expanded.SkipOption.Value);
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
                , expanded.ParseFilter(condition, pars)
                , expanded.ParseOrderBy()
                , skip
                , fetch);
            return cmdtxt;
        }

        EdmEntityObjectCollection Get(IEdmCollectionType edmType, string sqlCmd, List<SqlParameter> pars, List<ExpandedNavigationSelectItem> expands = null)
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
                            foreach (NavigationPropertySegment item in expanded.PathToNavigationProperty)
                            {
                                foreach (var p in item.NavigationProperty.ReferentialConstraint.PropertyPairs)
                                {
                                    condition.Add(p.packCondition(reader[p.DependentProperty.Name]));
                                }
                            }
                            List<SqlParameter> exPars = new List<SqlParameter>();
                            string expandCmd = BuildSqlQueryCmd(expanded, string.Join(" and ", condition), exPars);
                            var ss = Get(expanded.NavigationSource.Type as IEdmCollectionType, expandCmd, exPars);
                            bool t = entity.TrySetPropertyValue(expanded.NavigationSource.Name, ss);
                        }
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
        static string BuildTVFTarget(IEdmFunction func, JObject parameterValues)
        {
            //TODO: SQL injection issue
            string templete = "[{0}]({1})";
            List<string> ps = new List<string>();
            foreach (var p in func.Parameters)
            {
                if (p.Type.IsGuid()
                || p.Type.IsString()
                || p.Type.IsDateTimeOffset())
                    ps.Add(string.Format("'{0}'", parameterValues[p.Name].ToString()));
                else
                    ps.Add(parameterValues[p.Name].ToString());
            }
            return string.Format(templete, func.Name, string.Join(",", ps));
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
        public Action<RequestInfo> AfrerExcute { get; set; }
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
            var cxt = queryOptions.Context;
            var entityType = cxt.ElementType as EdmEntityType;
            var table = entityType.Name;
            var edmType = cxt.Path.EdmType as IEdmCollectionType;

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
            List<SqlParameter> pars = new List<SqlParameter>();
            string sqlCmd = BuildSqlQueryCmd(queryOptions, pars);
            return Get(edmType, sqlCmd, pars, expands);
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
            bool needExpand = string.IsNullOrEmpty(queryOptions.SelectExpand.RawExpand);
            using (DbAccess db = new DbAccess(this.ConnectionString))
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
                        par.AddWithValue("@" + keyDefine.Name, key.ChangeType(keyDefine.Type.PrimitiveKind()));
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
                string cmdtxt = BuildExpandQueryString(edmEntity, expanded, out List<SqlParameter> pars);
                EdmEntityObjectCollection collection = CreateExpandEntity(expanded, cmdtxt, pars);
                edmEntity.TrySetPropertyValue(expanded.NavigationSource.Name, collection);
            }
        }
        private EdmEntityObjectCollection CreateExpandEntity(ExpandedNavigationSelectItem expanded, string cmdtxt, List<SqlParameter> pars)
        {
            var edmType = expanded.NavigationSource.Type as IEdmCollectionType;
            var entityType = edmType.ElementType.AsEntity();
            EdmEntityObjectCollection collection = new EdmEntityObjectCollection(new EdmCollectionTypeReference(edmType));
            using (DbAccess db = new DbAccess(this.ConnectionString))
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

            return collection;
        }

        private static string BuildExpandQueryString(EdmEntityObject edmEntity, ExpandedNavigationSelectItem expanded, out List<SqlParameter> pars)
        {
            string cmdSql = "select {0} {1} from [{2}] where {3} {4} {5} {6}";
            string table = string.Empty;
            string top = string.Empty;
            string skip = string.Empty;
            string fetch = string.Empty;
            string where = string.Empty;
            string safeVar = string.Empty;
            pars = new List<SqlParameter>();
            foreach (NavigationPropertySegment item2 in expanded.PathToNavigationProperty)
            {
                foreach (var p in item2.NavigationProperty.ReferentialConstraint.PropertyPairs)
                {
                    edmEntity.TryGetPropertyValue(p.DependentProperty.Name, out object v);
                    safeVar = Utility.SafeSQLVar(p.DependentProperty.Name);
                    pars.Add(new SqlParameter(safeVar, v));
                }
            }
            where = string.Join("and", pars.ConvertAll<string>(p => p.ParameterName));
            table = expanded.NavigationSource.Name;
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
                , table
                , where
                , expanded.ParseFilter(pars)
                , expanded.ParseOrderBy()
                , skip
                , fetch);
        }

        public int GetCount(ODataQueryOptions queryOptions)
        {
            var cxt = queryOptions.Context;
            var entityType = cxt.ElementType as EdmEntityType;

            object rtv = null;
            List<SqlParameter> pars = new List<SqlParameter>();
            string sqlCmd = BuildSqlQueryCmd(queryOptions, pars);
            using (DbAccess db = new DbAccess(this.ConnectionString))
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
            var target = BuildTVFTarget(func, parameterValues);
            List<SqlParameter> pars = new List<SqlParameter>();
            var cmd = BuildSqlQueryCmd(queryOptions, pars, target);
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                var r = db.ExecuteScalar(
                    cmd,
                    (parBuider) => { parBuider.AddRange(pars.ToArray()); },
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
            var target = BuildTVFTarget(func, parameterValues);
            List<SqlParameter> pars = new List<SqlParameter>();
            var cmd = BuildSqlQueryCmd(queryOptions, pars, target);
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
                },
                (parBuilder) => { parBuilder.AddRange(pars.ToArray()); },
                CommandType.Text);
            }
            return collection;

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
            string cmdTemplate = "update [{0}] set {1} where [{2}]=@{2}  ";
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
        public IEdmObject DoAction(IEdmAction action, JObject parameterValues)
        {
            IEdmComplexType edmType = action.ReturnType.Definition as IEdmComplexType;
            var colltype = edmType.FindProperty("$result").Type.Definition;
            IEdmComplexType elementType = (colltype as IEdmCollectionType).ElementType.Definition as IEdmComplexType;

            EdmComplexObject rtv = new EdmComplexObject(edmType);
            rtv.TryGetPropertyValue("$result", out object obj);
            EdmComplexObjectCollection collection = obj as EdmComplexObjectCollection;
            using (DbAccess db = new DbAccess(this.ConnectionString))
            {
                var par = db.ExecuteReader(action.Name, (reader) =>
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
