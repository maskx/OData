using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.OData.Edm;
using Newtonsoft.Json.Linq;

namespace maskx.OData
{
    public class DbSourceBase : IDataSource
    {
        #region Constructor
        public DbSourceBase(string name, string connectionString) : this(name)
        {
            this.ConnectionString = connectionString;
        }
        public DbSourceBase(string name)
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
            foreach (var item in Items)
            {
                entityName = Configuration.DefaultSchema == item.SchemaName ? item.TableName : string.Format("{0}.{1}", item.SchemaName, item.TableName);
                if (Configuration.LowerName)
                    entityName = entityName.ToLower();
                if (t == null || t.Name != item.TableName || t.Namespace != item.SchemaName)
                {
                    edmSet = container.FindEntitySet(entityName);
                    if (edmSet == null)
                    {
                        t = new EdmEntityType(item.SchemaName, item.TableName);
                        model.AddElement(t);
                        container.AddEntitySet(entityName, t);
                    }
                    else
                        t = edmSet.EntityType() as EdmEntityType;
                }
                var et = GetEdmType(item.DataType);
                if (et.HasValue)
                {
                    EdmStructuralProperty key = t.AddStructuralProperty(Configuration.LowerName ? item.ColumnName.ToLower() : item.ColumnName, et.Value, true);
                    if (item.isKey)
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
            foreach (var item in GetStoredProcedures())
            {
                if (string.IsNullOrEmpty(currentName) || currentNs != item.SchemaName || currentName != item.StoredProcedureName)
                {
                    string spRtvTypeName = string.Format("{0}_RtvCollectionType", item.StoredProcedureName);
                    var t = new EdmUntypedStructuredType(item.SchemaName, spRtvTypeName);
                    var tr = new EdmUntypedStructuredTypeReference(t);
                    action = new EdmAction(item.SchemaName, item.StoredProcedureName, tr, false, null);
                    model.AddElement(action);
                    entityName = action.Namespace == Configuration.DefaultSchema ? action.Name : string.Format("{0}.{1}", action.Namespace, action.Name);
                    if (Configuration.LowerName)
                        entityName = entityName.ToLower();
                    container.AddActionImport(entityName, action, null);
                    currentName = item.StoredProcedureName;
                    currentNs = item.SchemaName;
                }
                if (string.IsNullOrEmpty(item.ParameterDataType))
                    continue;
                var et = GetEdmType(item.ParameterDataType);
                if (et.HasValue)
                {
                    var t = EdmCoreModel.Instance.GetPrimitive(et.Value, true);
                    action.AddParameter(Configuration.LowerName ? item.ParameterName.ToLower() : item.ParameterName, t);
                }
                else if (string.IsNullOrEmpty(item.UserDefinedTypeName))//UDT
                {
                    var udt = BuildUserDefinedType(item.UserDefinedTypeSchema, item.UserDefinedTypeName);
                    action.AddParameter(Configuration.LowerName ? item.ParameterName.ToLower() : item.ParameterName, udt);
                }

            }
        }
        EdmComplexTypeReference BuildUserDefinedType(string schema, string name)
        {
            EdmComplexType root = new EdmComplexType(schema, name);
            foreach (var item in GetUserDefinedType(schema, name))
            {
                var et = GetEdmType(item.DataType);
                if (et.HasValue)
                {
                    root.AddStructuralProperty(Configuration.LowerName ? item.ColumnName.ToLower() : item.ColumnName, et.Value);
                }
            }
            return new EdmComplexTypeReference(root, false);
        }
        EdmComplexTypeReference BuildTableValueType(string schema, string name)
        {
            EdmComplexType root = new EdmComplexType(schema, name);
            foreach (var item in GetTableValueType(schema, name))
            {
                var et = GetEdmType(item.DataType);
                if (et.HasValue)
                {
                    root.AddStructuralProperty(Configuration.LowerName ? item.ColumnName.ToLower() : item.ColumnName, et.Value);
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
            foreach (var item in GetTableValuedFunction())
            {
                if (string.IsNullOrEmpty(currentName) || currentName != item.FunctionName || currentNS != item.SchemaName)
                {
                    entityName = item.SchemaName == Configuration.DefaultSchema ? string.Format("{0}.{1}", item.SchemaName, item.FunctionName) : item.FunctionName;
                    if (Configuration.LowerName)
                        entityName = entityName.ToLower();
                    var t = BuildTableValueType(item.SchemaName, item.FunctionName);
                    func = new EdmFunction(item.SchemaName, item.FunctionName, t, false, null, true);
                    container.AddFunctionImport(entityName, func);
                }
                if (string.IsNullOrEmpty(item.ParameterDataType))
                    continue;
                var et = GetEdmType(item.ParameterDataType);
                if (et.HasValue)
                {
                    var t = EdmCoreModel.Instance.GetPrimitive(et.Value, true);
                    func.AddParameter(Configuration.LowerName ? item.ParameterName.ToLower() : item.ParameterName, t);
                }
                else if (string.IsNullOrEmpty(item.UserDefinedTypeName))//UDT
                {
                    var udt = BuildUserDefinedType(item.UserDefinedTypeSchema, item.UserDefinedTypeName);
                    func.AddParameter(Configuration.LowerName ? item.ParameterName.ToLower() : item.ParameterName, udt);
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

            foreach (var item in GetRelationship())
            {
                if(fk!=item.ForeignKeyName)
                {
                    parentEntityName = item.ParentSchemaName == Configuration.DefaultSchema ? item.ParentName : string.Format("{0}.{1}", item.ParentSchemaName, item.ParentName);
                    referencedEntityName = item.RefrencedSchemaName == Configuration.DefaultSchema ? item.RefrencedName : string.Format("{0}.{1}", item.RefrencedSchemaName, item.RefrencedName);
                    if (Configuration.LowerName)
                    {
                        parentEntityName = parentEntityName.ToLower();
                        referencedEntityName = referencedEntityName.ToLower();
                    }
                    principalProperties = new List<IEdmStructuralProperty>();
                    dependentProperties = new List<IEdmStructuralProperty>();
                    parent = model.FindDeclaredType(string.Format("{0}.{1}", item.ParentSchemaName, item.ParentName)) as EdmEntityType;
                    refrenced = model.FindDeclaredType(string.Format("{0}.{1}", item.RefrencedSchemaName, item.RefrencedName)) as EdmEntityType;
                    parentNav = new EdmNavigationPropertyInfo
                    {
                        Name = item.ParentName,
                        TargetMultiplicity = EdmMultiplicity.Many,
                        Target = parent
                    };
                    referencedNav = new EdmNavigationPropertyInfo
                    {
                        Name = item.RefrencedName,
                        TargetMultiplicity = EdmMultiplicity.Many
                    };
                    parentNav.PrincipalProperties = principalProperties;
                    parentNav.DependentProperties = dependentProperties;
                    var np = refrenced.AddBidirectionalNavigation(parentNav, referencedNav);
                    var parentSet = model.EntityContainer.FindEntitySet(parentEntityName) as EdmEntitySet;
                    var referenceSet = model.EntityContainer.FindEntitySet(referencedEntityName) as EdmEntitySet;
                    referenceSet.AddNavigationTarget(np, parentSet);
                    fk = item.ForeignKeyName;
                }
                principalProperties.Add(parent.FindProperty(item.ParentColumnName) as IEdmStructuralProperty);
                dependentProperties.Add(refrenced.FindProperty(item.RefrencedColumnName) as IEdmStructuralProperty);
            }
        }
        #endregion

        #region virtual method
        protected virtual IEnumerable<(string ForeignKeyName,
            string ParentSchemaName,
            string ParentName,
            string ParentColumnName,
            string RefrencedName,
            string RefrencedSchemaName,
            string RefrencedColumnName

            )> GetRelationship()
        {
            yield return ("", "", "", "", "", "", "");
        }
        /// <summary>
        /// Should order by SchemaName and TableName
        /// </summary>
        /// <returns></returns>
        protected virtual IEnumerable<(string SchemaName,
            string TableName,
            string ColumnName,
            string DataType,
            bool isKey)> GetTables()
        {
            yield return ("", "", "", "", false);
        }
        /// <summary>
        /// should order by SchemaName and ViewName
        /// </summary>
        /// <returns></returns>
        protected virtual IEnumerable<(string SchemaName,
            string ViewName,
            string ColumnName,
            string DataType,
            bool isKey)> GetViews()
        {
            yield return ("", "", "", "", false);
        }
        /// <summary>
        /// should order by SchemaName and StoredProcedureName
        /// </summary>
        /// <returns></returns>
        protected virtual IEnumerable<(string SchemaName,
            string StoredProcedureName,
            string ParameterName,
            string ParameterDataType,
            string ParemeterMode,
            string UserDefinedTypeSchema,
            string UserDefinedTypeName,
            int MaxLength,
            int NumericScale)> GetStoredProcedures()
        {
            yield return ("", "", "", "", "", "", "", 0, 0);
        }

        protected virtual IEnumerable<(string ColumnName,
            string DataType,
            int Length,
            bool isNullable)> GetUserDefinedType(string schema, string name)
        {
            yield return ("", "", 1, false);
        }
        /// <summary>
        /// should order by SchemaName and FunctionName
        /// </summary>
        /// <returns></returns>
        protected virtual IEnumerable<(string SchemaName,
            string FunctionName,
            string ParameterName,
            string ParameterDataType,
            string UserDefinedTypeSchema,
            string UserDefinedTypeName,
            int MaxLength,
            int NumericScale
            )> GetTableValuedFunction()
        {
            yield return ("", "", "", "", "", "", 0, 0);
        }
        protected virtual IEnumerable<(string ColumnName,
            string DataType,
            int Length,
            bool isNullable)> GetTableValueType(string schema, string name)
        {
            yield return ("", "", 1, false);
        }
        protected virtual EdmPrimitiveTypeKind? GetEdmType(string dbType)
        {
            return null;
        }
        #endregion



        #region IDataSource
        public Configuration Configuration { get; set; }

        public string Name { get; private set; }

        public EdmModel Model => throw new NotImplementedException();

        public Action<RequestInfo> BeforeExcute { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Func<RequestInfo, object, object> AfrerExcute { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
        #endregion
    }
}
