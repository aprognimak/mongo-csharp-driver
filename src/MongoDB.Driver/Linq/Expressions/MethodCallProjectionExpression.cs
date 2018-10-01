using System;
using System.Linq.Expressions;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver.Linq.Expressions;

namespace MongoDB.Driver.Linq.Expressions
{
    internal class MethodCallProjectionExpression : SerializationExpression
    {
        private IBsonSerializer _outputSerializer;

        public LambdaExpression ProjectorLambda { get; }

        internal MethodCallProjectionExpression(IBsonSerializer inputSerializer, LambdaExpression projectorLambda)
        {
            ProjectorLambda = projectorLambda;

            var func = projectorLambda.Compile();

            _outputSerializer = (IBsonSerializer)Activator.CreateInstance(
                typeof(ProjectingDeserializer<,>).MakeGenericType(inputSerializer.ValueType, projectorLambda.ReturnType),
                new object[]
                {
                    inputSerializer,
                    func
                });

        }

        public override ExtensionExpressionType ExtensionType => ExtensionExpressionType.MethodCallProjection;

        public override IBsonSerializer Serializer => _outputSerializer;

        public override Type Type => _outputSerializer.ValueType;

        protected internal override Expression Accept(ExtensionExpressionVisitor visitor)
        {
            return this;
        }
    }

}