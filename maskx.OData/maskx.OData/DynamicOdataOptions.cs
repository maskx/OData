using System.Collections.Generic;

namespace maskx.OData
{
    public class DynamicODataOptions
    {
        public IReadOnlyDictionary<string, IDataSource> DataSources { get; internal set; } 
        /// <summary>
        /// Defalut Schema name, default is dbo
        /// </summary>
        public string DefaultSchema { get; set; } = "dbo";
    }
}
