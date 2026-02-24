/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Unity-MCP)    │
│  Copyright (c) 2025 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/

#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Unity.MCP.API
{
    [McpPluginToolType]
    public partial class Tool_Type
    {
        static Type? ResolveType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null) return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetTypes().FirstOrDefault(t => t.FullName == typeName || t.Name == typeName);
                if (type != null) return type;
            }

            return null;
        }

        static string GetSchemaName(Type type)
        {
            if (type.IsGenericType)
            {
                var genericName = type.GetGenericTypeDefinition().Name;
                var backtickIdx = genericName.IndexOf('`');
                if (backtickIdx > 0) genericName = genericName[..backtickIdx];
                var argNames = string.Join("_", type.GetGenericArguments().Select(GetSchemaName));
                return $"{genericName}_{argNames}";
            }
            return type.Name;
        }

        static string? GetDescriptionText(MemberInfo member)
        {
            var attr = member.GetCustomAttribute<DescriptionAttribute>();
            return attr?.Description;
        }

        static bool IsPrimitiveOrSimple(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive
                || type == typeof(string)
                || type == typeof(decimal)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type == typeof(Guid)
                || type.IsEnum;
        }

        static bool IsNullableType(Type type)
            => Nullable.GetUnderlyingType(type) != null || !type.IsValueType;

        static bool IsCollectionType(Type type)
        {
            if (!type.IsGenericType) return false;
            var def = type.GetGenericTypeDefinition();
            return def == typeof(List<>)
                || def == typeof(IList<>)
                || def == typeof(IEnumerable<>)
                || def == typeof(ICollection<>)
                || def == typeof(HashSet<>)
                || def == typeof(IReadOnlyList<>)
                || def == typeof(IReadOnlyCollection<>);
        }

        static JsonObject BuildSchemaOrRef(Type type, SchemaGenerationContext ctx)
        {
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

            if (!ctx.IncludeNestedTypes
                || IsPrimitiveOrSimple(underlyingType)
                || underlyingType == typeof(object)
                || underlyingType.IsArray
                || IsCollectionType(underlyingType))
            {
                return BuildSchema(type, ctx);
            }

            if (underlyingType.IsGenericType
                && underlyingType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                return BuildSchema(type, ctx);
            }

            var schemaName = GetSchemaName(underlyingType);
            ctx.PendingDefinitions.Add(underlyingType);

            var refSchema = new JsonObject
            {
                ["$ref"] = JsonValue.Create($"#/$defs/{schemaName}")
            };

            if (IsNullableType(type))
                refSchema["nullable"] = JsonValue.Create(true);

            return refSchema;
        }

        static JsonObject BuildSchema(Type type, SchemaGenerationContext ctx)
        {
            var schema = new JsonObject();

            var underlyingNullable = Nullable.GetUnderlyingType(type);
            if (underlyingNullable != null)
            {
                var innerSchema = BuildSchema(underlyingNullable, ctx);
                innerSchema["nullable"] = JsonValue.Create(true);
                return innerSchema;
            }

            if (type == typeof(string) || type == typeof(char))
            {
                schema["type"] = JsonValue.Create("string");
                return schema;
            }
            if (type == typeof(bool))
            {
                schema["type"] = JsonValue.Create("boolean");
                return schema;
            }
            if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short)
                || type == typeof(ushort) || type == typeof(int) || type == typeof(uint)
                || type == typeof(long) || type == typeof(ulong))
            {
                schema["type"] = JsonValue.Create("integer");
                return schema;
            }
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            {
                schema["type"] = JsonValue.Create("number");
                return schema;
            }
            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            {
                schema["type"] = JsonValue.Create("string");
                schema["format"] = JsonValue.Create("date-time");
                return schema;
            }
            if (type == typeof(Guid))
            {
                schema["type"] = JsonValue.Create("string");
                schema["format"] = JsonValue.Create("uuid");
                return schema;
            }
            if (type == typeof(object))
            {
                schema["type"] = JsonValue.Create("object");
                return schema;
            }

            if (type.IsEnum)
            {
                schema["type"] = JsonValue.Create("string");
                var enumArray = new JsonArray();
                foreach (var name in Enum.GetNames(type))
                    enumArray.Add(JsonValue.Create(name));
                schema["enum"] = enumArray;

                if (ctx.IncludeDescription)
                {
                    var desc = GetDescriptionText(type);
                    if (!string.IsNullOrEmpty(desc))
                        schema["description"] = JsonValue.Create(desc);
                }
                return schema;
            }

            if (type.IsArray)
            {
                schema["type"] = JsonValue.Create("array");
                var elementType = type.GetElementType()!;
                schema["items"] = BuildSchemaOrRef(elementType, ctx);
                return schema;
            }

            if (type.IsGenericType)
            {
                var typeArgs = type.GetGenericArguments();

                if (IsCollectionType(type))
                {
                    schema["type"] = JsonValue.Create("array");
                    schema["items"] = BuildSchemaOrRef(typeArgs[0], ctx);
                    return schema;
                }

                var genericDef = type.GetGenericTypeDefinition();
                if (genericDef == typeof(Dictionary<,>)
                    || genericDef == typeof(IDictionary<,>))
                {
                    schema["type"] = JsonValue.Create("object");
                    schema["additionalProperties"] = BuildSchemaOrRef(typeArgs[1], ctx);
                    return schema;
                }
            }

            return BuildObjectSchema(type, ctx);
        }

        static JsonObject BuildObjectSchema(Type type, SchemaGenerationContext ctx)
        {
            var schema = new JsonObject();
            schema["type"] = JsonValue.Create("object");

            if (ctx.CurrentlyProcessing.Contains(type))
                return schema;

            ctx.CurrentlyProcessing.Add(type);

            if (ctx.IncludeDescription)
            {
                var typeDesc = GetDescriptionText(type);
                if (!string.IsNullOrEmpty(typeDesc))
                    schema["description"] = JsonValue.Create(typeDesc);
            }

            var properties = new JsonObject();
            var required = new JsonArray();

            foreach (var prop in type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
            {
                var propSchema = BuildSchemaOrRef(prop.PropertyType, ctx);

                if (ctx.IncludePropertyDescription)
                {
                    var desc = GetDescriptionText(prop);
                    if (!string.IsNullOrEmpty(desc))
                        propSchema["description"] = JsonValue.Create(desc);
                }

                properties[prop.Name] = propSchema;

                if (!IsNullableType(prop.PropertyType))
                    required.Add(JsonValue.Create(prop.Name));
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var fieldSchema = BuildSchemaOrRef(field.FieldType, ctx);

                if (ctx.IncludePropertyDescription)
                {
                    var desc = GetDescriptionText(field);
                    if (!string.IsNullOrEmpty(desc))
                        fieldSchema["description"] = JsonValue.Create(desc);
                }

                properties[field.Name] = fieldSchema;

                if (!IsNullableType(field.FieldType))
                    required.Add(JsonValue.Create(field.Name));
            }

            if (properties.Count > 0)
                schema["properties"] = properties;

            if (required.Count > 0)
                schema["required"] = required;

            ctx.CurrentlyProcessing.Remove(type);

            return schema;
        }

        sealed class SchemaGenerationContext
        {
            public bool IncludeDescription { get; init; }
            public bool IncludePropertyDescription { get; init; }
            public bool IncludeNestedTypes { get; init; }
            public HashSet<Type> CurrentlyProcessing { get; } = new();
            public HashSet<Type> PendingDefinitions { get; } = new();
        }
    }
}
