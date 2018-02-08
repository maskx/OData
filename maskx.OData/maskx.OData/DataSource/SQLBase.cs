using maskx.Database;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace maskx.OData.DataSource
{
    public abstract class SQLBase : IDataSource
    {
        #region Constructor
        protected SQLBase(string name, string connectionString) : this(name)
        {
            this.ConnectionString = connectionString;
        }
        protected SQLBase(string name)
        {
            this.Name = name;
            this.Configuration = new Configuration();
        }
        #endregion

        #region member
        public string ConnectionString { get; set; }
        private EdmComplexTypeReference _DefaultSPReturnType;
        #endregion

        #region private Method
        private EdmModel GetEdmModel()
        {
            var model = new EdmModel();
            var container = new EdmEntityContainer("ns", Name);
            model.AddElement(container);
            AddEdmElement(model);
            AddEdmAction(model);
            AddTableValuedFunction(model);
            AddRelationship(model);
            return model;
        }
        void AddEdmElement(EdmModel model)
        {
            AddEdmElement(model, GetTables());
            AddEdmElement(model, GetViews());
        }
        void AddEdmElement(EdmModel model, IEnumerable<(string SchemaName, string TableName, string ColumnName, string DataType, bool isKey)> Items)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string entityName = string.Empty;
            EdmEntityType t = null;
            IEdmEntitySet edmSet = null;
            foreach (var (SchemaName, TableName, ColumnName, DataType, isKey) in Items)
            {
                entityName = Configuration.DefaultSchema == SchemaName ? TableName : string.Format("{0}.{1}", SchemaName, TableName);
                if (Configuration.LowerName)
                    entityName = entityName.ToLower();
                if (t == null || t.Name != TableName || t.Namespace != SchemaName)
                {
                    edmSet = container.FindEntitySet(entityName);
                    if (edmSet == null)
                    {
                        t = new EdmEntityType(SchemaName, TableName);
                        model.AddElement(t);
                        container.AddEntitySet(entityName, t);
                    }
                    else
                        t = edmSet.EntityType() as EdmEntityType;
                }
                var et = GetEdmType(DataType);
                if (et.HasValue)
                {
                    EdmStructuralProperty key = t.AddStructuralProperty(Configuration.LowerName ? ColumnName.ToLower() : ColumnName, et.Value, true);
                    if (isKey)
                    {
                        t.AddKeys(key);
                    }
                }
            }
        }
        void AddEdmAction(EdmModel model)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string currentName = string.Empty;
            string currentNs = string.Empty;
            string entityName = string.Empty;
            List<(string Name, IEdmTypeReference Type)> pars = new List<(string Name, IEdmTypeReference type)>();
            List<(string Name, IEdmTypeReference Type)> outParameter = new List<(string Name, IEdmTypeReference type)>();
            EdmComplexType root = new EdmComplexType(model.EntityContainer.Namespace, "ActionResultSet", null, false, true);
            model.AddElement(root);
            _DefaultSPReturnType = new EdmComplexTypeReference(root, true);
            foreach (var (SchemaName, StoredProcedureName, ParameterName, ParameterDataType, ParemeterMode, UserDefinedTypeSchema, UserDefinedTypeName, MaxLength, NumericScale) in GetStoredProcedures())
            {
                if (string.IsNullOrEmpty(currentName))
                {
                    currentName = StoredProcedureName;
                    currentNs = SchemaName;
                }
                if (currentNs != SchemaName || currentName != StoredProcedureName)
                {
                    if (outParameter.Count > 0)
                    {
                        EdmComplexType t = new EdmComplexType(model.EntityContainer.Namespace, string.Format("{0}_{1}_Result", currentNs, currentName), null, false, true);
                        foreach (var p in outParameter)
                        {
                            t.AddStructuralProperty(p.Name, p.Type);
                        }
                        model.AddElement(t);
                        var tr = new EdmComplexTypeReference(t, true);
                        CreateAction(model, tr, currentNs, currentName, pars);
                    }
                    else
                    {
                        CreateAction(model, _DefaultSPReturnType, currentNs, currentName, pars);
                    }
                    currentName = StoredProcedureName;
                    currentNs = SchemaName;
                    pars.Clear();
                    outParameter.Clear();
                }
                if (string.IsNullOrEmpty(ParameterName))
                    continue;
                var et = GetEdmType(ParameterDataType);
                if (et.HasValue)
                {
                    var t = EdmCoreModel.Instance.GetPrimitive(et.Value, true);
                    var pname = Configuration.LowerName ? ParameterName.ToLower() : ParameterName;
                    pars.Add((pname, t));
                    if (ParemeterMode.EndsWith("OUT", StringComparison.InvariantCultureIgnoreCase))
                        outParameter.Add((pname, t));
                }
                else if (!string.IsNullOrEmpty(UserDefinedTypeName))//UDT
                {
                    var udt = BuildUserDefinedType(UserDefinedTypeSchema, UserDefinedTypeName);
                    pars.Add((Configuration.LowerName ? ParameterName.ToLower() : ParameterName, udt));
                }
            }
            if (outParameter.Count > 0)
            {
                EdmComplexType t = new EdmComplexType(model.EntityContainer.Namespace, string.Format("{0}_{1}_Result", currentNs, currentName), null, false, true);
                foreach (var p in outParameter)
                {
                    t.AddStructuralProperty(p.Name, p.Type);
                }
                outParameter.Clear();
                model.AddElement(t);
                var tr = new EdmComplexTypeReference(t, true);
                CreateAction(model, tr, currentNs, currentName, pars);
            }
            else
                CreateAction(model, _DefaultSPReturnType, currentNs, currentName, pars);

        }

        private EdmAction CreateAction(EdmModel model, EdmComplexTypeReference returType, string SchemaName, string StoredProcedureName, List<(string name, IEdmTypeReference type)> pars)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            var action = new EdmAction(SchemaName, StoredProcedureName, returType, false, null);
            string pName = string.Empty;
            foreach (var item in pars)
            {
                //remove the prefix such as @ or ?
                pName = item.name.Remove(0, 1);
                if (Configuration.LowerName)
                    pName = pName.ToLower();
                action.AddParameter(pName, item.type);
            }
            model.AddElement(action);
            string entityName = action.Namespace == Configuration.DefaultSchema ? action.Name : string.Format("{0}.{1}", action.Namespace, action.Name);
            if (Configuration.LowerName)
                entityName = entityName.ToLower();
            container.AddActionImport(entityName, action, null);
            return action;
        }

        EdmComplexTypeReference BuildUserDefinedType(string schema, string name)
        {
            EdmComplexType root = new EdmComplexType(schema, name);
            foreach (var (ColumnName, DataType, Length, isNullable) in GetUserDefinedType(schema, name))
            {
                var et = GetEdmType(DataType);
                if (et.HasValue)
                {
                    root.AddStructuralProperty(Configuration.LowerName ? ColumnName.ToLower() : ColumnName, et.Value);
                }
            }
            return new EdmComplexTypeReference(root, false);
        }
        EdmCollectionTypeReference BuildTableValueType(EdmModel model, string schema, string name)
        {
            EdmComplexType root = new EdmComplexType(schema, String.Format("{0}_RtvType", name));
            foreach (var (ColumnName, DataType, Length, isNullable) in GetTableValueType(schema, name))
            {
                var et = GetEdmType(DataType);
                if (et.HasValue)
                {
                    root.AddStructuralProperty(Configuration.LowerName ? ColumnName.ToLower() : ColumnName, et.Value);
                }
            }
            var etr = new EdmComplexTypeReference(root, true);
            var t1 = new EdmCollectionTypeReference(new EdmCollectionType(etr));
            model.AddElement((t1.Definition as EdmCollectionType).ElementType.Definition as IEdmSchemaElement);

            return t1;
        }
        void AddTableValuedFunction(EdmModel model)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string currentName = string.Empty;
            string currentNS = string.Empty;
            string entityName = string.Empty;
            EdmFunction func = null;
            foreach (var (SchemaName, FunctionName, ParameterName, ParameterDataType, UserDefinedTypeSchema, UserDefinedTypeName, MaxLength, NumericScale) in GetFunctions())
            {
                if (string.IsNullOrEmpty(currentName) || currentName != FunctionName || currentNS != SchemaName)
                {
                    entityName = SchemaName == Configuration.DefaultSchema ? FunctionName : string.Format("{0}.{1}", SchemaName, FunctionName);
                    if (Configuration.LowerName)
                        entityName = entityName.ToLower();
                    var t = BuildTableValueType(model, SchemaName, FunctionName);
                    func = new EdmFunction(SchemaName, FunctionName, t, false, null, true);
                    container.AddFunctionImport(entityName, func, null, true);
                    model.AddElement(func);
                }
                if (string.IsNullOrEmpty(ParameterDataType))
                    continue;
                var et = GetEdmType(ParameterDataType);
                if (et.HasValue)
                {
                    var t = EdmCoreModel.Instance.GetPrimitive(et.Value, true);
                    var p = ParameterName.Remove(0, 1);

                    func.AddParameter(Configuration.LowerName ? p.ToLower() : p, t);
                }
                else if (string.IsNullOrEmpty(UserDefinedTypeName))//UDT
                {
                    var udt = BuildUserDefinedType(UserDefinedTypeSchema, UserDefinedTypeName);
                    func.AddParameter(Configuration.LowerName ? ParameterName.ToLower() : ParameterName, udt);
                }
            }

        }
        void AddRelationship(EdmModel model)
        {
            string fk = string.Empty;
            string parentSchemaName = string.Empty;
            string referencedSchemaName = string.Empty;
            string parentName = string.Empty;
            string referencedName = string.Empty;
            string parentColName = string.Empty;
            string referencedColName = string.Empty;

            EdmEntityType parent = null;
            EdmEntityType refrenced = null;
            List<IEdmStructuralProperty> principalProperties = null;
            List<IEdmStructuralProperty> dependentProperties = null;

            foreach (var (ForeignKeyName, ParentSchemaName, ParentName, ParentColumnName, RefrencedName, RefrencedSchemaName, RefrencedColumnName) in GetRelationship())
            {
                if (fk != ForeignKeyName)
                {
                    if (parent != null)
                    {
                        CreatReference(model, parent, refrenced, principalProperties, dependentProperties);
                    }
                    parent = model.FindDeclaredType(string.Format("{0}.{1}", ParentSchemaName, ParentName)) as EdmEntityType;
                    refrenced = model.FindDeclaredType(string.Format("{0}.{1}", RefrencedSchemaName, RefrencedName)) as EdmEntityType;

                    principalProperties = new List<IEdmStructuralProperty>();
                    dependentProperties = new List<IEdmStructuralProperty>();
                    fk = ForeignKeyName;
                }
                principalProperties.Add(parent.FindProperty(ParentColumnName) as IEdmStructuralProperty);
                dependentProperties.Add(refrenced.FindProperty(RefrencedColumnName) as IEdmStructuralProperty);
            }
            CreatReference(model, parent, refrenced, principalProperties, dependentProperties);

        }

        private void CreatReference(EdmModel model, EdmEntityType parent, EdmEntityType refrenced, List<IEdmStructuralProperty> principalProperties, List<IEdmStructuralProperty> dependentProperties)
        {
            var parentEntityName = parent.Namespace == Configuration.DefaultSchema ? parent.Name : string.Format("{0}.{1}", parent.Namespace, parent.Name);
            var referencedEntityName = refrenced.Namespace == Configuration.DefaultSchema ? refrenced.Name : string.Format("{0}.{1}", refrenced.Namespace, refrenced.Name);
            if (Configuration.LowerName)
            {
                parentEntityName = parentEntityName.ToLower();
                referencedEntityName = referencedEntityName.ToLower();
            }

            var parentNav = new EdmNavigationPropertyInfo
            {
                Name = parent.Name,
                TargetMultiplicity = EdmMultiplicity.Many,
                Target = parent
            };
            var referencedNav = new EdmNavigationPropertyInfo
            {
                Name = refrenced.Name,
                TargetMultiplicity = EdmMultiplicity.Many
            };

            parentNav.PrincipalProperties = principalProperties;
            parentNav.DependentProperties = dependentProperties;

            var np = refrenced.AddBidirectionalNavigation(parentNav, referencedNav);
            var parentSet = model.EntityContainer.FindEntitySet(parentEntityName) as EdmEntitySet;
            var referenceSet = model.EntityContainer.FindEntitySet(referencedEntityName) as EdmEntitySet;
            referenceSet.AddNavigationTarget(np, parentSet);
        }
        #endregion

        #region abstract method
        protected abstract IEnumerable<(string ForeignKeyName,
            string ParentSchemaName,
            string ParentName,
            string ParentColumnName,
            string RefrencedName,
            string RefrencedSchemaName,
            string RefrencedColumnName

            )> GetRelationship();

        /// <summary>
        /// Should order by SchemaName and TableName
        /// </summary>
        /// <returns></returns>
        protected abstract IEnumerable<(string SchemaName,
            string TableName,
            string ColumnName,
            string DataType,
            bool isKey)> GetTables();
        /// <summary>
        /// should order by SchemaName and ViewName
        /// </summary>
        /// <returns></returns>
        protected abstract IEnumerable<(string SchemaName,
            string ViewName,
            string ColumnName,
            string DataType,
            bool isKey)> GetViews();
        /// <summary>
        /// should order by SchemaName and StoredProcedureName
        /// </summary>
        /// <returns></returns>
        protected abstract IEnumerable<(string SchemaName,
            string StoredProcedureName,
            string ParameterName,
            string ParameterDataType,
            string ParemeterMode,
            string UserDefinedTypeSchema,
            string UserDefinedTypeName,
            int MaxLength,
            int NumericScale)> GetStoredProcedures();

        protected abstract IEnumerable<(string ColumnName,
            string DataType,
            int Length,
            bool isNullable)> GetUserDefinedType(string schema, string name);
        /// <summary>
        /// should order by SchemaName and FunctionName
        /// </summary>
        /// <returns></returns>
        protected abstract IEnumerable<(string SchemaName,
            string FunctionName,
            string ParameterName,
            string ParameterDataType,
            string UserDefinedTypeSchema,
            string UserDefinedTypeName,
            int MaxLength,
            int NumericScale
            )> GetFunctions();
        protected abstract IEnumerable<(string ColumnName,
            string DataType,
            int Length,
            bool isNullable)> GetTableValueType(string schema, string name);
        protected abstract EdmPrimitiveTypeKind? GetEdmType(string dbType);
        #endregion

        #region IDataSource
        public Configuration Configuration { get; set; }

        public string Name { get; private set; }

        public EdmModel Model
        {
            get
            {
                return GetEdmModel();
            }
        }

        public Action<RequestInfo> BeforeExcute { get; set; }
        public Func<RequestInfo, object, object> AfrerExcute { get; set; }

        public string Create(IEdmEntityObject entity)
        {
            List<DbParameter> pars = new List<DbParameter>();
            string cmdTxt = BuildCreateCmd(entity, pars);
            object rtv = 0;
            using (var db = CreateDbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteScalar(cmdTxt, (dbpars) =>
                    {
                        dbpars.AddRange(pars.ToArray());
                    }, CommandType.Text);
            }
            return rtv.ToString();
        }

        public int Delete(string key, IEdmType elementType)
        {
            List<DbParameter> pars = new List<DbParameter>();
            string cmdTxt = BuildDeleteCmd(key, elementType, pars);
            int rtv = 0;
            using (var db = CreateDbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteNonQuery(cmdTxt, (parBuilder) =>
                      {
                          parBuilder.AddRange(pars.ToArray());
                      }, CommandType.Text);
            }
            return rtv;
        }

        public IEdmObject DoAction(IEdmAction action, JObject parameterValues)
        {
            IEdmComplexType edmType = action.ReturnType.Definition as IEdmComplexType;
            EdmComplexObject rtv = new EdmComplexObject(edmType);
            EdmComplexObject r = null;

            var b = new EdmCollectionTypeReference(new EdmCollectionType(_DefaultSPReturnType));
            EdmComplexObjectCollection rList = null;
            int resultSetIndex = -1;
            using (var db = CreateDbAccess(this.ConnectionString))
            {
                var par = db.ExecuteReader(
                    string.Format("{0}.{1}",
                        _DbUtility.SafeDbObject(action.Namespace),
                        _DbUtility.SafeDbObject(action.Name)),
                    (reader, resultSet) =>
                    {
                        if (resultSet != resultSetIndex)
                        {
                            rList = new EdmComplexObjectCollection(b);
                            rtv.TrySetPropertyValue("$ResultSet" + resultSet, rList);
                            resultSetIndex = resultSet;
                        }
                        r = new EdmComplexObject(_DefaultSPReturnType);
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            reader.SetEntityPropertyValue(i, r);
                        }
                        rList.Add(r);
                    }, (pars) =>
                    {
                        SetParameter(action, parameterValues, pars);
                    });
                foreach (var outp in edmType.Properties())
                {
                    var v = par[outp.Name].Value;
                    if (DBNull.Value != v)
                        rtv.TrySetPropertyValue(outp.Name, v);
                }
            }
            return rtv;
        }
        void SetParameter(IEdmAction action, JObject parameterValues, DbParameterCollection pars)
        {
            if (parameterValues == null)
                return;
            JToken token = null;
            Type colType = null;
            DbParameter dbParameter = null;
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
                        dbParameter = _DbUtility.CreateParameter(p.Name, dt);
                        pars.Add(dbParameter);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(token.ToString()))
                            dbParameter = _DbUtility.CreateParameter(p.Name, DBNull.Value);
                        else
                            dbParameter = _DbUtility.CreateParameter(p.Name, token.ToString().ChangeType(p.Type.PrimitiveKind()));
                        pars.Add(dbParameter);
                    }
                }
            }
            if (edmType.TypeKind == EdmTypeKind.Entity)
            {
                foreach (var outp in (edmType as EdmEntityType).Properties())
                {
                    if (pars.Contains(outp.Name))
                    {
                        pars[outp.Name].Direction = ParameterDirection.Output;
                    }
                    else
                    {
                        dbParameter = _DbUtility.CreateParameter(outp.Name, DBNull.Value);
                        dbParameter.Direction = ParameterDirection.Output;
                        pars.Add(dbParameter);
                    }
                }
            }
        }

        public EdmEntityObjectCollection Get(ODataQueryOptions queryOptions)
        {
            var cxt = queryOptions.Context;
            EdmEntityType entityType = cxt.ElementType as EdmEntityType;
            IEdmCollectionType edmType = cxt.Path.EdmType as IEdmCollectionType;
            List<DbParameter> pars = new List<DbParameter>();
            string sqlCmd = BuildQueryCmd(queryOptions, pars);
            EdmEntityObjectCollection collection = new EdmEntityObjectCollection(new EdmCollectionTypeReference(edmType));
            bool needExpand = queryOptions.SelectExpand != null && !string.IsNullOrEmpty(queryOptions.SelectExpand.RawExpand);

            using (var db = CreateDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(sqlCmd, (reader, resultSet) =>
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
            List<DbParameter> pars = new List<DbParameter>();
            string cmdTxt = BuildQueryByKeyCmd(key, queryOptions, pars);
            EdmEntityObject entity = new EdmEntityObject(entityType);
            bool needExpand = queryOptions.SelectExpand != null && !string.IsNullOrEmpty(queryOptions.SelectExpand.RawExpand);
            using (var db = CreateDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(cmdTxt,
                    (reader, resultSet) =>
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
                        par.AddRange(pars.ToArray());
                    },
                    CommandType.Text);
            }
            return entity;
        }

        public int GetCount(ODataQueryOptions queryOptions)
        {
            var cxt = queryOptions.Context;
            var entityType = cxt.ElementType as EdmEntityType;

            object rtv = null;
            List<DbParameter> pars = new List<DbParameter>();
            string sqlCmd = BuildQueryCmd(queryOptions, pars);
            using (var db = CreateDbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteScalar(sqlCmd,
                    (parBuilder) => { parBuilder.AddRange(pars.ToArray()); },
                    CommandType.Text);
            }
            if (rtv == null)
                return 0;
            return (int)rtv;
        }

        public int GetFuncResultCount(ODataQueryOptions queryOptions)
        {
            int count = 0;
            var ois = queryOptions.Context.Path.Segments.First() as OperationImportSegment;
            var func = ois.OperationImports.First().Operation;
            IEdmType edmType = func.ReturnType.Definition;
            List<DbParameter> sqlpars = new List<DbParameter>();
            var cmd = BuildQueryCmd(queryOptions, sqlpars);
            using (var db = CreateDbAccess(this.ConnectionString))
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

        public IEdmObject InvokeFunction(ODataQueryOptions queryOptions)
        {
            var ois = queryOptions.Context.Path.Segments.First() as OperationImportSegment;
            var func = ois.OperationImports.First().Operation;
            IEdmType edmType = func.ReturnType.Definition;
            IEdmType elementType = (edmType as IEdmCollectionType).ElementType.Definition;
            EdmComplexObjectCollection collection = new EdmComplexObjectCollection(new EdmCollectionTypeReference(edmType as IEdmCollectionType));
            List<DbParameter> sqlpars = new List<DbParameter>();
            var cmd = BuildQueryCmd(queryOptions, sqlpars);
            using (var db = CreateDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(cmd, (reader, resultSet) =>
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
            if (!(entity as Delta).GetChangedPropertyNames().Any())
                return 0;
            List<DbParameter> sqlpars = new List<DbParameter>();
            string cmdTxt = BuildMergeCmd(key, entity, sqlpars);
            int rtv;
            using (var db = CreateDbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteNonQuery(cmdTxt
                    , (dbpars) =>
                    {
                        dbpars.AddRange(sqlpars.ToArray());
                    }, CommandType.Text);
            }
            return rtv;
        }

        public int Replace(string key, IEdmEntityObject entity)
        {
            List<DbParameter> pars = new List<DbParameter>();
            string cmdTxt = BuildReplaceCmd(key, entity, pars);
            int rtv;
            using (var db = CreateDbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteNonQuery(cmdTxt, (dbpars) =>
                {
                    dbpars.AddRange(pars.ToArray());
                }, CommandType.Text);
            }
            return rtv;
        }
        #endregion

        protected abstract DbAccess CreateDbAccess(string connectionString);
        protected abstract DbUtility _DbUtility { get; }
        protected abstract string GetCmdTemplete(MethodType methodType);
        protected abstract string GetCmdTemplete(MethodType methodType, ODataQueryOptions options);
        protected abstract string GetCmdTemplete(MethodType methodType, ExpandedNavigationSelectItem expanded);


        protected virtual string BuildExpandQueryCmd(EdmEntityObject edmEntity, ExpandedNavigationSelectItem expanded, List<DbParameter> pars)
        {
            var entityType = expanded.NavigationSource.EntityType();
            var wp = new List<string>();
            foreach (NavigationPropertySegment item2 in expanded.PathToNavigationProperty)
            {
                foreach (var p in item2.NavigationProperty.ReferentialConstraint.PropertyPairs)
                {
                    edmEntity.TryGetPropertyValue(p.DependentProperty.Name, out object v);
                    var par = _DbUtility.CreateParameter(v, pars);
                    wp.Add(string.Format("{0}={1}", _DbUtility.SafeDbObject(p.PrincipalProperty.Name), par.ParameterName));
                }
            }
            string where = " where " + string.Join("and", wp);

            return string.Format(GetCmdTemplete(MethodType.Get, expanded)
                , expanded.TopOption.HasValue ? expanded.TopOption.Value.ToString() : string.Empty
                , expanded.ParseSelect(_DbUtility)
                , entityType.Namespace
                , entityType.Name
                , where
                , expanded.ParseFilter(pars, _DbUtility)
                , expanded.ParseOrderBy(_DbUtility)
                , expanded.SkipOption.HasValue ? expanded.SkipOption.Value.ToString() : string.Empty);
        }

        protected virtual string BuildQueryCmd(ODataQueryOptions options, List<DbParameter> pars)
        {
            var cxt = options.Context;
            string ns, name;
            if (cxt.Path.Segments.First() is OperationImportSegment ois)
            {
                var func = ois.OperationImports.First().Operation;
                DbParameter par;
                List<DbParameter> funcPList = new List<DbParameter>();
                foreach (var item in ois.Parameters)
                {
                    par = _DbUtility.CreateParameter((item.Value as ConstantNode).Value, pars);
                    funcPList.Add(par);
                }
                ns = func.Namespace;
                name = string.Format("{0}({1})", _DbUtility.SafeDbObject(func.Name), string.Join(",", funcPList.ConvertAll<string>(p => p.ParameterName)));
            }
            else
            {
                var entityType = cxt.ElementType as EdmEntityType;
                ns = _DbUtility.SafeDbObject(entityType.Namespace);
                name = _DbUtility.SafeDbObject(entityType.Name);
            }

            return string.Format(GetCmdTemplete(MethodType.Get, options)
                , options.Top?.RawValue
                , options.ParseSelect(_DbUtility)
                , ns
                , name
                , options.ParseFilter(pars, _DbUtility)
                , options.ParseOrderBy(_DbUtility)
                , options.Skip?.RawValue);
        }

        protected virtual string BuildCreateCmd(IEdmEntityObject entity, List<DbParameter> pars)
        {
            var edmType = entity.GetEdmType();
            var entityType = edmType.Definition as EdmEntityType;

            List<string> cols = new List<string>();
            List<string> ps = new List<string>();
            object v = null;
            foreach (var p in (entity as Delta).GetChangedPropertyNames())
            {
                entity.TryGetPropertyValue(p, out v);
                cols.Add(_DbUtility.SafeDbObject(p));
                var par = _DbUtility.CreateParameter(v, pars);
                ps.Add(par.ParameterName);
            }
            return string.Format(GetCmdTemplete(MethodType.Create),
               _DbUtility.SafeDbObject(entityType.Namespace),
               _DbUtility.SafeDbObject(entityType.Name),
                string.Join(", ", cols),
                string.Join(", ", ps));
        }
        protected virtual string BuildQueryByKeyCmd(string key, ODataQueryOptions options, List<DbParameter> pars)
        {
            var cxt = options.Context;
            var entityType = cxt.ElementType as EdmEntityType;
            var keyDefine = entityType.DeclaredKey.First();
            string cmdSql = "select {0} from {1} where {2}={3}";
            var par = _DbUtility.CreateParameter(key.ChangeType(keyDefine.Type.PrimitiveKind()), pars);

            return string.Format(cmdSql
                , options.ParseSelect(_DbUtility)
                , _DbUtility.SafeDbObject(entityType.Name)
                , _DbUtility.SafeDbObject(keyDefine.Name),
                par.ParameterName);
        }
        protected virtual string BuildDeleteCmd(string key, IEdmType elementType, List<DbParameter> pars)
        {
            var entityType = elementType as EdmEntityType;
            var keyDefine = entityType.DeclaredKey.First();
            var par = _DbUtility.CreateParameter(key.ChangeType(keyDefine.Type.PrimitiveKind()), pars);
            return string.Format("delete from  {0}.{1} where {2}={3};",
               _DbUtility.SafeDbObject(entityType.Namespace),
               _DbUtility.SafeDbObject(entityType.Name),
               _DbUtility.SafeDbObject(keyDefine.Name),
               par.ParameterName);
        }
        protected virtual string BuildReplaceCmd(string key, IEdmEntityObject entity, List<DbParameter> pars)
        {
            string cmdTemplate = "update {0}.{1} set {2} where {3}={4}";
            var entityType = entity.GetEdmType().Definition as EdmEntityType;
            var keyDefine = entityType.DeclaredKey.First();
            List<string> cols = new List<string>();
            DbParameter par = null;
            object v = null;

            foreach (var p in entityType.Properties())
            {
                if (p.PropertyKind == EdmPropertyKind.Navigation) continue;
                if (entity.TryGetPropertyValue(p.Name, out v))
                {
                    if (keyDefine.Name == p.Name) continue;
                    par = _DbUtility.CreateParameter(v, pars);
                    cols.Add(string.Format("{0}={1}", _DbUtility.SafeDbObject(p.Name), par.ParameterName));
                }
            }
            par = _DbUtility.CreateParameter(key.ChangeType(keyDefine.Type.PrimitiveKind()), pars);

            return string.Format(cmdTemplate,
                _DbUtility.SafeDbObject(entityType.Namespace),
                _DbUtility.SafeDbObject(entityType.Name),
                string.Join(",", cols),
               _DbUtility.SafeDbObject(keyDefine.Name),
                par.ParameterName);
        }
        protected virtual string BuildMergeCmd(string key, IEdmEntityObject entity, List<DbParameter> pars)
        {
            string cmdTemplate = "update {0}.{1} set {2} where {3}={4} ";
            var entityType = entity.GetEdmType().Definition as EdmEntityType;
            var keyDefine = entityType.DeclaredKey.First();
            List<string> cols = new List<string>();
            string safep = string.Empty;
            object v = null;
            DbParameter par = null;
            foreach (var p in (entity as Delta).GetChangedPropertyNames())
            {
                if (entity.TryGetPropertyValue(p, out v))
                {
                    par = _DbUtility.CreateParameter(v, pars);
                    cols.Add(string.Format("{0}={1}", _DbUtility.SafeDbObject(p), par.ParameterName));
                }
            }
            par = _DbUtility.CreateParameter(key.ChangeType(keyDefine.Type.PrimitiveKind()), pars);
            return string.Format(cmdTemplate,
               _DbUtility.SafeDbObject(entityType.Namespace),
               _DbUtility.SafeDbObject(entityType.Name),
                string.Join(", ", cols),
               _DbUtility.SafeDbObject(keyDefine.Name),
                par.ParameterName);
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
            List<DbParameter> pars = new List<DbParameter>();
            string cmdtxt = BuildExpandQueryCmd(edmEntity, expanded, pars);
            var edmType = expanded.NavigationSource.Type as IEdmCollectionType;
            var entityType = edmType.ElementType.AsEntity();
            EdmEntityObjectCollection collection = new EdmEntityObjectCollection(new EdmCollectionTypeReference(edmType));
            using (var db = CreateDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(cmdtxt, (reader, resultSet) =>
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

    }
}
