using AutoFactories.Types;
using HandlebarsDotNet;
using Ninject.AutoFactories;
using System.Collections.Generic;
using System.Linq;
using AutoFactories.Templating;
using AutoFactories.Models;

namespace AutoFactories.Visitors
{

    internal class FactoryDeclaration
    {
        /// <summary>
        /// Comparer that deduplicates parameters by their qualified type name.
        /// Used when building shared factories to avoid duplicate constructor parameters
        /// when multiple classes have [FromFactory] parameters of the same type.
        /// </summary>
        private class ParameterTypeEqualityComparer : IEqualityComparer<ParameterSyntaxVisitor>
        {
            public bool Equals(ParameterSyntaxVisitor x, ParameterSyntaxVisitor y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;
                
                // Deduplicate by qualified type name only
                // Parameters with the same type should be considered equal
                // even if they have different parameter names
                return x.Type.QualifiedName.Equals(y.Type.QualifiedName);
            }

            public int GetHashCode(ParameterSyntaxVisitor obj)
            {
                return obj?.Type.QualifiedName?.GetHashCode() ?? 0;
            }
        }

        public MetadataTypeName Type { get; }
        public AccessModifier ImplementationAccessModifier { get; }
        public AccessModifier InterfaceAccessModifier { get; }
        public IReadOnlyList<string> Usings { get; }
        public IReadOnlyList<ClassDeclarationVisitor> Classes { get; }
        public IReadOnlyList<ParameterSyntaxVisitor> Parameters { get; }


        private FactoryDeclaration(MetadataTypeName type, IEnumerable<ClassDeclarationVisitor> classes)
        {
            Type = type;
            Classes = classes.ToList();
            Parameters = Classes
                .SelectMany(c => c.Constructors)
                .Where(c => !c.IsPrivate)
                .Where(c => !c.IsStatic)
                .SelectMany(c => c.Parameters)
                .Where(p => p.HasMarkerAttribute)
                .GroupBy(p => p.Type.QualifiedName)  // Group by type's qualified name
                .Select(g => g.First())               // Take first parameter of each type
                .ToList();

            Usings = classes.SelectMany(c => c.Usings)
                .Distinct()
                .OrderBy(s => s.StartsWith("System") ? 0 : 1)
                .ThenBy(s => s)
                .ToList();

            InterfaceAccessModifier = AccessModifier.MostRestrictive(
                Classes
                .Select(c => c.InterfaceAccessModifier)
                .ToArray());

            ImplementationAccessModifier = AccessModifier.MostRestrictive(
                Classes
                .Select(c => c.FactoryAccessModifier)
                .ToArray());
        }

        public static IEnumerable<FactoryDeclaration> Create(IEnumerable<ClassDeclarationVisitor> classes)
        {
            foreach (IGrouping<MetadataTypeName, ClassDeclarationVisitor> grouping in classes.GroupBy(v => v.FactoryType))
            {
                yield return new FactoryDeclaration(grouping.Key, grouping);
            }
        }


        public static FactoryViewModel Map(FactoryDeclaration declaration)
            => new FactoryViewModel()
            {
                Type = declaration.Type,
                Usings = declaration.Usings.ToList(),
                ImplementationAccessModifier = declaration.ImplementationAccessModifier,
                InterfaceAccessModifier = declaration.InterfaceAccessModifier,
                Parameters = declaration.Parameters
                    .Select(ParameterViewModel.Map)
                    .ToList(),
                Methods = declaration.Classes
                    .SelectMany(c => c.Constructors)
                    .Where(c => !c.IsStatic)
                    .Select(FactoryMethodViewModel.Map)
                    .ToList()
            };
    }
}
