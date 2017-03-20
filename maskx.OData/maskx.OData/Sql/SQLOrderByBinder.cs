using Microsoft.OData.Core.UriParser;
using Microsoft.OData.Core.UriParser.Semantic;

namespace maskx.OData.Sql
{
    class SQLOrderByBinder
    {
        public static string BindOrderByQueryOption(OrderByClause orderByClause)
        {
            if (orderByClause == null)
                return string.Empty;
            return "order by " + BindOrderByClause(orderByClause);
        }
        static string BindOrderByClause(OrderByClause orderByClause)
        {
            string orderby = string.Format("[{0}] {1}", Bind(orderByClause.Expression), GetDirection(orderByClause.Direction));
            if (orderByClause.ThenBy != null)
                orderby += "," + BindOrderByClause(orderByClause.ThenBy);
            return orderby;
        }
        static string GetDirection(OrderByDirection dir)
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
                    case Microsoft.OData.Core.UriParser.TreeNodeKinds.QueryNodeKind.EntityRangeVariableReference:
                        return BindRangeVariable((node as EntityRangeVariableReferenceNode).RangeVariable);
                    case Microsoft.OData.Core.UriParser.TreeNodeKinds.QueryNodeKind.SingleValuePropertyAccess:
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
        static string BindRangeVariable(EntityRangeVariable entityRangeVariable)
        {
            return entityRangeVariable.Name.ToString();
        }
    }
}
