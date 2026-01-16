using System;
using System.Collections.Generic;
using System.Linq;
using AutoFactories.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoFactories.Extensions
{
    internal static class SymbolHelpers
    {
        private static Dictionary<string, string> s_alias;

        static SymbolHelpers()
        {
            s_alias = new Dictionary<string, string>();

            void AddAlias<T>(string alias)
                => s_alias[typeof(T).FullName] = alias;

            AddAlias<string>("string");
            AddAlias<int>("int");
            AddAlias<long>("long");
            AddAlias<float>("float");
            AddAlias<double>("double");
            AddAlias<bool>("bool");
            AddAlias<short>("short");
            AddAlias<uint>("uint");
            AddAlias<ulong>("ulong");
        }


        public static MetadataTypeName ResolveTypeName(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is INamedTypeSymbol namedType)
            {
                if (namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                {
                    var underlyingType = namedType.TypeArguments[0];
                    var resolved = ResolveTypeName(underlyingType);
                    return new MetadataTypeName(resolved.Name, resolved.Namespace, true, resolved.IsAlias);
                }

                string name = typeSymbol.Name;
                string @namespace = GetFullNamespace(namedType);
                bool isNullable = typeSymbol.NullableAnnotation == NullableAnnotation.Annotated;
                bool isAlias = false;

                if (s_alias.TryGetValue($"{@namespace}.{name}", out string alias))
                {
                    name = alias;
                    @namespace = "";
                    isAlias = true;
                }

                if (namedType.IsGenericType)
                {
                    string typeArguments = string.Join(", ", namedType.TypeArguments
                        .Select(ResolveTypeName)
                        .Select(t => $"{t.QualifiedName}{(t.IsNullable ? "?" : "")}"));

                    name += $"<{typeArguments}>";
                }

                return new MetadataTypeName(name, @namespace, isNullable, isAlias);
            }

            return new MetadataTypeName(typeSymbol.Name, GetFullNamespace(typeSymbol), false, false);
        }

        public static string ResolveTypeToString(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is INamedTypeSymbol namedType)
            {
                return ResolveTypeName(typeSymbol).QualifiedName;
            }
            else if (typeSymbol.TypeKind == TypeKind.Array)
            {
                IArrayTypeSymbol arrayType = (IArrayTypeSymbol)typeSymbol;
                return $"{ResolveTypeToString(arrayType.ElementType)}[{new string(',', arrayType.Rank - 1)}]";
            }
            else
            {
                // Fallback for unknown types
                return typeSymbol.ToDisplayString();
            }
        }


        public static string GetFullNamespace(ITypeSymbol typeSymbol)
        {
            Stack<string> namespaces = new Stack<string>();
            INamespaceSymbol ns = typeSymbol.ContainingNamespace;

            while (ns != null && !ns.IsGlobalNamespace)
            {
                namespaces.Push(ns.Name);
                ns = ns.ContainingNamespace;
            }

            return string.Join(".", namespaces);
        }
    }
}
