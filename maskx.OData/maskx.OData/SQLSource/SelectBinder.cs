using Microsoft.AspNet.OData.Query;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Text;

namespace maskx.OData.SQLSource
{
    public static class SelectBinder
    {
        internal static string ParseSelect(this ODataQueryOptions options, DbUtility utility)
        {
            if (options.Count != null)
                return "count(0)";
            if (options.SelectExpand == null)
                return "*";
            if (options.SelectExpand.SelectExpandClause.AllSelected)
                return "*";
            List<string> s = new List<string>();
            PathSelectItem select = null;
            foreach (var item in options.SelectExpand.SelectExpandClause.SelectedItems)
            {
                select = item as PathSelectItem;
                if (select != null)
                {
                    foreach (PropertySegment path in select.SelectedPath)
                    {
                        s.Add(utility.SafeDbObject(path.Property.Name));
                    }
                }
            }
            return string.Join(",", s);
        }
        internal static string ParseSelect(this ExpandedNavigationSelectItem expanded, DbUtility utility)
        {
            if (expanded.CountOption.HasValue)
                return "count(0)";
            if (expanded.SelectAndExpand == null)
                return "*";
            if (expanded.SelectAndExpand.AllSelected)
                return "*";
            List<string> s = new List<string>();
            PathSelectItem select = null;
            foreach (var item in expanded.SelectAndExpand.SelectedItems)
            {
                select = item as PathSelectItem;
                if (select != null)
                {
                    foreach (PropertySegment path in select.SelectedPath)
                    {
                        s.Add(utility.SafeDbObject(path.Property.Name));
                    }
                }
            }
            return string.Join(",", s);
        }
    }
}
