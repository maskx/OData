using Microsoft.OData.Core.UriParser.Semantic;
using Microsoft.OData.Edm.Library;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.OData;
using System.Web.OData.Query;

namespace maskx.OData.Sql
{
    static class Extensions
    {
        internal static bool IsDBNull(this DbDataReader reader, string columnName)
        {
            return Convert.IsDBNull(reader[columnName]);
        }
        internal static string ParseSelect(this ODataQueryOptions options)
        {
            if (options.Count != null)
                return "count(*)";
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
                        s.Add(string.Format("[{0}]", path.Property.Name));
                    }
                }
            }
            return string.Join(",", s);
        }
        internal static string ParseSelect(this ExpandedNavigationSelectItem expanded)
        {
            if (expanded.CountOption.HasValue)
                return "count(*)";
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
                        s.Add(string.Format("[{0}]", path.Property.Name));
                    }
                }
            }
            return string.Join(",", s);
        }
        internal static string ParseOrderBy(this ODataQueryOptions options)
        {
            if (options.Count != null)
                return string.Empty;
            if (options.OrderBy == null)
                return string.Empty;
            return SQLOrderByBinder.BindOrderByQueryOption(options.OrderBy.OrderByClause);

        }
        internal static string ParseOrderBy(this ExpandedNavigationSelectItem expanded)
        {
            if (expanded.CountOption.HasValue)
                return string.Empty;
            if (expanded.OrderByOption == null)
                return string.Empty;
            return SQLOrderByBinder.BindOrderByQueryOption(expanded.OrderByOption);

        }
        internal static string ParseWhere(this ODataQueryOptions options)
        {
            if (options.Filter == null)
                return string.Empty;
            string where= SQLFilterBinder.BindFilterQueryOption(options.Filter.FilterClause, options.Context.Model);
            if (string.IsNullOrEmpty(where))
                where += " where " + where;
            return where;
        }
        internal static string ParseWhere(this ExpandedNavigationSelectItem expanded,string condition, EdmModel model)
        {
            string where = SQLFilterBinder.BindFilterQueryOption(expanded.FilterOption,model);
            if (string.IsNullOrEmpty(where))
            {
                where = condition;
            }
            else if (!string.IsNullOrEmpty(condition))
            {
                where = string.Format("({0}) and ({1})", condition, where);
            }

            if (!string.IsNullOrEmpty(where))
            {
                where = " where " + where;
            }
            return where;
        }
        internal static void SetEntityPropertyValue(this DbDataReader reader, int fieldIndex, EdmStructuredObject entity)
        {
            string name = reader.GetName(fieldIndex);
            if (reader.IsDBNull(fieldIndex))
            {
                entity.TrySetPropertyValue(name, null);
                return;
            }
            if (reader.GetFieldType(fieldIndex) == typeof(DateTime))
            {
                entity.TrySetPropertyValue(name, new DateTimeOffset(reader.GetDateTime(fieldIndex)));
            }
            else
            {
                entity.TrySetPropertyValue(name, reader.GetValue(fieldIndex));
            }
        }

    }
}
