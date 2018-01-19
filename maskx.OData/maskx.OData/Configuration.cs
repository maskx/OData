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
        }
        /// <summary>
        /// Defalut Schema, default is dbo
        /// </summary>
        public string DefaultSchema
        {
            get; set;
        }
        /// <summary>
        /// Support Multiple Schema,default is false
        /// </summary>
        public bool SupportMultiSchema { get; set; }
    }
}
