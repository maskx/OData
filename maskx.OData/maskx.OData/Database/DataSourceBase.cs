using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using maskx.Database;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Newtonsoft.Json.Linq;

namespace maskx.OData.Database
{
    public abstract class DbSourceBase : IDataSource
    {
        #region Constructor
        protected DbSourceBase(string name, string connectionString) : this(name)
        {
            this.ConnectionString = connectionString;
        }
        protected DbSourceBase(string name)
        {
            this.Name = name;
            this.Configuration = new Configuration();
        }
        #endregion

        #region member
        public string ConnectionString { get; set; }
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
            EdmAction action = null;
            List<(string Name, IEdmTypeReference Type)> outParameter = new List<(string Name, IEdmTypeReference type)>();
            EdmComplexType root = new EdmComplexType(model.EntityContainer.Namespace, "ActionResultSet", null, false, true);
            model.AddElement(root);
            var defaultReturnType = new EdmComplexTypeReference(root, true);
            foreach (var (SchemaName, StoredProcedureName, ParameterName, ParameterDataType, ParemeterMode, UserDefinedTypeSchema, UserDefinedTypeName, MaxLength, NumericScale) in GetStoredProcedures())
            {
                if (!string.IsNullOrEmpty(currentName))
                {
                    if (currentNs != SchemaName || currentName != StoredProcedureName)
                    {
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
                            CreateAction(model, tr, SchemaName, StoredProcedureName);
                        }
                        else
                            CreateAction(model, defaultReturnType, SchemaName, StoredProcedureName);
                        currentName = StoredProcedureName;
                        currentNs = SchemaName;
                    }
                }
                if (string.IsNullOrEmpty(ParameterName))
                {
                    CreateAction(model, defaultReturnType, SchemaName, StoredProcedureName);
                    currentName = StoredProcedureName;
                    currentNs = SchemaName;
                    outParameter.Clear();
                    continue;
                }
                var et = GetEdmType(ParameterDataType);
                if (et.HasValue)
                {
                    var t = EdmCoreModel.Instance.GetPrimitive(et.Value, true);
                    var p = action.AddParameter(Configuration.LowerName ? ParameterName.ToLower() : ParameterName, t);
                    if (ParemeterMode.EndsWith("OUT", StringComparison.InvariantCultureIgnoreCase))
                        outParameter.Add((p.Name, t));
                }
                else if (string.IsNullOrEmpty(UserDefinedTypeName))//UDT
                {
                    var udt = BuildUserDefinedType(UserDefinedTypeSchema, UserDefinedTypeName);
                    action.AddParameter(Configuration.LowerName ? ParameterName.ToLower() : ParameterName, udt);
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
                CreateAction(model, tr, currentNs, currentName);
            }
            else
                CreateAction(model, defaultReturnType, currentNs, currentName);

        }

        private EdmAction CreateAction(EdmModel model, EdmComplexTypeReference returType, string SchemaName, string StoredProcedureName)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            var action = new EdmAction(SchemaName, StoredProcedureName, returType, false, null);
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
        EdmComplexTypeReference BuildTableValueType(string schema, string name)
        {
            EdmComplexType root = new EdmComplexType(schema, name);
            foreach (var (ColumnName, DataType, Length, isNullable) in GetTableValueType(schema, name))
            {
                var et = GetEdmType(DataType);
                if (et.HasValue)
                {
                    root.AddStructuralProperty(Configuration.LowerName ? ColumnName.ToLower() : ColumnName, et.Value);
                }
            }
            return new EdmComplexTypeReference(root, false);
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
                    entityName = SchemaName == Configuration.DefaultSchema ? string.Format("{0}.{1}", SchemaName, FunctionName) : FunctionName;
                    if (Configuration.LowerName)
                        entityName = entityName.ToLower();
                    var t = BuildTableValueType(SchemaName, FunctionName);
                    func = new EdmFunction(SchemaName, FunctionName, t, false, null, true);
                    container.AddFunctionImport(entityName, func);
                }
                if (string.IsNullOrEmpty(ParameterDataType))
                    continue;
                var et = GetEdmType(ParameterDataType);
                if (et.HasValue)
                {
                    var t = EdmCoreModel.Instance.GetPrimitive(et.Value, true);
                    func.AddParameter(Configuration.LowerName ? ParameterName.ToLower() : ParameterName, t);
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
            string parentEntityName = string.Empty;
            string referencedEntityName = string.Empty;
            EdmEntityType parent = null;
            EdmEntityType refrenced = null;
            EdmNavigationPropertyInfo parentNav = null;
            EdmNavigationPropertyInfo referencedNav = null;
            List<IEdmStructuralProperty> principalProperties = null;
            List<IEdmStructuralProperty> dependentProperties = null;

            foreach (var (ForeignKeyName, ParentSchemaName, ParentName, ParentColumnName, RefrencedName, RefrencedSchemaName, RefrencedColumnName) in GetRelationship())
            {
                if (fk != ForeignKeyName)
                {
                    parentEntityName = ParentSchemaName == Configuration.DefaultSchema ? ParentName : string.Format("{0}.{1}", ParentSchemaName, ParentName);
                    referencedEntityName = RefrencedSchemaName == Configuration.DefaultSchema ? RefrencedName : string.Format("{0}.{1}", RefrencedSchemaName, RefrencedName);
                    if (Configuration.LowerName)
                    {
                        parentEntityName = parentEntityName.ToLower();
                        referencedEntityName = referencedEntityName.ToLower();
                    }
                    principalProperties = new List<IEdmStructuralProperty>();
                    dependentProperties = new List<IEdmStructuralProperty>();
                    parent = model.FindDeclaredType(string.Format("{0}.{1}", ParentSchemaName, ParentName)) as EdmEntityType;
                    refrenced = model.FindDeclaredType(string.Format("{0}.{1}", RefrencedSchemaName, RefrencedName)) as EdmEntityType;
                    parentNav = new EdmNavigationPropertyInfo
                    {
                        Name = ParentName,
                        TargetMultiplicity = EdmMultiplicity.Many,
                        Target = parent
                    };
                    referencedNav = new EdmNavigationPropertyInfo
                    {
                        Name = RefrencedName,
                        TargetMultiplicity = EdmMultiplicity.Many
                    };
                    parentNav.PrincipalProperties = principalProperties;
                    parentNav.DependentProperties = dependentProperties;
                    var np = refrenced.AddBidirectionalNavigation(parentNav, referencedNav);
                    var parentSet = model.EntityContainer.FindEntitySet(parentEntityName) as EdmEntitySet;
                    var referenceSet = model.EntityContainer.FindEntitySet(referencedEntityName) as EdmEntitySet;
                    referenceSet.AddNavigationTarget(np, parentSet);
                    fk = ForeignKeyName;
                }
                principalProperties.Add(parent.FindProperty(ParentColumnName) as IEdmStructuralProperty);
                dependentProperties.Add(refrenced.FindProperty(RefrencedColumnName) as IEdmStructuralProperty);
            }
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

            rtv.TrySetPropertyValue("first", 123);
            return rtv;
        }

        public EdmEntityObjectCollection Get(ODataQueryOptions queryOptions)
        {
            var cxt = queryOptions.Context;
            var entityType = cxt.ElementType as EdmEntityType;
            var table = entityType.Name;
            var edmType = cxt.Path.EdmType as IEdmCollectionType;
            List<DbParameter> pars = new List<DbParameter>();
            string sqlCmd = BuildQueryCmd(queryOptions, pars);
            EdmEntityObjectCollection collection = new EdmEntityObjectCollection(new EdmCollectionTypeReference(edmType));
            bool needExpand = queryOptions.SelectExpand != null && !string.IsNullOrEmpty(queryOptions.SelectExpand.RawExpand);

            using (var db = CreateDbAccess(this.ConnectionString))
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
            List<DbParameter> pars = new List<DbParameter>();
            string cmdTxt = BuildQueryByKeyCmd(key, queryOptions, pars);
            EdmEntityObject entity = new EdmEntityObject(entityType);
            bool needExpand = queryOptions.SelectExpand != null && !string.IsNullOrEmpty(queryOptions.SelectExpand.RawExpand);
            using (var db = CreateDbAccess(this.ConnectionString))
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

        public int GetFuncResultCount(IEdmFunction func, JObject parameterValues, ODataQueryOptions queryOptions)
        {
            int count = 0;
            IEdmType edmType = func.ReturnType.Definition;
            List<DbParameter> sqlpars = new List<DbParameter>();
            var target = BuildTVFTarget(func, parameterValues, sqlpars);

            var cmd = BuildQueryCmd(queryOptions, sqlpars, target);
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

        public IEdmObject InvokeFunction(IEdmFunction func, JObject parameterValues, ODataQueryOptions queryOptions = null)
        {
            IEdmType edmType = func.ReturnType.Definition;
            IEdmType elementType = (edmType as IEdmCollectionType).ElementType.Definition;
            EdmComplexObjectCollection collection = new EdmComplexObjectCollection(new EdmCollectionTypeReference(edmType as IEdmCollectionType));
            List<DbParameter> sqlpars = new List<DbParameter>();
            var target = BuildTVFTarget(func, parameterValues, sqlpars);
            var cmd = BuildQueryCmd(queryOptions, sqlpars, target);
            using (var db = CreateDbAccess(this.ConnectionString))
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
        protected abstract string BuildQueryCmd(ODataQueryOptions options, List<DbParameter> pars, string target = "");
        protected abstract string BuildExpandQueryCmd(EdmEntityObject edmEntity, ExpandedNavigationSelectItem expanded, List<DbParameter> pars);
        protected abstract string BuildQueryByKeyCmd(string key, ODataQueryOptions options, List<DbParameter> pars);
        protected abstract string BuildTVFTarget(IEdmFunction func, JObject parameterValues, List<DbParameter> sqlpars);
        protected abstract string BuildMergeCmd(string key, IEdmEntityObject entity, List<DbParameter> pars);
        protected abstract string BuildReplaceCmd(string key, IEdmEntityObject entity, List<DbParameter> pars);
        protected abstract string BuildCreateCmd(IEdmEntityObject entity, List<DbParameter> pars);
        protected abstract string BuildDeleteCmd(string key, IEdmType elementType, List<DbParameter> pars);
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

    }
}
