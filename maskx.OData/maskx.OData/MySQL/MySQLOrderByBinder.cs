using maskx.OData.Database;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Text;

namespace maskx.OData.MySQL
{
    public class MySQLOrderByBinder : OrderByBinder
    {

        protected override string BindPropertyAccessQueryNode(SingleValuePropertyAccessNode singleValuePropertyAccessNode)
        {
            return string.Format("`{0}`", singleValuePropertyAccessNode.Property.Name);
        }
        protected override string BindRangeVariable(ResourceRangeVariable entityRangeVariable)
        {
            return string.Format("`{0}`", entityRangeVariable.Name);
        }
    }
}
