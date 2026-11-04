using Revathryn.Contracts.Models;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Review.CSharp
{
    /// <summary>Maps Roslyn-extracted <see cref="CodeSymbol"/> data into index-ready models.</summary>
    internal static class CodeSymbolMapper
    {
        /// <summary>Converts a single extracted symbol into an indexed symbol, including its members.</summary>
        internal static IndexedSymbol ToIndexedSymbol(CodeSymbol symbol)
        {
            IndexedSymbol indexed = new()
            {
                Name = symbol.Name ?? string.Empty,
                FullyQualifiedName = symbol.FullyQualifiedName ?? string.Empty,
                Kind = symbol.Kind.ToString(),
                FilePath = symbol.FilePath ?? string.Empty,
                LineNumber = symbol.LineNumber,
                EndLineNumber = symbol.EndLineNumber,
                Namespace = symbol.Namespace ?? string.Empty,
                Accessibility = symbol.Accessibility ?? string.Empty,
                IsStatic = symbol.IsStatic,
                IsAbstract = symbol.IsAbstract,
                IsSealed = symbol.IsSealed,
                Dependencies = symbol.Dependencies is null ? [] : [.. symbol.Dependencies]
            };

            foreach (MethodSymbol method in symbol.Methods ?? [])
            {
                indexed.Members.Add(new IndexedMember
                {
                    Kind = MemberKindMethod,
                    Name = method.Name ?? string.Empty,
                    Signature = BuildMethodSignature(method),
                    LineNumber = method.LineNumber,
                    EndLineNumber = method.EndLineNumber,
                    Accessibility = method.Accessibility ?? string.Empty,
                    IsStatic = method.IsStatic
                });
            }

            foreach (PropertySymbol property in symbol.Properties ?? [])
            {
                indexed.Members.Add(new IndexedMember
                {
                    Kind = MemberKindProperty,
                    Name = property.Name ?? string.Empty,
                    Signature = BuildTypeAndName(property.Type, property.Name),
                    Accessibility = property.Accessibility ?? string.Empty,
                    IsStatic = property.IsStatic
                });
            }

            foreach (FieldSymbol field in symbol.Fields ?? [])
            {
                indexed.Members.Add(new IndexedMember
                {
                    Kind = MemberKindField,
                    Name = field.Name ?? string.Empty,
                    Signature = BuildTypeAndName(field.Type, field.Name),
                    Accessibility = field.Accessibility ?? string.Empty,
                    IsStatic = field.IsStatic
                });
            }

            foreach (ConstructorSymbol constructor in symbol.Constructors ?? [])
            {
                indexed.Members.Add(new IndexedMember
                {
                    Kind = MemberKindConstructor,
                    Name = symbol.Name ?? string.Empty,
                    Signature = BuildCallableSignature(symbol.Name, constructor.Parameters),
                    LineNumber = constructor.LineNumber,
                    EndLineNumber = constructor.EndLineNumber,
                    Accessibility = constructor.Accessibility ?? string.Empty,
                    IsStatic = constructor.IsStatic
                });
            }

            foreach (DestructorSymbol destructor in symbol.Destructors ?? [])
            {
                indexed.Members.Add(new IndexedMember
                {
                    Kind = MemberKindDestructor,
                    Name = symbol.Name ?? string.Empty,
                    Signature = BuildDestructorSignature(symbol.Name),
                    LineNumber = destructor.LineNumber,
                    EndLineNumber = destructor.EndLineNumber
                });
            }

            foreach (IndexerSymbol indexer in symbol.Indexers ?? [])
            {
                indexed.Members.Add(new IndexedMember
                {
                    Kind = MemberKindIndexer,
                    Name = KeywordThis,
                    Signature = BuildIndexerSignature(indexer),
                    LineNumber = indexer.LineNumber,
                    EndLineNumber = indexer.EndLineNumber,
                    Accessibility = indexer.Accessibility ?? string.Empty
                });
            }

            foreach (OperatorSymbol operatorSymbol in symbol.Operators ?? [])
            {
                indexed.Members.Add(new IndexedMember
                {
                    Kind = MemberKindOperator,
                    Name = operatorSymbol.OperatorToken ?? string.Empty,
                    Signature = BuildOperatorSignature(operatorSymbol),
                    LineNumber = operatorSymbol.LineNumber,
                    EndLineNumber = operatorSymbol.EndLineNumber,
                    Accessibility = operatorSymbol.Accessibility ?? string.Empty,
                    IsStatic = operatorSymbol.IsStatic
                });
            }

            foreach (EnumMemberSymbol enumMember in symbol.EnumMembers ?? [])
            {
                indexed.Members.Add(new IndexedMember
                {
                    Kind = MemberKindEnumMember,
                    Name = enumMember.Name ?? string.Empty,
                    Signature = enumMember.Name ?? string.Empty
                });
            }

            return indexed;
        }

        private static string BuildMethodSignature(MethodSymbol method)
        {
            string returnType = string.IsNullOrWhiteSpace(method.ReturnType) ? string.Empty : method.ReturnType + StringSpace;
            return string.Concat(returnType, method.Name ?? string.Empty, BuildParameterList(method.Parameters));
        }

        private static string BuildTypeAndName(string type, string name)
        {
            return string.Concat(type ?? string.Empty, StringSpace, name ?? string.Empty).Trim();
        }

        private static string BuildCallableSignature(string name, List<ParameterSymbol> parameters)
        {
            return string.Concat(name ?? string.Empty, BuildParameterList(parameters));
        }

        private static string BuildDestructorSignature(string name)
        {
            return string.Concat(CharTilde, name ?? string.Empty, CharLeftParenthesis, CharRightParenthesis);
        }

        private static string BuildIndexerSignature(IndexerSymbol indexer)
        {
            return string.Concat(
                indexer.Type ?? string.Empty,
                StringSpace,
                KeywordThis,
                CharLeftSquareBracket,
                BuildParameters(indexer.Parameters),
                CharRightSquareBracket);
        }

        private static string BuildOperatorSignature(OperatorSymbol operatorSymbol)
        {
            string returnType = string.IsNullOrWhiteSpace(operatorSymbol.ReturnType) ? string.Empty : operatorSymbol.ReturnType + StringSpace;
            return string.Concat(
                OperatorKeyword,
                StringSpace,
                operatorSymbol.OperatorToken ?? string.Empty,
                BuildParameterList(operatorSymbol.Parameters));
        }

        private static string BuildParameterList(List<ParameterSymbol> parameters)
        {
            return string.Concat(CharLeftParenthesis, BuildParameters(parameters), CharRightParenthesis);
        }

        private static string BuildParameters(List<ParameterSymbol> parameters)
        {
            if (parameters is null || parameters.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(ListSeparator, parameters.Select(parameter => string.Concat(parameter.Type ?? string.Empty, StringSpace, parameter.Name ?? string.Empty).Trim()));
        }
    }
}
