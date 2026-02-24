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
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Unity.MCP.API
{
    public partial class Tool_Type
    {
        public const string TypeGetJsonSchemaToolId = "type-get-json-schema";
        [McpPluginTool
        (
            TypeGetJsonSchemaToolId,
            Title = "Type / Get Json Schema"
        )]
        [Description("Generates a JSON Schema for a given C# type name using reflection. " +
            "Supports primitives, enums, arrays, generic collections, dictionaries, and complex objects. " +
            "The type must be present in any loaded assembly. " +
            "Use the full type name (e.g. 'UnityEngine.Vector3') for best results.")]
        public string GetJsonSchema
        (
            [Description("Full C# type name to generate the schema for. " +
                "Examples: 'System.String', 'UnityEngine.Vector3', 'System.Collections.Generic.List`1[[System.Int32]]'. " +
                "Simple names like 'Vector3' are also accepted when unambiguous.")]
            string typeName,

            [Description("Include the type-level description from [Description] attribute in the schema. Default: false.")]
            bool includeDescription = false,

            [Description("Include descriptions from [Description] attributes on properties and fields in the schema. Default: false.")]
            bool includePropertyDescription = false,

            [Description("When true, complex nested types are extracted into '$defs' and referenced via '$ref' " +
                "instead of being inlined. Useful for large or recursive types. Default: false.")]
            bool includeNestedTypes = false
        )
        {
            var type = ResolveType(typeName);
            if (type == null)
                return $"[Error] Type '{typeName}' not found in any loaded assembly. " +
                    "Use the full type name including namespace (e.g. 'UnityEngine.Vector3').";

            var ctx = new SchemaGenerationContext
            {
                IncludeDescription = includeDescription,
                IncludePropertyDescription = includePropertyDescription,
                IncludeNestedTypes = includeNestedTypes
            };

            var schema = BuildSchema(type, ctx);

            if (includeNestedTypes && ctx.PendingDefinitions.Count > 0)
            {
                var defs = new JsonObject();

                foreach (var defType in ctx.PendingDefinitions)
                {
                    var defCtx = new SchemaGenerationContext
                    {
                        IncludeDescription = includeDescription,
                        IncludePropertyDescription = includePropertyDescription,
                        IncludeNestedTypes = includeNestedTypes
                    };
                    var defSchema = BuildSchema(defType, defCtx);
                    defs[GetSchemaName(defType)] = defSchema;
                }

                schema["$defs"] = defs;
            }

            var json = schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

            return $"[Success] JSON Schema for '{type.FullName}':\n```json\n{json}\n```";
        }
    }
}
