using Microsoft.AspNetCore.OData.Query;
using Microsoft.OData.UriParser;

namespace maskx.OData.SQLSource
{
    public static class OrderByBinder
    {
        public static string ParseOrderBy(this ODataQueryOptions options, SQLBase dbUtility)
        {
            if (options.Count != null
                || options.OrderBy == null
                || options.OrderBy.OrderByClause == null)
            {
                return string.Empty;
            }
            return BindOrderByClause(options.OrderBy.OrderByClause, dbUtility);
        }
        public static string ParseOrderBy(this ExpandedNavigationSelectItem expanded, SQLBase dbUtility)
        {
            if (expanded.CountOption.HasValue)
                return string.Empty;
            if (expanded.OrderByOption == null)
                return string.Empty;
            return BindOrderByClause(expanded.OrderByOption, dbUtility);
        }
        static string BindOrderByClause(OrderByClause orderByClause, SQLBase dbUtility)
        {
            string orderby = string.Format("{0} {1}", Bind(orderByClause.Expression, dbUtility), GetDirection(orderByClause.Direction));
            if (orderByClause.ThenBy != null)
                orderby += "," + BindOrderByClause(orderByClause.ThenBy, dbUtility);
            return orderby;
        }
        static string GetDirection(OrderByDirection dir)
        {
            if (dir == OrderByDirection.Ascending)
                return "asc";
            return "desc";
        }
        static string Bind(QueryNode node, SQLBase dbUtility)
        {
            if (node is SingleValueNode singleValueNode)
            {
                switch (singleValueNode.Kind)
                {
                    case QueryNodeKind.ResourceRangeVariableReference:
                        return BindRangeVariable((node as ResourceRangeVariableReferenceNode).RangeVariable, dbUtility);
                    case QueryNodeKind.SingleValuePropertyAccess:
                        return BindPropertyAccessQueryNode(node as SingleValuePropertyAccessNode, dbUtility);
                    default:
                        return string.Empty;
                }
            }
            return string.Empty;
        }
        static string BindPropertyAccessQueryNode(SingleValuePropertyAccessNode singleValuePropertyAccessNode, SQLBase dbUtility)
        {
            return dbUtility.SafeDbObject(singleValuePropertyAccessNode.Property.Name);
        }
        static string BindRangeVariable(ResourceRangeVariable entityRangeVariable, SQLBase dbUtility)
        {
            return dbUtility.SafeDbObject(entityRangeVariable.Name);
        }
    }
}
