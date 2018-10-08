/* Copyright 2015-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver.Linq.Expressions;
using MongoDB.Driver.Support;

namespace MongoDB.Driver.Linq.Processors.Pipeline.MethodCallBinders
{
    internal sealed class JoinBinder : IMethodCallBinder<PipelineBindingContext>
    {
        public static IEnumerable<MethodInfo> GetSupportedMethods()
        {
            yield return MethodHelper.GetMethodDefinition(() => Enumerable.Join<object, object, object, object>(null, null, null, null, null));
            yield return MethodHelper.GetMethodDefinition(() => Queryable.Join<object, object, object, object>(null, null, null, null, null));
            yield return MethodHelper.GetMethodDefinition(() => Enumerable.GroupJoin<object, object, object, object>(null, null, null, null, null));
            yield return MethodHelper.GetMethodDefinition(() => Queryable.GroupJoin<object, object, object, object>(null, null, null, null, null));
        }

        public Expression Bind(PipelineExpression pipeline, PipelineBindingContext bindingContext, MethodCallExpression node, IEnumerable<Expression> arguments)
        {
            var args = arguments.ToList();
            var joined = bindingContext.Bind(args[0]) as SerializationExpression;
            if (joined == null)
            {
                throw new NotSupportedException("The joined collection cannot have any qualifiers.");
            }

            var sourceKeySelectorLambda = ExpressionHelper.GetLambda(args[1]);
            bindingContext.AddExpressionMapping(sourceKeySelectorLambda.Parameters[0], pipeline.Projector);
            var sourceKeySelector = bindingContext.Bind(sourceKeySelectorLambda.Body);
            if (sourceKeySelector.NodeType == ExpressionType.Convert)
            {
                //Hack?
                sourceKeySelector = (sourceKeySelector as UnaryExpression).Operand;
            }

            if (sourceKeySelector == null)
            {
                var message = string.Format("Unable to determine the serialization information for the outer key selector in the tree: {0}", node.ToString());
                throw new NotSupportedException(message);
            }

            var joinedArraySerializer = joined.Serializer as IBsonArraySerializer;
            BsonSerializationInfo joinedItemSerializationInfo;
            if (joinedArraySerializer == null || !joinedArraySerializer.TryGetItemSerializationInfo(out joinedItemSerializationInfo))
            {
                var message = string.Format("Unable to determine the serialization information for the inner collection: {0}", node.ToString());
                throw new NotSupportedException(message);
            }

            var joinedKeySelectorLambda = ExpressionHelper.GetLambda(args[2]);
            var joinedDocument = new DocumentExpression(joinedItemSerializationInfo.Serializer);
            bindingContext.AddExpressionMapping(joinedKeySelectorLambda.Parameters[0], joinedDocument);
            var joinedKeySelector = bindingContext.Bind(joinedKeySelectorLambda.Body);
            if (joinedKeySelector.NodeType == ExpressionType.Convert)
            {
                //Hack?
                joinedKeySelector = (joinedKeySelector as UnaryExpression).Operand;
            }

            if (joinedKeySelector == null)
            {
                var message = string.Format("Unable to determine the serialization information for the inner key selector in the tree: {0}", node.ToString());
                throw new NotSupportedException(message);
            }

            var resultSelectorLambda = ExpressionHelper.GetLambda(args[3]);

            var sourceSerializer = pipeline.Projector.Serializer;
            var joinedSerializer = node.Method.Name == "GroupJoin" ?
                joined.Serializer :
                joinedItemSerializationInfo.Serializer;
            var sourceDocument = new DocumentExpression(sourceSerializer);
            var joinedField = new FieldExpression(
                resultSelectorLambda.Parameters[1].Name,
                joinedSerializer);

            bindingContext.AddExpressionMapping(
                resultSelectorLambda.Parameters[0],
                sourceDocument);
            bindingContext.AddExpressionMapping(
                resultSelectorLambda.Parameters[1],
                joinedField);
            var resultSelector = bindingContext.Bind(resultSelectorLambda.Body);

            Expression source;
            if (node.Method.Name == "GroupJoin")
            {
                source = new GroupJoinExpression(
                    node.Type,
                    pipeline.Source,
                    joined,
                    (Expression)sourceKeySelector,
                    (Expression)joinedKeySelector,
                    resultSelectorLambda.Parameters[1].Name);
            }
            else
            {
                source = new JoinExpression(
                node.Type,
                pipeline.Source,
                joined,
                (Expression)sourceKeySelector,
                (Expression)joinedKeySelector,
                resultSelectorLambda.Parameters[1].Name);
            }

            SerializationExpression projector;
            var memberInitResultSelector = resultSelector as MemberInitExpression;
            var memberInitAssignments = memberInitResultSelector?.Bindings.Cast<MemberAssignment>().ToList();

            var newResultSelector = resultSelector as NewExpression;
            var methodCallResultSelector = resultSelector as MethodCallExpression;
            
            if (newResultSelector != null &&
                newResultSelector.Arguments.Count == 2 &&
                newResultSelector.Arguments[0] == sourceDocument &&
                newResultSelector.Arguments[1] == joinedField)
            {
                Func<object, object, object> creator = (s, j) => Activator.CreateInstance(resultSelector.Type, s, j);
                var serializer = (IBsonSerializer)Activator.CreateInstance(
                    typeof(JoinSerializer<>).MakeGenericType(resultSelector.Type),
                    sourceSerializer,
                    newResultSelector.Members[0].Name,
                    joinedSerializer,
                    newResultSelector.Members[1].Name,
                    resultSelectorLambda.Parameters[1].Name,
                    creator);

                projector = new DocumentExpression(serializer);
            }
            else if (memberInitResultSelector != null && 
                     memberInitAssignments.Count==2 && 
                     memberInitAssignments[0].Expression == sourceDocument &&
                     memberInitAssignments[1].Expression == joinedField)
            {
                var sourceMemberAssignment = memberInitAssignments[0];
                var joinedMemberAssignment = memberInitAssignments[1];

                var resultSelectorTypeInfo = resultSelector.Type.GetTypeInfo();
                var constructorWithMembers = resultSelectorTypeInfo.GetConstructor(new Type[]
                    {sourceMemberAssignment.Member.GetType(), joinedMemberAssignment.Member.GetType()});

                var propOrFieldSource = resultSelectorTypeInfo.GetMember(sourceMemberAssignment.Member.Name,
                    BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.IgnoreCase | BindingFlags.NonPublic | BindingFlags.Public |
                    BindingFlags.Instance).FirstOrDefault();

                var propOrFieldJoined = resultSelectorTypeInfo.GetMember(joinedMemberAssignment.Member.Name,
                    BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.IgnoreCase | BindingFlags.NonPublic | BindingFlags.Public |
                    BindingFlags.Instance).FirstOrDefault();

                bindingContext.AddMemberMapping(propOrFieldSource, sourceDocument);
                bindingContext.AddMemberMapping(propOrFieldJoined, joinedField);

                Func<object, object, object> creator = (s, j) =>
                {
                    if (constructorWithMembers != null)
                    {
                        var o = Activator.CreateInstance(resultSelector.Type, s, j);
                        return o;
                    }
                    else
                    {
                        var o = Activator.CreateInstance(resultSelector.Type);

                        var propInfoSource = propOrFieldSource as PropertyInfo;
                        propInfoSource?.SetValue(o, s);

                        var propInfoJoined = propOrFieldJoined as PropertyInfo;
                        propInfoJoined?.SetValue(o, j);

                        var fieldInfoSource = propOrFieldSource as FieldInfo;
                        fieldInfoSource?.SetValue(o, s);

                        var fieldInfoJoined = propOrFieldJoined as FieldInfo;
                        fieldInfoJoined?.SetValue(o, j);

                        return o;
                    }
                };

                var serializer = (IBsonSerializer)Activator.CreateInstance(
                    typeof(JoinSerializer<>).MakeGenericType(resultSelector.Type),
                    sourceSerializer,
                    memberInitAssignments[0].Member.Name,
                    joinedSerializer,
                    memberInitAssignments[1].Member.Name,
                    resultSelectorLambda.Parameters[1].Name,
                    creator);
                
                projector = new DocumentExpression(serializer);
            }
            else if (methodCallResultSelector != null)
            {
                var sourceArgument = resultSelectorLambda.Parameters[0];
                var joinedArgument = resultSelectorLambda.Parameters[1];

                var resultTypeInfo = resultSelectorLambda.ReturnType.GetTypeInfo();
                var resultTypeInfoProps = resultTypeInfo.GetProperties().Cast<MemberInfo>().ToList();
                if (!resultTypeInfoProps.Any())
                {
                    resultTypeInfoProps = resultTypeInfo.GetFields().Cast<MemberInfo>().ToList();
                }
                var sourceMemberInfo = resultTypeInfoProps[0];
                var joinedMemberInfo = resultTypeInfoProps[1];

                bindingContext.AddMemberMapping(sourceMemberInfo, sourceDocument);
                bindingContext.AddMemberMapping(joinedMemberInfo, joinedField);

                var methodCallCompiled = resultSelectorLambda.Compile();

                Func<object, object, object> creator = (s, j) =>
                    {
                        return methodCallCompiled.DynamicInvoke(s, j);
                    };

                var serializer = (IBsonSerializer)Activator.CreateInstance(
                    typeof(JoinSerializer<>).MakeGenericType(resultSelector.Type),
                    sourceSerializer,
                    sourceArgument.Name,
                    joinedSerializer,
                    joinedArgument.Name,
                    resultSelectorLambda.Parameters[1].Name, 
                    creator);

                projector = new DocumentExpression(serializer);
            }
            else
            {
                projector = bindingContext.BindProjector(ref resultSelector);
                source = new SelectExpression(
                    source,
                    "__i", // since this is a top-level pipeline, this doesn't matter
                    resultSelector);
            }

            return new PipelineExpression(
                source,
                projector);
        }
    }
}
