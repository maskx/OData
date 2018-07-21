using System;
using System.Collections.Generic;
using System.Text;

namespace maskx.OData
{
    public class Configuration
    {
        public Configuration()
        {
            DefaultSchema = "dbo";
            SupportMultiSchema = false;
            LowerName = false;
        }
        /// <summary>
        /// Defalut Schema name, default is dbo
        /// </summary>
        public string DefaultSchema
        {
            get; set;
        }
        /// <summary>
        /// Support Multiple Schema,default is false
        /// </summary>
        public bool SupportMultiSchema { get; set; }
        /// <summary>
        /// make the name of database object to lower, default is false
        /// </summary>
        public bool LowerName { get; set; }
    }
}
