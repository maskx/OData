using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Microsoft.OData.Edm;
using Newtonsoft.Json.Linq;

namespace maskx.OData.SQL
{
    public class MySQL : IDataSource
    {
        private EdmModel BuildEdmModel()
        {
            return null;
        }
        #region IDataSource
        public string Name { get; private set; }
        EdmModel _Model = null;
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
