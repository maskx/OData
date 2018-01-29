
using Microsoft.AspNet.OData.Query;
using Microsoft.OData.UriParser;

namespace maskx.OData.Database
{
    public abstract class OrderByBinder
    {
        public string ParseOrderBy(ODataQueryOptions options)
        {
            if (options.Count != null
                || options.OrderBy == null
                || options.OrderBy.OrderByClause == null)
                return string.Empty;
            return "order by " + BindOrderByClause(options.OrderBy.OrderByClause);
        }
        public string ParseOrderBy(ExpandedNavigationSelectItem expanded)
        {
            if (expanded.CountOption.HasValue)
                return string.Empty;
            if (expanded.OrderByOption == null)
                return string.Empty;
            return "order by " + BindOrderByClause(expanded.OrderByOption);
        }
        protected string BindOrderByClause(OrderByClause orderByClause)
        {
            string orderby = string.Format("{0} {1}", Bind(orderByClause.Expression), GetDirection(orderByClause.Direction));
            if (orderByClause.ThenBy != null)
                orderby += "," + BindOrderByClause(orderByClause.ThenBy);
            return orderby;
        }
        string GetDirection(OrderByDirection dir)
        {
            if (dir == OrderByDirection.Ascending)
                return "asc";
            return "desc";
        }
        string Bind(QueryNode node)
        {
            if (node is SingleValueNode singleValueNode)
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
        protected abstract string BindPropertyAccessQueryNode(SingleValuePropertyAccessNode singleValuePropertyAccessNode);
        protected abstract string BindRangeVariable(ResourceRangeVariable entityRangeVariable);
    }
}
