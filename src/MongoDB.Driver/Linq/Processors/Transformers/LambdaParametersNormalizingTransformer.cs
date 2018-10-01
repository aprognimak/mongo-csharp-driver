using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using MongoDB.Driver.Linq.Expressions;

namespace MongoDB.Driver.Linq.Processors.Transformers
{
    internal sealed class LambdaParametersNormalizingTransformer : IExpressionTransformer<LambdaExpression>
    {
        private static readonly ExpressionType[] NodeTypes = new ExpressionType[] { ExpressionType.Lambda };

        public ExpressionType[] SupportedNodeTypes => NodeTypes;

        public Expression Transform(LambdaExpression node)
        {
            if (node.Parameters.Select(e => e.Name).Distinct().Count() == node.Parameters.Count)
            {
                if (node.Parameters.Count(e => e.Name.Contains(".")) == 0)
                {
                    return node;
                }
            }

            var replacedParameters = new Dictionary<ParameterExpression, ParameterExpression>();

            for (var index = 0; index < node.Parameters.Count; index++)
            {
                var parameterExpression = node.Parameters[index];
                var parameterName = parameterExpression.Name;

                if (parameterName.Contains("."))
                {
                    //Cannot have that, effs up the deserialization and aggregation when joining stuff
                    parameterName = parameterName.Replace(".", "_");
                }

                var modifiedParam = Expression.Parameter(parameterExpression.Type, $"{parameterName}{index}");
                replacedParameters[parameterExpression] = modifiedParam;
            }

            Expression newBody = node.Body;
            foreach (var kvp in replacedParameters)
            {
                newBody = ExpressionReplacer.Replace(newBody, kvp.Key, kvp.Value);
            }

            var newLambda = Expression.Lambda(newBody, replacedParameters.Values);

            return newLambda;
        }
    }
}