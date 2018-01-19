using System;
using System.Collections.Generic;
using System.Text;
using maskx.OData.Sql;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.OData.Edm;
using Newtonsoft.Json.Linq;

namespace maskx.OData.Sql
{
    public class MySQL : IDataSource
    {
        public Configuration Configuration { get; set; }
        readonly string ConnectionString;
        public string ModelCommand { get; private set; }
        private EdmModel BuildEdmModel()
        {
            var model = new EdmModel();
            var container = new EdmEntityContainer("ns", "container");
            model.AddElement(container);
            AddEdmElement(model);
            return model;
        }
        void AddEdmElement(EdmModel model)
        {
            EdmEntityContainer container = model.EntityContainer as EdmEntityContainer;
            string tableName = string.Empty;
            EdmEntityType t = null;
            IEdmEntitySet edmSet = null;
            using (var db = new MySQLDbAccess(this.ConnectionString))
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
        #region IDataSource
        public string Name { get; private set; }
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
