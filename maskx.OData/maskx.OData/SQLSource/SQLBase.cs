using maskx.Database;
using maskx.OData.Extensions;
using maskx.OData.Infrastructure;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter.Value;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace maskx.OData.SQLSource
{
    public abstract class SQLBase : IDataSource
    {
        #region Constructor
        protected SQLBase(string connectionString)
        {
            this.ConnectionString = connectionString;
        }

        #endregion

        #region member
        public string ConnectionString { get; set; }
        public DynamicODataOptions DynamicODataOptions { get; set; }
        private EdmComplexTypeReference _DefaultSPReturnType;
        private readonly NameSpaces _NameSpaces = new NameSpaces();
        #endregion

        #region private Method
        private EdmModel GetEdmModel()
        {
            var model = new EdmModel();
            var container = new EdmEntityContainer("ns", "defaultContainer");
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
            EdmEntityType t = null;
            string currentSchemaName = string.Empty;
            string currentTableName = string.Empty;

            foreach (var (SchemaName, TableName, ColumnName, DataType, isKey) in Items)
            {

                if (t == null || currentTableName != TableName || currentSchemaName != SchemaName)
                {
                    currentSchemaName = SchemaName;
                    currentTableName = TableName;
                    string tName = _NameSpaces.GetByOriginalName(SchemaName).GetByOriginalName(TableName).Name;
                    string sName = _NameSpaces.GetName(SchemaName);
                    string entityName = (this as IDataSource).DynamicODataOptions.DefaultSchema == sName ? tName : string.Format("{0}.{1}", sName, tName);

                    IEdmEntitySet edmSet = container.FindEntitySet(entityName);
                    if (edmSet == null)
                    {
                        t = new EdmEntityType(sName, tName);
                        model.AddElement(t);
                        container.AddEntitySet(entityName, t);
                    }
                    else
                        t = edmSet.EntityType() as EdmEntityType;
                }
                var p = _NameSpaces.GetByOriginalName(currentSchemaName).GetByOriginalName(currentTableName).Properties.GetProperty(ColumnName);
                p.DbType = GetDbType(DataType);
                p.EdmPrimitiveTypeKind = GetEdmType(DataType);
                EdmStructuralProperty key = t.AddStructuralProperty(p.Name, p.EdmPrimitiveTypeKind, true);
                if (isKey)
                {
                    t.AddKeys(key);
                }
            }
        }
        void AddEdmAction(EdmModel model)
        {
            string currentName = string.Empty;
            string currentNs = string.Empty;
            EntityCollection ns = null;
            Entity act = null;
            List<(string Name, IEdmTypeReference Type)> pars = new();
            List<(string Name, IEdmTypeReference Type)> outParameter = new();
            EdmComplexType root = new(model.EntityContainer.Namespace, "ActionResultSet", null, false, true);
            model.AddElement(root);
            _DefaultSPReturnType = new EdmComplexTypeReference(root, true);
            foreach (var (SchemaName, StoredProcedureName, ParameterName, ParameterDataType, ParemeterMode, UserDefinedTypeSchema, UserDefinedTypeName, MaxLength, NumericScale) in GetStoredProcedures())
            {
                if (string.IsNullOrEmpty(currentName))
                {
                    currentName = StoredProcedureName;
                    currentNs = SchemaName;
                    ns = _NameSpaces.GetByOriginalName(currentNs);
                    act = ns.GetByOriginalName(StoredProcedureName);
                }
                if (currentNs != SchemaName || currentName != StoredProcedureName)
                {
                    if (outParameter.Count > 0)
                    {
                        EdmComplexType t = new(model.EntityContainer.Namespace, $"{ns.NameSpace}_{act.Name}_Result", null, false, true);
                        foreach (var p1 in outParameter)
                        {
                            t.AddStructuralProperty(p1.Name, p1.Type);
                        }
                        model.AddElement(t);
                        var tr = new EdmComplexTypeReference(t, true);
                        CreateAction(model, tr, ns.NameSpace, act.Name, pars);
                    }
                    else
                    {
                        CreateAction(model, _DefaultSPReturnType, ns.NameSpace, act.Name, pars);
                    }
                    currentName = StoredProcedureName;
                    currentNs = SchemaName;
                    ns = _NameSpaces.GetByOriginalName(currentNs);
                    act = ns.GetByOriginalName(StoredProcedureName);
                    pars.Clear();
                    outParameter.Clear();
                }
                if (string.IsNullOrEmpty(ParameterName))
                    continue;
                var p = act.Properties.GetProperty(ParameterName);
                p.DbType = GetDbType(ParameterDataType);
                p.EdmPrimitiveTypeKind = GetEdmType(ParameterDataType);
                p.Direction = GetParameterDirection(ParemeterMode);
                if (p.EdmPrimitiveTypeKind != EdmPrimitiveTypeKind.None)
                {
                    var t = EdmCoreModel.Instance.GetPrimitive(p.EdmPrimitiveTypeKind, true);
                    pars.Add((p.Name, t));
                    if (ParemeterMode.EndsWith("OUT", StringComparison.InvariantCultureIgnoreCase))
                        outParameter.Add((p.Name, t));
                }
                else if (!string.IsNullOrEmpty(UserDefinedTypeName))//UDT
                {
                    var udt = BuildUserDefinedType(UserDefinedTypeSchema, UserDefinedTypeName);
                    pars.Add((p.Name, udt));
                }
            }
            if (outParameter.Count > 0)
            {
                EdmComplexType t = new(model.EntityContainer.Namespace, $"{ns.NameSpace}_{act.Name}_Result", null, false, true);
                foreach (var p in outParameter)
                {
                    t.AddStructuralProperty(p.Name, p.Type);
                }
                outParameter.Clear();
                model.AddElement(t);
                var tr = new EdmComplexTypeReference(t, true);
                CreateAction(model, tr, ns.NameSpace, act.Name, pars);
            }
            else
                CreateAction(model, _DefaultSPReturnType, ns.NameSpace, act.Name, pars);

        }

        private EdmAction CreateAction(EdmModel model, EdmComplexTypeReference returType, string nameSpace, string actionName, List<(string name, IEdmTypeReference type)> pars)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            var action = new EdmAction(nameSpace, actionName, returType, false, null);
            foreach (var (name, type) in pars)
            {
                //remove the prefix such as @ or ?
                string pName = name.Remove(0, 1);

                action.AddParameter(pName, type);
            }
            model.AddElement(action);
            string entityName = action.Namespace == DynamicODataOptions.DefaultSchema ? action.Name : $"{action.Namespace}.{action.Name}";

            container.AddActionImport(entityName, action, null);
            return action;
        }

        EdmComplexTypeReference BuildUserDefinedType(string schema, string name)
        {
            var ns = _NameSpaces.GetByOriginalName(schema);
            var entity = ns.GetByOriginalName(name);
            EdmComplexType root = new(ns.NameSpace, entity.Name);
            foreach (var (ColumnName, DataType, _, _) in GetUserDefinedType(schema, name))
            {
                var et = GetEdmType(DataType);
                if (et != EdmPrimitiveTypeKind.None)
                {
                    var p = entity.Properties.GetProperty(ColumnName);
                    p.DbType = GetDbType(DataType);
                    p.EdmPrimitiveTypeKind = GetEdmType(DataType);
                    root.AddStructuralProperty(p.Name, p.EdmPrimitiveTypeKind);
                }
            }
            return new EdmComplexTypeReference(root, false);
        }
        EdmCollectionTypeReference BuildTableValueType(EdmModel model, string schema, string name)
        {
            var ns = _NameSpaces.GetByOriginalName(schema);
            var entity = ns.GetByOriginalName(name);
            EdmComplexType root = new(schema, $"{entity.Name}_RtvType");
            foreach (var (ColumnName, DataType, _, _) in GetTableValueType(schema, name))
            {
                var p = entity.Properties.GetProperty(ColumnName);
                p.EdmPrimitiveTypeKind = GetEdmType(DataType);
                p.DbType = GetDbType(DataType);
                if (p.EdmPrimitiveTypeKind != EdmPrimitiveTypeKind.None)
                {
                    root.AddStructuralProperty(p.Name, p.EdmPrimitiveTypeKind);
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
            EntityCollection ns = null;
            Entity f = null;
            EdmFunction func = null;
            foreach (var (SchemaName, FunctionName, ParameterName, ParameterDataType, UserDefinedTypeSchema, UserDefinedTypeName, MaxLength, NumericScale) in GetFunctions())
            {
                if (string.IsNullOrEmpty(currentName) || currentName != FunctionName || currentNS != SchemaName)
                {
                    ns = _NameSpaces.GetByOriginalName(SchemaName);
                    f = ns.GetByOriginalName(FunctionName);
                    string entityName = ns.NameSpace == DynamicODataOptions.DefaultSchema ? f.Name : $"{ns.NameSpace}.{f.Name}";

                    var t = BuildTableValueType(model, SchemaName, FunctionName);
                    func = new EdmFunction(ns.NameSpace, f.Name, t, false, null, true);
                    container.AddFunctionImport(entityName, func, null, true);
                    model.AddElement(func);
                }
                if (string.IsNullOrEmpty(ParameterDataType))
                    continue;
                var par = f.Properties.GetProperty(ParameterName);
                par.EdmPrimitiveTypeKind = GetEdmType(ParameterDataType);
                par.DbType = GetDbType(ParameterDataType);
                if (par.EdmPrimitiveTypeKind != EdmPrimitiveTypeKind.None)
                {
                    var t = EdmCoreModel.Instance.GetPrimitive(par.EdmPrimitiveTypeKind, true);
                    func.AddParameter(par.Name, t);
                }
                else if (string.IsNullOrEmpty(UserDefinedTypeName))//UDT
                {
                    var udt = BuildUserDefinedType(UserDefinedTypeSchema, UserDefinedTypeName);
                    func.AddParameter(par.Name, udt);
                }
            }

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        void AddRelationship(EdmModel model)
        {
            string fk = string.Empty;
            Entity p_entity = null;
            Entity r_entity = null;
            ForeignKey foreignKey = null;
            List<ForeignKey> foreignKeys = new List<ForeignKey>();
            foreach (var (ForeignKeyName, ParentSchemaName, ParentName, ParentColumnName, ReferencedName, ReferencedSchemaName, ReferencedColumnName) in GetRelationship())
            {
                if (fk != ForeignKeyName)
                {
                    if (p_entity != null)
                        p_entity.Properties.AddForeignKey(foreignKey);
                    foreignKey = new ForeignKey(ForeignKeyName);
                    var p_ns = _NameSpaces.GetByOriginalName(ParentSchemaName);
                    p_entity = p_ns.GetByOriginalName(ParentName);
                    var r_ns = _NameSpaces.GetByOriginalName(ReferencedSchemaName);
                    r_entity = r_ns.GetByOriginalName(ReferencedName);
                    foreignKey.DeclaringEntityType = r_entity;
                    fk = ForeignKeyName;
                    foreignKeys.Add(foreignKey);
                }
                foreignKey.PrincipalProperties.Add(p_entity.Properties.GetProperty(ParentColumnName));
                foreignKey.DeclaringProperties.Add(r_entity.Properties.GetProperty(ReferencedColumnName));
            }
            if (p_entity != null)
                p_entity.Properties.AddForeignKey(foreignKey);
            foreach (var key in foreignKeys)
            {
                CreateReference(model, key);
            }
        }
        private void CreateReference(EdmModel model, ForeignKey foreignKey)
        {
            var parent = model.FindDeclaredType(foreignKey.PrincipalEntityType.FullName) as EdmEntityType;
            var reference = model.FindDeclaredType(foreignKey.DeclaringEntityType.FullName) as EdmEntityType;
            var parentNav = new EdmNavigationPropertyInfo
            {
                Name = foreignKey.PrincipalName,
                TargetMultiplicity = EdmMultiplicity.Many,
                Target = parent
            };

            parentNav.PrincipalProperties = foreignKey.PrincipalProperties.ToEdmProperties(parent);
            parentNav.DependentProperties = foreignKey.DeclaringProperties.ToEdmProperties(reference);
            parent.AddUnidirectionalNavigation(parentNav);

            //////////////////////////////
            ///
 
            //var parent = model.FindDeclaredType(foreignKey.PrincipalEntityType.FullName) as EdmEntityType;
            //var reference = model.FindDeclaredType(foreignKey.DeclaringEntityType.FullName) as EdmEntityType;

            //var parentNav = new EdmNavigationPropertyInfo
            //{
            //    Name = foreignKey.PrincipalName,
            //    TargetMultiplicity = EdmMultiplicity.Many,
            //    Target = parent
            //};
            var referencedNav = new EdmNavigationPropertyInfo
            {
                Name = foreignKey.Name,
                TargetMultiplicity = EdmMultiplicity.Many
            };

            //parentNav.PrincipalProperties = foreignKey.PrincipalProperties.ToEdmProperties(parent);
            //parentNav.DependentProperties = foreignKey.DeclaringProperties.ToEdmProperties(reference);

            var np = reference.AddBidirectionalNavigation(parentNav, referencedNav);
            var parentSet = model.EntityContainer.FindEntitySet(foreignKey.PrincipalEntityType.FullName) as EdmEntitySet;
            var referenceSet = model.EntityContainer.FindEntitySet(foreignKey.DeclaringEntityType.FullName) as EdmEntitySet;
            referenceSet.AddNavigationTarget(np, parentSet);
        }

        #endregion


        #region method for Build EdmModel
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
        protected abstract EdmPrimitiveTypeKind GetEdmType(string dbType);
        protected abstract int GetDbType(string dbType);
        protected abstract ParameterDirection GetParameterDirection(string direction);
        #endregion

        #region IDataSource

        public string Name { get; private set; }
        EdmModel _EdmModel;
        readonly object _ModelLocker = new();
        public EdmModel Model
        {
            get
            {
                if (_EdmModel == null)
                {
                    lock (_ModelLocker)
                    {
                        if (_EdmModel == null)
                            _EdmModel = GetEdmModel();
                    }
                }
                return _EdmModel;
            }
        }

        public Action<RequestInfo> BeforeExcute { get; set; }
        public Func<RequestInfo, object, object> AfrerExcute { get; set; }

        public string Create(IEdmEntityObject entity)
        {
            List<DbParameter> pars = new();
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
            List<DbParameter> pars = new();
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
            EdmComplexObject rtv = new(edmType);
            EdmComplexObject r = null;

            var b = new EdmCollectionTypeReference(new EdmCollectionType(_DefaultSPReturnType));
            EdmComplexObjectCollection rList = null;
            int resultSetIndex = -1;
            using (var db = CreateDbAccess(this.ConnectionString))
            {
                var par = db.ExecuteReader(
                    string.Format("{0}.{1}",
                        SafeDbObject(_NameSpaces.GetName(action.Namespace)),
                        SafeDbObject(_NameSpaces[action.Namespace][action.Name].OriginalName)),
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
            IEdmComplexType edmType = action.ReturnType.Definition as IEdmComplexType;
            DbParameter dbParameter;
            foreach (var p in action.Parameters)
            {

                if (parameterValues.TryGetValue(p.Name, out JToken token))
                {
                    if (p.Type.Definition.TypeKind == EdmTypeKind.Complex)
                    {
                        DataTable dt = new();
                        var c = p.Type.AsComplex();
                        var ss = p.Type.Definition as EdmComplexType;
                        var n = _NameSpaces[ss.Namespace][ss.Name];
                        foreach (var item in c.StructuralProperties())
                        {
                            dt.Columns.Add(n.Properties[item.Name].OriginalName, item.Type.PrimitiveKind().ToClrType());
                        }
                        var colName = "";
                        foreach (var item in token)
                        {
                            DataRow dr = dt.NewRow();
                            foreach (JProperty col in item)
                            {
                                colName = n.Properties[col.Name]?.OriginalName;
                                if (string.IsNullOrEmpty(colName) || !dt.Columns.Contains(colName))
                                    continue;
                                Type colType = dt.Columns[colName].DataType;
                                if (colType == typeof(Boolean))
                                {
                                    dr.SetField(colName, col.Value.ToString() != "0");
                                }
                                else
                                    dr.SetField(colName, col.Value.ToString().ChangeType(colType));
                            }
                            dt.Rows.Add(dr);
                        }
                        // todo: get original parameter name
                        dbParameter = CreateParameter(p.Name, dt);
                        pars.Add(dbParameter);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(token.ToString()))
                            dbParameter = CreateParameter(p.Name, DBNull.Value);
                        else
                            dbParameter = CreateParameter(p.Name, token.ToString().ChangeType(p.Type.PrimitiveKind()));
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
                        dbParameter = CreateParameter(outp.Name, DBNull.Value);
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
            IEdmCollectionType edmType = cxt.Path.GetEdmType() as IEdmCollectionType;
            List<DbParameter> pars = new();
            string sqlCmd = BuildQueryCmd(queryOptions, pars);
            EdmEntityObjectCollection collection = new(new EdmCollectionTypeReference(edmType));
            bool needExpand = queryOptions.SelectExpand != null && !string.IsNullOrEmpty(queryOptions.SelectExpand.RawExpand);

            using (var db = CreateDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(sqlCmd, (reader, resultSet) =>
                {
                    EdmEntityObject entity = new(entityType);
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
            List<DbParameter> pars = new();
            string cmdTxt = BuildQueryByKeyCmd(key, queryOptions, pars);
            EdmEntityObject entity = new(entityType);
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

        public long GetCount(ODataQueryOptions queryOptions)
        {
            var cxt = queryOptions.Context;
            var entityType = cxt.ElementType as EdmEntityType;

            object rtv = null;
            List<DbParameter> pars = new();
            string sqlCmd = BuildQueryCmd(queryOptions, pars);
            using (var db = CreateDbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteScalar(sqlCmd,
                    (parBuilder) => { parBuilder.AddRange(pars.ToArray()); },
                    CommandType.Text);
            }
            if (rtv == null)
                return 0;
            if (long.TryParse(rtv.ToString(), out long lrtv))
                return lrtv;
            return 0;
        }

        public int GetFuncResultCount(ODataQueryOptions queryOptions)
        {
            int count = 0;
            var ois = queryOptions.Context.Path.FirstSegment as OperationImportSegment;
            var func = ois.OperationImports.First().Operation;
            IEdmType edmType = func.ReturnType.Definition;
            List<DbParameter> sqlpars = new();
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
            var ois = queryOptions.Context.Path.FirstSegment as OperationImportSegment;
            var func = ois.OperationImports.First().Operation;
            IEdmType edmType = func.ReturnType.Definition;
            IEdmType elementType = (edmType as IEdmCollectionType).ElementType.Definition;
            EdmComplexObjectCollection collection = new(new EdmCollectionTypeReference(edmType as IEdmCollectionType));
            List<DbParameter> sqlpars = new();
            var cmd = BuildQueryCmd(queryOptions, sqlpars);
            using (var db = CreateDbAccess(this.ConnectionString))
            {
                _ = db.ExecuteReader(cmd, (reader, resultSet) =>
                  {
                      EdmComplexObject entity = new(elementType as IEdmComplexType);
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
            List<DbParameter> sqlpars = new();
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
            List<DbParameter> pars = new();
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

        #region method for build SQL command string
        protected virtual string BuildExpandQueryCmd(EdmEntityObject edmEntity, ExpandedNavigationSelectItem expanded, List<DbParameter> pars)
        {
            var entityType = expanded.NavigationSource.EntityType();
            var ns = _NameSpaces[entityType.Namespace];
            var ent = ns[entityType.Name];
            var wp = new List<string>();
            var sourceEntityType = edmEntity.ActualEdmType as IEdmEntityType;
            var sourceEnt = _NameSpaces[sourceEntityType.Namespace][sourceEntityType.Name];
            foreach (NavigationPropertySegment item2 in expanded.PathToNavigationProperty)
            {
                foreach (var p in item2.NavigationProperty.ReferentialConstraint.PropertyPairs)
                {
                    edmEntity.TryGetPropertyValue(p.DependentProperty.Name, out object v);
                    var par = CreateParameter(sourceEnt.Properties[p.DependentProperty.Name] as Property, v, pars);
                    pars.Add(par);
                    wp.Add(string.Format("{0}={1}", SafeDbObject(ent.Properties[p.PrincipalProperty.Name].OriginalName), par.ParameterName));
                }
            }
            string where = " where " + string.Join("and", wp) + expanded.ParseFilter(ent, this, pars);
            string cmdTemplete;
            //0:Top,1:Select,2:Schema,3:Table,4:where,5:orderby,6:skip
            if (expanded.CountOption.HasValue)
                cmdTemplete = QueryCountCommandTemplete;
            else if (expanded.SkipOption.HasValue)
                cmdTemplete = QueryPagingCommandTemplete;
            else if (expanded.TopOption.HasValue)
                cmdTemplete = QueryTopCommandTemplete;
            else
                cmdTemplete = QueryCommandTemplete;
            string order = expanded.ParseOrderBy(this);
            if (string.IsNullOrEmpty(order))
            {
                if (entityType.DeclaredKey.Any())
                    order = SafeDbObject(ent.Properties[entityType.DeclaredKey.First().Name].OriginalName);
                else
                    order = SafeDbObject(ent.Properties[entityType.DeclaredProperties.First().Name].OriginalName);

            }
            return string.Format(cmdTemplete
                , expanded.TopOption.HasValue ? expanded.TopOption.Value.ToString() : string.Empty
                , expanded.ParseSelect(this)
                , SafeDbObject(ns.Schema)
                , SafeDbObject(ent.OriginalName)
                , where
                , order
                , expanded.SkipOption.HasValue ? expanded.SkipOption.Value.ToString() : string.Empty);
        }
        protected virtual string QueryCountCommandTemplete { get { return "select {0} {1} from {2}.{3} {4}"; } }
        protected virtual string QueryPagingCommandTemplete { get { return "select {1} from {2}.{3} {4} order by {5} OFFSET {6} rows FETCH NEXT {0} rows only"; } }
        protected virtual string QueryTopCommandTemplete { get { return "select top {0} {1} from {2}.{3} {4} order by {5}"; } }
        protected virtual string QueryCommandTemplete { get { return "select {0} {1} from {2}.{3} {4} order by {5}"; } }
        protected virtual string BuildQueryCmd(ODataQueryOptions options, List<DbParameter> pars)
        {
            var cxt = options.Context;
            Entity entity;

            string name, order, cmdTemplete;
            order = options.ParseOrderBy(this);
            if (cxt.Path.FirstSegment is OperationImportSegment ois)
            {
                var func = ois.OperationImports.First().Operation;
                entity = _NameSpaces[func.Namespace][func.Name];
                DbParameter par;
                List<DbParameter> funcPList = new();

                foreach (var item in ois.Parameters)
                {
                    par = CreateParameter(entity.Properties[item.Name] as Property, (item.Value as ConstantNode).Value, pars);
                    pars.Add(par);
                    funcPList.Add(par);
                }

                name = $"{SafeDbObject(entity.Schema)}.{SafeDbObject(entity.OriginalName)}({string.Join(",", funcPList.ConvertAll<string>(p => p.ParameterName))})";
                if (string.IsNullOrEmpty(order))
                {
                    var entityType = cxt.ElementType as EdmComplexType;
                    order = SafeDbObject(entityType.DeclaredProperties.First().Name);
                }
            }
            else
            {
                var entityType = cxt.ElementType as EdmEntityType;
                entity = _NameSpaces[entityType.Namespace][entityType.Name];
                if (string.IsNullOrEmpty(order))
                {
                    if (entityType.DeclaredKey.Any())
                        order = SafeDbObject(entity.Properties[entityType.DeclaredKey.First().Name].OriginalName);
                    else
                        order = SafeDbObject(entity.Properties[entityType.DeclaredProperties.First().Name].OriginalName);
                }
            }
            //0:Top,1:Select,2:Schema,3:Table,4:where,5:orderby,6:skip
            if (options.Count != null)
                cmdTemplete = QueryCountCommandTemplete;
            else if (options.Skip != null)
                cmdTemplete = QueryPagingCommandTemplete;
            else if (options.Top != null)
                cmdTemplete = QueryTopCommandTemplete;
            else cmdTemplete = QueryCommandTemplete;
            return string.Format(cmdTemplete
                , options.Top?.RawValue
                , options.ParseSelect(this)
                , SafeDbObject(entity.Schema)
                , SafeDbObject(entity.OriginalName)
                , options.ParseFilter(entity, this, pars)
                , order
                , options.Skip?.RawValue);
        }
        protected virtual string CreateCommandTemplete { get { return "insert into {0}.{1} ({2}) values ({3}); select SCOPE_IDENTITY() "; } }

        protected virtual string BuildCreateCmd(IEdmEntityObject entity, List<DbParameter> pars)
        {
            var edmType = entity.GetEdmType();
            var entityType = edmType.Definition as EdmEntityType;

            List<string> cols = new();
            List<string> ps = new();
            var schema = _NameSpaces.GetOriginalName(entityType.Namespace);
            var ent = _NameSpaces[entityType.Namespace][entityType.Name];
            foreach (var p in (entity as Delta).GetChangedPropertyNames())
            {
                if (entity.TryGetPropertyValue(p, out object v))
                {
                    var pro = ent.Properties[p];
                    cols.Add(SafeDbObject(pro.OriginalName));
                    var par = CreateParameter(pro as Property, v, pars);
                    ps.Add(par.ParameterName);
                }
            }
            return string.Format(CreateCommandTemplete,
               SafeDbObject(schema),
               SafeDbObject(ent.OriginalName),
                string.Join(", ", cols),
                string.Join(", ", ps));
        }
        protected virtual string QueryByKeyCommandTemplete { get { return "select {0} from {1} where {2}={3}"; } }

        protected virtual string BuildQueryByKeyCmd(string key, ODataQueryOptions options, List<DbParameter> pars)
        {
            var cxt = options.Context;
            var entityType = cxt.ElementType as EdmEntityType;
            var ns = _NameSpaces[entityType.Namespace];
            var ent = _NameSpaces[entityType.Namespace][entityType.Name];
            var keyDefine = entityType.DeclaredKey.First();
            var par = CreateParameter(ent.Properties[keyDefine.Name] as Property, key.ChangeType(keyDefine.Type.PrimitiveKind()), pars);

            return string.Format(QueryByKeyCommandTemplete
                , options.ParseSelect(this)
                , $"{SafeDbObject(ns.Schema)}.{SafeDbObject(ent.OriginalName)}"
                , SafeDbObject(ent.Properties[keyDefine.Name].OriginalName),
                par.ParameterName);
        }
        protected virtual string DeleteCommandTemplete { get { return "delete from  {0}.{1} where {2}={3};"; } }

        protected virtual string BuildDeleteCmd(string key, IEdmType elementType, List<DbParameter> pars)
        {
            var entityType = elementType as EdmEntityType;
            var ns = _NameSpaces[entityType.Namespace];
            var ent = _NameSpaces[entityType.Namespace][entityType.Name];
            var keyDefine = entityType.DeclaredKey.First();
            var par = CreateParameter(ent.Properties[keyDefine.Name] as Property, key.ChangeType(keyDefine.Type.PrimitiveKind()), pars);
            return string.Format(DeleteCommandTemplete,
               SafeDbObject(ns.Schema),
               SafeDbObject(ent.OriginalName),
               SafeDbObject(ent.Properties[keyDefine.Name].OriginalName),
               par.ParameterName);
        }
        protected virtual string ReplaceCommandTemplete { get { return "update {0}.{1} set {2} where {3}={4} "; } }

        protected virtual string BuildReplaceCmd(string key, IEdmEntityObject entity, List<DbParameter> pars)
        {
            var entityType = entity.GetEdmType().Definition as EdmEntityType;
            var ns = _NameSpaces[entityType.Namespace];
            var ent = _NameSpaces[entityType.Namespace][entityType.Name];
            var keyDefine = entityType.DeclaredKey.First();
            List<string> cols = new();
            DbParameter par;
            foreach (var p in entityType.Properties())
            {
                if (p.PropertyKind == EdmPropertyKind.Navigation) continue;
                if (entity.TryGetPropertyValue(p.Name, out object v))
                {
                    if (keyDefine.Name == p.Name) continue;
                    par = CreateParameter(ent.Properties[p.Name] as Property, v, pars);
                    pars.Add(par);
                    cols.Add(string.Format("{0}={1}", SafeDbObject((ent.Properties[p.Name] as Property).OriginalName), par.ParameterName));
                }
            }
            par = CreateParameter(ent.Properties[key] as Property, key.ChangeType(keyDefine.Type.PrimitiveKind()), pars);
            pars.Add(par);

            return string.Format(ReplaceCommandTemplete,
                SafeDbObject(ns.Schema),
                SafeDbObject(ent.OriginalName),
                string.Join(",", cols),
               SafeDbObject(ent.Properties[keyDefine.Name].OriginalName),
                par.ParameterName);
        }
        protected virtual string MergeCommandTemplete { get { return "update {0}.{1} set {2} where {3}={4} "; } }


        protected virtual string BuildMergeCmd(string key, IEdmEntityObject entity, List<DbParameter> pars)
        {
            var entityType = entity.GetEdmType().Definition as EdmEntityType;
            var ns = _NameSpaces[entityType.Namespace];
            var ent = ns[entityType.Name];
            var keyDefine = entityType.DeclaredKey.First();
            List<string> cols = new();
            DbParameter par;
            foreach (var p in (entity as Delta).GetChangedPropertyNames())
            {
                if (entity.TryGetPropertyValue(p, out object v))
                {
                    par = CreateParameter(ent.Properties[p] as Property, v, pars);

                    cols.Add(string.Format("{0}={1}", SafeDbObject(p), par.ParameterName));
                }
            }
            par = CreateParameter(ent.Properties[keyDefine.Name] as Property, key.ChangeType(keyDefine.Type.PrimitiveKind()), pars);

            return string.Format(MergeCommandTemplete,
               SafeDbObject(ns.Schema),
               SafeDbObject(ent.OriginalName),
                string.Join(", ", cols),
               SafeDbObject(ent.Properties[keyDefine.Name].OriginalName),
                par.ParameterName);
        }
        private void Expand(EdmEntityObject edmEntity, SelectExpandClause expandClause)
        {
            foreach (var item1 in expandClause.SelectedItems)
            {
                if (item1 is not ExpandedNavigationSelectItem expanded)
                    continue;
                CreateExpandEntity(edmEntity, expanded);
            }
        }
        private void CreateExpandEntity(EdmEntityObject edmEntity, ExpandedNavigationSelectItem expanded)
        {
            List<DbParameter> pars = new();
            string cmdtxt = BuildExpandQueryCmd(edmEntity, expanded, pars);
            var edmType = expanded.NavigationSource.Type as IEdmCollectionType;
            var entityType = edmType.ElementType.AsEntity();
            EdmEntityObjectCollection collection = new(new EdmCollectionTypeReference(edmType));
            using (var db = CreateDbAccess(this.ConnectionString))
            {
                db.ExecuteReader(cmdtxt, (reader, resultSet) =>
                {
                    EdmEntityObject entity = new(entityType);
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

        #endregion

        public abstract string SafeDbObject(string obj);
        public abstract DbParameter CreateParameter(Property property, object value, List<DbParameter> pars);
        public abstract DbParameter CreateParameter(object value, List<DbParameter> pars);
        public abstract DbParameter CreateParameter(string name, object value);
    }
}
