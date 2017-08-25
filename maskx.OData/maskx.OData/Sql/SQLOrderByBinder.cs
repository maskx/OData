
using Microsoft.OData.UriParser;
using System.Web.OData.Query;

namespace maskx.OData.Sql
{
    static class SQLOrderByBinder
    {
        internal static string ParseOrderBy(this ODataQueryOptions options)
        {
            if (options.Count != null
                || options.OrderBy == null
                || options.OrderBy.OrderByClause == null)
                return string.Empty;
            return "order by " + options.OrderBy.OrderByClause.BindOrderByClause();
        }
        internal static string ParseOrderBy(this ExpandedNavigationSelectItem expanded)
        {
            if (expanded.CountOption.HasValue)
                return string.Empty;
            if (expanded.OrderByOption == null)
                return string.Empty;
            return "order by " + expanded.OrderByOption.BindOrderByClause();
        }
        static string BindOrderByClause(this OrderByClause orderByClause)
        {
            string orderby = string.Format("[{0}] {1}", Bind(orderByClause.Expression), GetDirection(orderByClause.Direction));
            if (orderByClause.ThenBy != null)
                orderby += "," + BindOrderByClause(orderByClause.ThenBy);
            return orderby;
        }
        static string GetDirection(this OrderByDirection dir)
        {
            if (dir == OrderByDirection.Ascending)
                return "asc";
            return "desc";
        }
        static string Bind(QueryNode node)
        {
            CollectionNode collectionNode = node as CollectionNode;
            SingleValueNode singleValueNode = node as SingleValueNode;
            if (singleValueNode != null)
            {
                switch (singleValueNode.Kind)
                {
                    case QueryNodeKind.ResourceRangeVariableReference:
                        return BindRangeVariable((node as ResourceRangeVariableReferenceNode).RangeVariable);
                    case QueryNodeKind.SingleValuePropertyAccess:
                        return BindPropertyAccessQueryNode(node as SingleValuePropertyAccessNode);
                    default:
                        return string.Empty;
                }
            }
            return string.Empty;
        }
        static string BindPropertyAccessQueryNode(SingleValuePropertyAccessNode singleValuePropertyAccessNode)
        {
            return singleValuePropertyAccessNode.Property.Name;
        }
        static string BindRangeVariable(ResourceRangeVariable entityRangeVariable)
        {
            return entityRangeVariable.Name;
        }
    }
}
