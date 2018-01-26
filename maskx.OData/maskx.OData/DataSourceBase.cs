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
using Newtonsoft.Json.Linq;

namespace maskx.OData
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
            throw new NotImplementedException();
        }

        public int Delete(string key, IEdmType elementType)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public EdmEntityObject Get(string key, ODataQueryOptions queryOptions)
        {
            throw new NotImplementedException();
        }

        public int GetCount(ODataQueryOptions queryOptions)
        {
            throw new NotImplementedException();
        }

        public int GetFuncResultCount(IEdmFunction func, JObject parameterValues, ODataQueryOptions queryOptions)
        {
            throw new NotImplementedException();
        }

        public IEdmObject InvokeFunction(IEdmFunction action, JObject parameterValues, ODataQueryOptions queryOptions = null)
        {
            throw new NotImplementedException();
        }

        public int Merge(string key, IEdmEntityObject entity)
        {
            throw new NotImplementedException();
        }

        public int Replace(string key, IEdmEntityObject entity)
        {
            string cmdTemplate = "update {0}.[{1}] set {2} where [{3}]=@{3}  ";
            var entityType = entity.GetEdmType().Definition as EdmEntityType;

            var keyDefine = entityType.DeclaredKey.First();
            List<string> cols = new List<string>();
            foreach (var p in entityType.Properties())
            {
                if (p.PropertyKind == EdmPropertyKind.Navigation) continue;
                cols.Add(string.Format("[{0}]=@{0}", p.Name));
            }
            if (cols.Count == 0)
                return 0;
            int rtv;
            using (var db = CreateDbAccess(this.ConnectionString))
            {
                rtv = db.ExecuteNonQuery(string.Format(cmdTemplate, entityType.Namespace, entityType.Name, string.Join(", ", cols), keyDefine.Name)
                    , (dbpars) =>
                    {
                        foreach (var p in entityType.Properties())
                        {
                            if (p.PropertyKind == EdmPropertyKind.Navigation) continue;
                            if (entity.TryGetPropertyValue(p.Name, out object v))
                            {

                            }
                        }
                    }, CommandType.Text);
            }
            return rtv;
        }
        #endregion

        protected abstract DbAccess CreateDbAccess(string connectionString);
    }
}
