using Microsoft.OData.Core.UriParser.Semantic;
using Microsoft.OData.Core.UriParser.TreeNodeKinds;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.OData.Query;

namespace maskx.OData.Sql
{
    /// <summary>
    /// reference:
    /// https://aspnet.codeplex.com/SourceControl/changeset/view/72014f4c779e#Samples/WebApi/NHibernateQueryableSample/System.Web.Http.OData.NHibernate/NHibernateFilterBinder.cs
    /// </summary>
    class SQLFilterBinder
    {
        private IEdmModel _model;

        protected SQLFilterBinder(IEdmModel model)
        {
            _model = model;
        }

        public static string BindFilterQueryOption(FilterQueryOption filterQuery)
        {
            if (filterQuery != null)
            {
                SQLFilterBinder binder = new SQLFilterBinder(filterQuery.Context.Model);
                return binder.BindFilter(filterQuery) + Environment.NewLine;
            }
            return string.Empty;
        }
        public static string BindFilterQueryOption(FilterClause filterClause, IEdmModel model)
        {
            if (filterClause == null || model == null)
                return string.Empty;
            SQLFilterBinder binder = new SQLFilterBinder(model);
            return binder.BindFilterClause(filterClause) + Environment.NewLine;
        }
        protected string BindFilter(FilterQueryOption filterQuery)
        {
            return BindFilterClause(filterQuery.FilterClause);
        }

        protected string BindFilterClause(FilterClause filterClause)
        {
            return Bind(filterClause.Expression);
        }

        protected string Bind(QueryNode node)
        {
            CollectionNode collectionNode = node as CollectionNode;
            SingleValueNode singleValueNode = node as SingleValueNode;

            if (collectionNode != null)
            {
                switch (node.Kind)
                {
                    case QueryNodeKind.CollectionNavigationNode:
                        CollectionNavigationNode navigationNode = node as CollectionNavigationNode;
                        return BindNavigationPropertyNode(navigationNode.Source, navigationNode.NavigationProperty);

                    case QueryNodeKind.CollectionPropertyAccess:
                        return BindCollectionPropertyAccessNode(node as CollectionPropertyAccessNode);
                }
            }
            else if (singleValueNode != null)
            {
                switch (node.Kind)
                {
                    case QueryNodeKind.BinaryOperator:
                        return BindBinaryOperatorNode(node as BinaryOperatorNode);

                    case QueryNodeKind.Constant:
                        return BindConstantNode(node as ConstantNode);

                    case QueryNodeKind.Convert:
                        return BindConvertNode(node as ConvertNode);

                    case QueryNodeKind.EntityRangeVariableReference:
                        return BindRangeVariable((node as EntityRangeVariableReferenceNode).RangeVariable);

                    case QueryNodeKind.NonentityRangeVariableReference:
                        return BindRangeVariable((node as NonentityRangeVariableReferenceNode).RangeVariable);

                    case QueryNodeKind.SingleValuePropertyAccess:
                        return BindPropertyAccessQueryNode(node as SingleValuePropertyAccessNode);

                    case QueryNodeKind.UnaryOperator:
                        return BindUnaryOperatorNode(node as UnaryOperatorNode);

                    case QueryNodeKind.SingleValueFunctionCall:
                        return BindSingleValueFunctionCallNode(node as SingleValueFunctionCallNode);

                    case QueryNodeKind.SingleNavigationNode:
                        SingleNavigationNode navigationNode = node as SingleNavigationNode;
                        return BindNavigationPropertyNode(navigationNode.Source, navigationNode.NavigationProperty);

                    case QueryNodeKind.Any:
                        return BindAnyNode(node as AnyNode);

                    case QueryNodeKind.All:
                        return BindAllNode(node as AllNode);
                }
            }

            throw new NotSupportedException(String.Format("Nodes of type {0} are not supported", node.Kind));
        }

        private string BindCollectionPropertyAccessNode(CollectionPropertyAccessNode collectionPropertyAccessNode)
        {
            return collectionPropertyAccessNode.Property.Name;
            //return Bind(collectionPropertyAccessNode.Source) + "." + collectionPropertyAccessNode.Property.Name;
        }

        private string BindNavigationPropertyNode(SingleValueNode singleValueNode, IEdmNavigationProperty edmNavigationProperty)
        {
            return Bind(singleValueNode) + "." + edmNavigationProperty.Name;
        }

        private string BindAllNode(AllNode allNode)
        {
            string innerQuery = "not exists ( from " + Bind(allNode.Source) + " " + allNode.RangeVariables.First().Name;
            innerQuery += " where NOT(" + Bind(allNode.Body) + ")";
            return innerQuery + ")";
        }

        private string BindAnyNode(AnyNode anyNode)
        {
            string innerQuery = "exists ( from " + Bind(anyNode.Source) + " " + anyNode.RangeVariables.First().Name;
            if (anyNode.Body != null)
            {
                innerQuery += " where " + Bind(anyNode.Body);
            }
            return innerQuery + ")";
        }

        private string BindNavigationPropertyNode(SingleEntityNode singleEntityNode, IEdmNavigationProperty edmNavigationProperty)
        {
            return Bind(singleEntityNode) + "." + edmNavigationProperty.Name;
        }

        private string BindSingleValueFunctionCallNode(SingleValueFunctionCallNode singleValueFunctionCallNode)
        {
            var arguments = singleValueFunctionCallNode.Parameters.ToList();
            switch (singleValueFunctionCallNode.Name)
            {
                case "concat":
                    return singleValueFunctionCallNode.Name + "(" + Bind(arguments[0]) + "," + Bind(arguments[1]) + ")";
                case "contains":
                    return string.Format("{0} like '%{1}%'", Bind(arguments[0]) , (arguments[1] as ConstantNode).Value);
                case "length":
                case "trim":
                case "year":
                case "years":
                case "month":
                case "months":
                case "day":
                case "days":
                case "hour":
                case "hours":
                case "minute":
                case "minutes":
                case "second":
                case "seconds":
                case "round":
                case "floor":
                case "ceiling":
                    return singleValueFunctionCallNode.Name + "(" + Bind(arguments[0]) + ")";
                default:
                    throw new NotImplementedException();
            }
        }

        private string BindUnaryOperatorNode(UnaryOperatorNode unaryOperatorNode)
        {
            return ToString(unaryOperatorNode.OperatorKind) + "(" + Bind(unaryOperatorNode.Operand) + ")";
        }

        private string BindPropertyAccessQueryNode(SingleValuePropertyAccessNode singleValuePropertyAccessNode)
        {
            return singleValuePropertyAccessNode.Property.Name;
            //  return Bind(singleValuePropertyAccessNode.Source) + "." + singleValuePropertyAccessNode.Property.Name;
        }

        private string BindRangeVariable(NonentityRangeVariable nonentityRangeVariable)
        {
            return nonentityRangeVariable.Name.ToString();
        }

        private string BindRangeVariable(EntityRangeVariable entityRangeVariable)
        {
            return entityRangeVariable.Name.ToString();
        }

        private string BindConvertNode(ConvertNode convertNode)
        {
            return Bind(convertNode.Source);
        }

        private string BindConstantNode(ConstantNode constantNode)
        {
            if (constantNode.Value is string)
            {
                return String.Format("'{0}'", constantNode.Value);
            }
            if (constantNode.Value is DateTimeOffset)
            {
                return String.Format("'{0}'", ((DateTimeOffset)constantNode.Value).ToString("yyyy-MM-dd HH:mm:ss"));
            }
            if (constantNode.Value is Guid)
                return String.Format("'{0}'", constantNode.Value);
            if (constantNode.Value is bool)
                return (bool)constantNode.Value ? "1" : "0";
            return constantNode.Value.ToString();
        }

        private string BindBinaryOperatorNode(BinaryOperatorNode binaryOperatorNode)
        {
            var left = Bind(binaryOperatorNode.Left);
            var right = Bind(binaryOperatorNode.Right);
            return "(" + left + " " + ToString(binaryOperatorNode.OperatorKind) + " " + right + ")";
        }

        private string ToString(BinaryOperatorKind binaryOpertor)
        {
            switch (binaryOpertor)
            {
                case BinaryOperatorKind.Add:
                    return "+";
                case BinaryOperatorKind.And:
                    return "AND";
                case BinaryOperatorKind.Divide:
                    return "/";
                case BinaryOperatorKind.Equal:
                    return "=";
                case BinaryOperatorKind.GreaterThan:
                    return ">";
                case BinaryOperatorKind.GreaterThanOrEqual:
                    return ">=";
                case BinaryOperatorKind.LessThan:
                    return "<";
                case BinaryOperatorKind.LessThanOrEqual:
                    return "<=";
                case BinaryOperatorKind.Modulo:
                    return "%";
                case BinaryOperatorKind.Multiply:
                    return "*";
                case BinaryOperatorKind.NotEqual:
                    return "<>";
                case BinaryOperatorKind.Or:
                    return "OR";
                case BinaryOperatorKind.Subtract:
                    return "-";
                default:
                    return null;
            }
        }

        private string ToString(UnaryOperatorKind unaryOperator)
        {
            switch (unaryOperator)
            {
                case UnaryOperatorKind.Negate:
                    return "!";
                case UnaryOperatorKind.Not:
                    return "NOT";
                default:
                    return null;
            }
        }

    }
}
