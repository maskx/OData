using maskx.Database;
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
        private readonly CSharpUtilities _CSharpUtilities = new CSharpUtilities();
        private readonly Dictionary<string, CSharpUniqueNamer> _TableNamers = new Dictionary<string, CSharpUniqueNamer>();
        private readonly Dictionary<string, CSharpUniqueNamer> _columnNamers = new Dictionary<string, CSharpUniqueNamer>();
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
            EdmEntityType t = null;
            string currentSchemaName = string.Empty;
            string currentTableName = string.Empty;

            foreach (var (SchemaName, TableName, ColumnName, DataType, isKey) in Items)
            {

                if (t == null || currentTableName != TableName || currentSchemaName != SchemaName)
                {
                    currentSchemaName = SchemaName;
                    currentTableName = TableName;
                    string tName = GetEntityName(SchemaName, TableName);
                    string sName = _CSharpUtilities.GenerateCSharpIdentifier(SchemaName, null, null);
                    string entityName = Configuration.DefaultSchema == sName ? tName : string.Format("{0}.{1}", sName, tName);
                    if (Configuration.LowerName)
                        entityName = entityName.ToLower();
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
                var et = GetEdmType(DataType);
                if (!et.HasValue)
                    continue;
                if (et.Value == EdmPrimitiveTypeKind.Binary)
                    continue;
                EdmStructuralProperty key = t.AddStructuralProperty(GetPropertyName($"{currentSchemaName}.{currentTableName}", Configuration.LowerName ? ColumnName.ToLower() : ColumnName), et.Value, true);
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
                }
                if (currentNs != SchemaName || currentName != StoredProcedureName)
                {
                    if (outParameter.Count > 0)
                    {
                        EdmComplexType t = new(model.EntityContainer.Namespace, string.Format("{0}_{1}_Result", currentNs, currentName), null, false, true);
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
                EdmComplexType t = new(model.EntityContainer.Namespace, string.Format("{0}_{1}_Result", currentNs, currentName), null, false, true);
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
            foreach (var (name, type) in pars)
            {
                //remove the prefix such as @ or ?
                string pName = name.Remove(0, 1);
                if (Configuration.LowerName)
                    pName = pName.ToLower();
                action.AddParameter(pName, type);
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
            EdmComplexType root = new(schema, name);
            foreach (var (ColumnName, DataType, _, _) in GetUserDefinedType(schema, name))
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
            EdmComplexType root = new(schema, String.Format("{0}_RtvType", name));
            foreach (var (ColumnName, DataType, _, _) in GetTableValueType(schema, name))
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
            EdmFunction func = null;
            foreach (var (SchemaName, FunctionName, ParameterName, ParameterDataType, UserDefinedTypeSchema, UserDefinedTypeName, MaxLength, NumericScale) in GetFunctions())
            {
                if (string.IsNullOrEmpty(currentName) || currentName != FunctionName || currentNS != SchemaName)
                {
                    string entityName = SchemaName == Configuration.DefaultSchema ? FunctionName : string.Format("{0}.{1}", SchemaName, FunctionName);
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

            EdmEntityType parent = null;
            EdmEntityType refrenced = null;
            List<IEdmStructuralProperty> principalProperties = null;
            List<IEdmStructuralProperty> dependentProperties = null;

            foreach (var (ForeignKeyName, ParentSchemaName, ParentName, ParentColumnName, ReferencedName, ReferencedSchemaName, ReferencedColumnName) in GetRelationship())
            {
                if (fk != ForeignKeyName)
                {
                    if (parent != null)
                    {
                        CreatReference(model, parent, refrenced, principalProperties, dependentProperties);
                    }
                    parent = model.FindDeclaredType(GetEntityTypeName(ParentSchemaName, ParentName)) as EdmEntityType;
                    refrenced = model.FindDeclaredType(GetEntityTypeName(ReferencedSchemaName, ReferencedName)) as EdmEntityType;

                    principalProperties = new List<IEdmStructuralProperty>();
                    dependentProperties = new List<IEdmStructuralProperty>();
                    fk = ForeignKeyName;
                }
                principalProperties.Add(parent.FindProperty(Configuration.LowerName ? ParentColumnName.ToLower() : ParentColumnName) as IEdmStructuralProperty);
                dependentProperties.Add(refrenced.FindProperty(Configuration.LowerName ? ReferencedColumnName.ToLower() : ReferencedColumnName) as IEdmStructuralProperty);
            }
            if (parent != null)
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

        string BuildNavigationName()
        {
            return string.Empty;
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
        protected abstract EdmPrimitiveTypeKind? GetEdmType(string dbType);
        #endregion

        #region IDataSource
        public Configuration Configuration { get; set; }

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
                                Type colType = dt.Columns[col.Name].DataType;
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
                        reader.SetEntityPropertyValue(i, entity, Configuration.LowerName);
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
        protected abstract DbUtility _DbUtility { get; }

        #region method for build SQL command string
        protected virtual string BuildExpandQueryCmd(EdmEntityObject edmEntity, ExpandedNavigationSelectItem expanded, List<DbParameter> pars)
        {
            var entityType = expanded.NavigationSource.EntityType();
            var wp = new List<string>();
            foreach (NavigationPropertySegment item2 in expanded.PathToNavigationProperty)
            {
                foreach (var p in item2.NavigationProperty.Partner.ReferentialConstraint.PropertyPairs)
                {
                    edmEntity.TryGetPropertyValue(p.DependentProperty.Name, out object v);
                    var par = _DbUtility.CreateParameter(v, pars);
                    wp.Add(string.Format("{0}={1}", _DbUtility.SafeDbObject(p.PrincipalProperty.Name), par.ParameterName));
                }
            }
            string where = " where " + string.Join("and", wp) + expanded.ParseFilter(pars, _DbUtility);
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
            string order = expanded.ParseOrderBy(_DbUtility);
            if (string.IsNullOrEmpty(order))
            {
                if (entityType.DeclaredKey.Any())
                    order = _DbUtility.SafeDbObject(entityType.DeclaredKey.First().Name);
                else
                    order = _DbUtility.SafeDbObject(entityType.DeclaredProperties.First().Name);

            }
            return string.Format(cmdTemplete
                , expanded.TopOption.HasValue ? expanded.TopOption.Value.ToString() : string.Empty
                , expanded.ParseSelect(_DbUtility)
                , entityType.Namespace
                , entityType.Name
                , where
                , order
                , expanded.SkipOption.HasValue ? expanded.SkipOption.Value.ToString() : string.Empty);
        }
        protected virtual string QueryCountCommandTemplete { get { return "select {0} {1} from {2}.{3} {4}"; } }
        protected virtual string QueryPagingCommandTemplete { get { return "select {1} from {2}.{3} {4} order by {5} OFFSET {6} rows FETCH NEXT {0} rows only"; } }
        protected virtual string QueryTopCommandTemplete { get { return "select top {0} {1} from {2}.{3} {4} order by {5}"; } }
        protected virtual String QueryCommandTemplete { get { return "select {0} {1} from {2}.{3} {4} order by {5}"; } }
        protected virtual string BuildQueryCmd(ODataQueryOptions options, List<DbParameter> pars)
        {
            var cxt = options.Context;
            string ns, name, order, cmdTemplete;
            order = options.ParseOrderBy(_DbUtility);
            if (cxt.Path.FirstSegment is OperationImportSegment ois)
            {
                var func = ois.OperationImports.First().Operation;
                DbParameter par;
                List<DbParameter> funcPList = new();
                foreach (var item in ois.Parameters)
                {
                    par = _DbUtility.CreateParameter((item.Value as ConstantNode).Value, pars);
                    funcPList.Add(par);
                }
                ns = func.Namespace;
                name = string.Format("{0}({1})", _DbUtility.SafeDbObject(func.Name), string.Join(",", funcPList.ConvertAll<string>(p => p.ParameterName)));
                if (string.IsNullOrEmpty(order))
                {
                    var entityType = cxt.ElementType as EdmComplexType;
                    order = _DbUtility.SafeDbObject(entityType.DeclaredProperties.First().Name);
                }
            }
            else
            {
                var entityType = cxt.ElementType as EdmEntityType;
                ns = _DbUtility.SafeDbObject(entityType.Namespace);
                name = _DbUtility.SafeDbObject(entityType.Name);
                if (string.IsNullOrEmpty(order))
                {
                    if (entityType.DeclaredKey.Any())
                        order = _DbUtility.SafeDbObject(entityType.DeclaredKey.First().Name);
                    else
                        order = _DbUtility.SafeDbObject(entityType.DeclaredProperties.First().Name);
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
                , options.ParseSelect(_DbUtility)
                , ns
                , name
                , options.ParseFilter(pars, _DbUtility)
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
            foreach (var p in (entity as Delta).GetChangedPropertyNames())
            {
                entity.TryGetPropertyValue(p, out object v);
                cols.Add(_DbUtility.SafeDbObject(p));
                var par = _DbUtility.CreateParameter(v, pars);
                ps.Add(par.ParameterName);
            }
            return string.Format(CreateCommandTemplete,
               _DbUtility.SafeDbObject(entityType.Namespace),
               _DbUtility.SafeDbObject(entityType.Name),
                string.Join(", ", cols),
                string.Join(", ", ps));
        }
        protected virtual string QueryByKeyCommandTemplete { get { return "select {0} from {1} where {2}={3}"; } }

        protected virtual string BuildQueryByKeyCmd(string key, ODataQueryOptions options, List<DbParameter> pars)
        {
            var cxt = options.Context;
            var entityType = cxt.ElementType as EdmEntityType;
            var keyDefine = entityType.DeclaredKey.First();
            var par = _DbUtility.CreateParameter(key.ChangeType(keyDefine.Type.PrimitiveKind()), pars);

            return string.Format(QueryByKeyCommandTemplete
                , options.ParseSelect(_DbUtility)
                , _DbUtility.SafeDbObject(entityType.Name)
                , _DbUtility.SafeDbObject(keyDefine.Name),
                par.ParameterName);
        }
        protected virtual string DeleteCommandTemplete { get { return "delete from  {0}.{1} where {2}={3};"; } }

        protected virtual string BuildDeleteCmd(string key, IEdmType elementType, List<DbParameter> pars)
        {
            var entityType = elementType as EdmEntityType;
            var keyDefine = entityType.DeclaredKey.First();
            var par = _DbUtility.CreateParameter(key.ChangeType(keyDefine.Type.PrimitiveKind()), pars);
            return string.Format(DeleteCommandTemplete,
               _DbUtility.SafeDbObject(entityType.Namespace),
               _DbUtility.SafeDbObject(entityType.Name),
               _DbUtility.SafeDbObject(keyDefine.Name),
               par.ParameterName);
        }
        protected virtual string ReplaceCommandTemplete { get { return "update {0}.{1} set {2} where {3}={4} "; } }

        protected virtual string BuildReplaceCmd(string key, IEdmEntityObject entity, List<DbParameter> pars)
        {
            var entityType = entity.GetEdmType().Definition as EdmEntityType;
            var keyDefine = entityType.DeclaredKey.First();
            List<string> cols = new();
            DbParameter par;
            foreach (var p in entityType.Properties())
            {
                if (p.PropertyKind == EdmPropertyKind.Navigation) continue;
                if (entity.TryGetPropertyValue(p.Name, out object v))
                {
                    if (keyDefine.Name == p.Name) continue;
                    par = _DbUtility.CreateParameter(v, pars);
                    cols.Add(string.Format("{0}={1}", _DbUtility.SafeDbObject(p.Name), par.ParameterName));
                }
            }
            par = _DbUtility.CreateParameter(key.ChangeType(keyDefine.Type.PrimitiveKind()), pars);

            return string.Format(ReplaceCommandTemplete,
                _DbUtility.SafeDbObject(entityType.Namespace),
                _DbUtility.SafeDbObject(entityType.Name),
                string.Join(",", cols),
               _DbUtility.SafeDbObject(keyDefine.Name),
                par.ParameterName);
        }
        protected virtual string MergeCommandTemplete { get { return "update {0}.{1} set {2} where {3}={4} "; } }
        protected virtual string BuildMergeCmd(string key, IEdmEntityObject entity, List<DbParameter> pars)
        {
            var entityType = entity.GetEdmType().Definition as EdmEntityType;
            var keyDefine = entityType.DeclaredKey.First();
            List<string> cols = new();
            DbParameter par;
            foreach (var p in (entity as Delta).GetChangedPropertyNames())
            {
                if (entity.TryGetPropertyValue(p, out object v))
                {
                    par = _DbUtility.CreateParameter(v, pars);
                    cols.Add(string.Format("{0}={1}", _DbUtility.SafeDbObject(p), par.ParameterName));
                }
            }
            par = _DbUtility.CreateParameter(key.ChangeType(keyDefine.Type.PrimitiveKind()), pars);
            return string.Format(MergeCommandTemplete,
               _DbUtility.SafeDbObject(entityType.Namespace),
               _DbUtility.SafeDbObject(entityType.Name),
                string.Join(", ", cols),
               _DbUtility.SafeDbObject(keyDefine.Name),
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


        private string GetPropertyName(string table, string column)
        {
            if (!_columnNamers.TryGetValue(table, out CSharpUniqueNamer columnNamer))
            {
                columnNamer = new CSharpUniqueNamer();
                _columnNamers.Add(table, columnNamer);
            }
            return _columnNamers[table].GetName(column);
        }
        private string GetEntityName(string schema, string table)
        {
            if (!_TableNamers.TryGetValue(schema, out CSharpUniqueNamer cSharpNamer))
            {
                cSharpNamer = new CSharpUniqueNamer();
                _TableNamers.Add(schema, cSharpNamer);
            }
            return cSharpNamer.GetName(table);
        }
        private string GetEntityTypeName(string schema, string table)
        {
            return $"{_CSharpUtilities.GenerateCSharpIdentifier(schema, null, null)}.{GetEntityName(schema, table)}";
        }
    }
}
