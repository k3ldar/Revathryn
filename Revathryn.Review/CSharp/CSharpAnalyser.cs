using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using Revathryn.Contracts;
using Revathryn.Engine;

using static Revathryn.Contracts.Consts;

namespace Revathryn.Review.CSharp
{
    public sealed class CSharpAnalyser : IAnalyser
    {
        private readonly string _outputDirectory;
        private const string AnalyserType = "C# Analyser";
        private const string OutputDirectoryRequiredMessage = "An output directory is required.";
        private static readonly string[] _filePatterns = [CSharpFilePattern];
        private const int MinimumInterfaceNameLength = 2;

        private static readonly HashSet<string> _systemTypeNames =
        [
            with(StringComparer.Ordinal),
            TypeVoid, TypeBool, TypeByte, TypeSByte, TypeShort, TypeUshort, TypeInt, TypeUint, TypeLong, TypeULong,
            TypeFloat, TypeDouble, TypeDecimal, TypeChar, TypeString, TypeObject, TypeDynamic,
            TypeNetString, TypeNetBoolean, TypeNetByte, TypeNetSByte, TypeNetInt16, TypeNetUInt16, TypeNetInt32, TypeNetUInt32,
            TypeNetInt64, TypeNetUInt64, TypeNetSingle, TypeNetDouble, TypeNetDecimal, TypeNetChar, TypeNetObject,
            TypeTask, TypeValueTask, TypeAction, TypeFunc, TypePredicate, TypeEventHandler,
            TypeList, TypeDictionary, TypeHashSet, TypeIEnumerable, TypeIList, TypeICollection,
            TypeIReadOnlyList, TypeIReadOnlyCollection, TypeIReadOnlyDictionary, TypeIDictionary,
            TypeIEnumerator, TypeArray, TypeSpan, TypeReadOnlySpan, TypeMemory, TypeReadOnlyMemory,
            TypeNullable, TypeType, TypeException, TypeArgumentException, TypeArgumentNullException,
            TypeNotImplementedException, TypeInvalidOperationException, TypeCancellationToken,
            TypeDateTime, TypeDateTimeOffset, TypeTimeSpan, TypeGuid, TypeUri, TypeEnum,
            TypeVar, TypeNameOf, TypeTypeOf, TypeSizeOf, TypeDefault, TypeNull, TypeValueTrue, TypeValueFalse
        ];

        private static readonly JsonSerializerOptions options = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Initialises a new instance of the <see cref="CSharpAnalyser"/> class.
        /// </summary>
        /// <param name="outputDirectory">The directory under which analysis artefacts are written.</param>
        public CSharpAnalyser(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException(OutputDirectoryRequiredMessage, nameof(outputDirectory));

            _outputDirectory = outputDirectory;
        }

        public string Name => AnalyserType;

        public IReadOnlyList<string> FilePatterns => _filePatterns;

        public string Language => LanguageType;

        public string LanguageVersion => LanguageVersionNumber;

        public int AnalyserVersion => 1;

        public void Analyse(string repositoryName, string filePath, IManifestManager manifestManager)
        {
            if (!manifestManager.RequiresAnalysis(repositoryName, filePath, AnalyserVersion))
                return;

            string source = File.ReadAllText(filePath);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(source, path: filePath);
            CompilationUnitSyntax root = (CompilationUnitSyntax)tree.GetRoot();

            List<string> usingDirectives = ExtractUsingDirectives(root);
            List<CodeSymbol> symbols = ExtractSymbols(root, filePath, usingDirectives);

            if (symbols.Count == 0)
                return;

            string repositoryDirectory = Path.Combine(_outputDirectory, RepositoryFileNaming.GetSafeName(repositoryName));
            Directory.CreateDirectory(repositoryDirectory);

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string namespaceName = symbols[0].Namespace;
            string outputName = string.IsNullOrEmpty(namespaceName)
                ? $"{fileName}.{JsonFileExtension}"
                : $"{namespaceName}.{fileName}.{JsonFileExtension}";

            string outputPath = Path.Combine(repositoryDirectory, outputName);

            string json = JsonSerializer.Serialize(symbols, options);
            File.WriteAllText(outputPath, json);

            manifestManager.RecordAnalysis(repositoryName, new ManifestEntry
            {
                SourcePath = filePath,
                Language = Language,
                LanguageVersion = LanguageVersion,
                AnalyserVersion = AnalyserVersion,
                OutputFile = outputPath,
                SourceHash = ManifestManager.ComputeHash(filePath),
                SourceLastWriteUtc = File.GetLastWriteTimeUtc(filePath),
                AnalysedUtc = DateTime.UtcNow
            });
        }

        private static List<string> ExtractUsingDirectives(CompilationUnitSyntax root)
        {
            return [.. root.Usings
                .Select(u => u.Name?.ToString() ?? string.Empty)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)];
        }

        private static List<CodeSymbol> ExtractSymbols(
            CompilationUnitSyntax root,
            string filePath,
            List<string> usingDirectives)
        {
            List<CodeSymbol> symbols = [];

            foreach (MemberDeclarationSyntax member in root.Members)
                CollectFromMember(member, string.Empty, null, filePath, usingDirectives, symbols);

            return symbols;
        }

        private static void CollectFromMember(
            MemberDeclarationSyntax member,
            string namespaceName,
            string outerTypeName,
            string filePath,
            List<string> usingDirectives,
            List<CodeSymbol> symbols)
        {
            if (member is BaseNamespaceDeclarationSyntax ns)
            {
                string nsName = ns.Name.ToString();
                foreach (MemberDeclarationSyntax child in ns.Members)
                    CollectFromMember(child, nsName, null, filePath, usingDirectives, symbols);
                return;
            }

            if (member is DelegateDeclarationSyntax delegateDecl)
            {
                symbols.Add(BuildDelegateSymbol(delegateDecl, namespaceName, outerTypeName, filePath, usingDirectives));
                return;
            }

            if (member is BaseTypeDeclarationSyntax typeDecl)
            {
                CodeSymbol symbol = BuildTypeSymbol(typeDecl, namespaceName, outerTypeName, filePath, usingDirectives);
                symbols.Add(symbol);

                if (typeDecl is TypeDeclarationSyntax typeWithMembers)
                {
                    foreach (MemberDeclarationSyntax nested in typeWithMembers.Members)
                    {
                        if (nested is BaseTypeDeclarationSyntax)
                            CollectFromMember(nested, namespaceName, symbol.Name, filePath, usingDirectives, symbols);
                    }
                }
            }
        }

        private static CodeSymbol BuildTypeSymbol(
            BaseTypeDeclarationSyntax typeDecl,
            string namespaceName,
            string outerTypeName,
            string filePath,
            List<string> usingDirectives)
        {
            string simpleName = typeDecl.Identifier.Text;
            string qualifiedName = outerTypeName != null ? $"{outerTypeName}.{simpleName}" : simpleName;
            string fqn = namespaceName.Length > 0 ? $"{namespaceName}.{qualifiedName}" : qualifiedName;

            FileLinePositionSpan span = typeDecl.GetLocation().GetLineSpan();

            CodeSymbol symbol = new()
            {
                Name = qualifiedName,
                FullyQualifiedName = fqn,
                Kind = ResolveKind(typeDecl),
                FilePath = filePath,
                LineNumber = span.StartLinePosition.Line + 1,
                EndLineNumber = span.EndLinePosition.Line + 1,
                Namespace = namespaceName,
                Accessibility = ResolveAccessibility(typeDecl.Modifiers),
                IsStatic = HasModifier(typeDecl.Modifiers, SyntaxKind.StaticKeyword),
                IsAbstract = HasModifier(typeDecl.Modifiers, SyntaxKind.AbstractKeyword),
                IsSealed = HasModifier(typeDecl.Modifiers, SyntaxKind.SealedKeyword),
                UsingDirectives = usingDirectives
            };

            // Base types and implemented interfaces
            if (typeDecl.BaseList != null)
            {
                foreach (BaseTypeSyntax baseType in typeDecl.BaseList.Types)
                {
                    string typeName = baseType.Type.ToString();

                    if (IsInterfaceName(typeName))
                        symbol.ImplementedInterfaces.Add(typeName);
                    else
                        symbol.BaseTypes.Add(typeName);
                }
            }

            IEnumerable<MemberDeclarationSyntax> directMembers = typeDecl is TypeDeclarationSyntax td
                ? td.Members
                : Enumerable.Empty<MemberDeclarationSyntax>();

            foreach (MemberDeclarationSyntax member in directMembers)
            {
                switch (member)
                {
                    case ConstructorDeclarationSyntax ctor:
                        symbol.Constructors.Add(BuildConstructorSymbol(ctor));
                        break;

                    case DestructorDeclarationSyntax dtor:
                        symbol.Destructors.Add(BuildDestructorSymbol(dtor));
                        break;

                    case MethodDeclarationSyntax method:
                        symbol.Methods.Add(BuildMethodSymbol(method));
                        break;

                    case IndexerDeclarationSyntax indexer:
                        symbol.Indexers.Add(BuildIndexerSymbol(indexer));
                        break;

                    case OperatorDeclarationSyntax op:
                        symbol.Operators.Add(BuildOperatorSymbol(op));
                        break;

                    case ConversionOperatorDeclarationSyntax conv:
                        symbol.Operators.Add(BuildConversionOperatorSymbol(conv));
                        break;

                    case PropertyDeclarationSyntax prop:
                        symbol.Properties.Add(new PropertySymbol
                        {
                            Name = prop.Identifier.Text,
                            Type = prop.Type.ToString(),
                            Accessibility = ResolveAccessibility(prop.Modifiers),
                            IsStatic = HasModifier(prop.Modifiers, SyntaxKind.StaticKeyword)
                        });
                        break;

                    case FieldDeclarationSyntax field:
                        foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                            symbol.Fields.Add(new FieldSymbol
                            {
                                Name = variable.Identifier.Text,
                                Type = field.Declaration.Type.ToString(),
                                Accessibility = ResolveAccessibility(field.Modifiers),
                                IsStatic = HasModifier(field.Modifiers, SyntaxKind.StaticKeyword),
                                IsReadOnly = HasModifier(field.Modifiers, SyntaxKind.ReadOnlyKeyword)
                            });
                        break;
                }
            }

            // Enum members
            if (typeDecl is EnumDeclarationSyntax enumDecl)
            {
                foreach (EnumMemberDeclarationSyntax enumMember in enumDecl.Members)
                    symbol.EnumMembers.Add(new EnumMemberSymbol
                    {
                        Name = enumMember.Identifier.Text,
                        Value = enumMember.EqualsValue?.Value.ToString()
                    });
            }

            symbol.Dependencies = CollectDependencies(symbol);

            return symbol;
        }

        private static MethodSymbol BuildMethodSymbol(MethodDeclarationSyntax method)
        {
            FileLinePositionSpan span = method.GetLocation().GetLineSpan();

            MethodSymbol ms = new()
            {
                Name = method.Identifier.Text,
                ReturnType = method.ReturnType.ToString(),
                Accessibility = ResolveAccessibility(method.Modifiers),
                IsAsync = HasModifier(method.Modifiers, SyntaxKind.AsyncKeyword),
                IsStatic = HasModifier(method.Modifiers, SyntaxKind.StaticKeyword),
                IsAbstract = HasModifier(method.Modifiers, SyntaxKind.AbstractKeyword),
                IsVirtual = HasModifier(method.Modifiers, SyntaxKind.VirtualKeyword),
                IsOverride = HasModifier(method.Modifiers, SyntaxKind.OverrideKeyword),
                IsSealed = HasModifier(method.Modifiers, SyntaxKind.SealedKeyword),
                LineNumber = span.StartLinePosition.Line + 1,
                EndLineNumber = span.EndLinePosition.Line + 1
            };

            foreach (ParameterSyntax param in method.ParameterList.Parameters)
                ms.Parameters.Add(new ParameterSymbol
                {
                    Name = param.Identifier.Text,
                    Type = param.Type?.ToString() ?? string.Empty
                });

            SyntaxNode body = (SyntaxNode)method.Body ?? method.ExpressionBody;

            if (body != null)
            {
                ms.Invocations = ExtractInvocations(body);
                ms.ReferencedTypes = ExtractReferencedTypes(body, ms.Invocations);
            }

            return ms;
        }

        private static ConstructorSymbol BuildConstructorSymbol(ConstructorDeclarationSyntax ctor)
        {
            FileLinePositionSpan span = ctor.GetLocation().GetLineSpan();

            ConstructorSymbol cs = new()
            {
                Accessibility = ResolveAccessibility(ctor.Modifiers),
                IsStatic = HasModifier(ctor.Modifiers, SyntaxKind.StaticKeyword),
                LineNumber = span.StartLinePosition.Line + 1,
                EndLineNumber = span.EndLinePosition.Line + 1
            };

            foreach (ParameterSyntax param in ctor.ParameterList.Parameters)
                cs.Parameters.Add(new ParameterSymbol
                {
                    Name = param.Identifier.Text,
                    Type = param.Type?.ToString() ?? string.Empty
                });

            SyntaxNode body = (SyntaxNode)ctor.Body ?? ctor.ExpressionBody;

            if (body != null)
            {
                cs.Invocations = ExtractInvocations(body);
                cs.ReferencedTypes = ExtractReferencedTypes(body, cs.Invocations);
            }

            return cs;
        }

        private static DestructorSymbol BuildDestructorSymbol(DestructorDeclarationSyntax dtor)
        {
            FileLinePositionSpan span = dtor.GetLocation().GetLineSpan();
            DestructorSymbol ds = new()
            {
                LineNumber = span.StartLinePosition.Line + 1,
                EndLineNumber = span.EndLinePosition.Line + 1
            };

            SyntaxNode body = (SyntaxNode)dtor.Body ?? dtor.ExpressionBody;

            if (body != null)
            {
                ds.Invocations = ExtractInvocations(body);
                ds.ReferencedTypes = ExtractReferencedTypes(body, ds.Invocations);
            }

            return ds;
        }

        private static IndexerSymbol BuildIndexerSymbol(IndexerDeclarationSyntax indexer)
        {
            FileLinePositionSpan span = indexer.GetLocation().GetLineSpan();
            IndexerSymbol ixs = new()
            {
                Type = indexer.Type.ToString(),
                Accessibility = ResolveAccessibility(indexer.Modifiers),
                HasGetter = indexer.AccessorList?.Accessors
                    .Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration)) ?? indexer.ExpressionBody != null,
                HasSetter = indexer.AccessorList?.Accessors
                    .Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration) || a.IsKind(SyntaxKind.InitAccessorDeclaration)) ?? false,
                LineNumber = span.StartLinePosition.Line + 1,
                EndLineNumber = span.EndLinePosition.Line + 1
            };

            foreach (ParameterSyntax param in indexer.ParameterList.Parameters)
                ixs.Parameters.Add(new ParameterSymbol
                {
                    Name = param.Identifier.Text,
                    Type = param.Type?.ToString() ?? string.Empty
                });

            return ixs;
        }

        private static OperatorSymbol BuildOperatorSymbol(OperatorDeclarationSyntax op)
        {
            FileLinePositionSpan span = op.GetLocation().GetLineSpan();
            OperatorSymbol os = new()
            {
                OperatorToken = op.OperatorToken.Text,
                ReturnType = op.ReturnType.ToString(),
                Accessibility = ResolveAccessibility(op.Modifiers),
                IsStatic = HasModifier(op.Modifiers, SyntaxKind.StaticKeyword),
                LineNumber = span.StartLinePosition.Line + 1,
                EndLineNumber = span.EndLinePosition.Line + 1
            };

            foreach (ParameterSyntax param in op.ParameterList.Parameters)
                os.Parameters.Add(new ParameterSymbol
                {
                    Name = param.Identifier.Text,
                    Type = param.Type?.ToString() ?? string.Empty
                });

            SyntaxNode body = (SyntaxNode)op.Body ?? op.ExpressionBody;

            if (body != null)
            {
                os.Invocations = ExtractInvocations(body);
                os.ReferencedTypes = ExtractReferencedTypes(body, os.Invocations);
            }

            return os;
        }

        private static OperatorSymbol BuildConversionOperatorSymbol(ConversionOperatorDeclarationSyntax conv)
        {
            FileLinePositionSpan span = conv.GetLocation().GetLineSpan();
            string keyword = conv.ImplicitOrExplicitKeyword.Text; // "implicit" or "explicit"
            OperatorSymbol os = new()
            {
                OperatorToken = $"{keyword} operator {conv.Type}",
                ReturnType = conv.Type.ToString(),
                Accessibility = ResolveAccessibility(conv.Modifiers),
                IsStatic = HasModifier(conv.Modifiers, SyntaxKind.StaticKeyword),
                LineNumber = span.StartLinePosition.Line + 1,
                EndLineNumber = span.EndLinePosition.Line + 1
            };

            foreach (ParameterSyntax param in conv.ParameterList.Parameters)
                os.Parameters.Add(new ParameterSymbol
                {
                    Name = param.Identifier.Text,
                    Type = param.Type?.ToString() ?? string.Empty
                });

            SyntaxNode body = (SyntaxNode)conv.Body ?? conv.ExpressionBody;

            if (body != null)
            {
                os.Invocations = ExtractInvocations(body);
                os.ReferencedTypes = ExtractReferencedTypes(body, os.Invocations);
            }

            return os;
        }

        private static CodeSymbol BuildDelegateSymbol(
            DelegateDeclarationSyntax delegateDecl,
            string namespaceName,
            string outerTypeName,
            string filePath,
            List<string> usingDirectives)
        {
            string simpleName = delegateDecl.Identifier.Text;
            string qualifiedName = outerTypeName != null ? $"{outerTypeName}.{simpleName}" : simpleName;
            string fqn = namespaceName.Length > 0 ? $"{namespaceName}.{qualifiedName}" : qualifiedName;

            FileLinePositionSpan span = delegateDecl.GetLocation().GetLineSpan();

            CodeSymbol symbol = new()
            {
                Name = qualifiedName,
                FullyQualifiedName = fqn,
                Kind = SymbolKind.Delegate,
                FilePath = filePath,
                LineNumber = span.StartLinePosition.Line + 1,
                EndLineNumber = span.EndLinePosition.Line + 1,
                Namespace = namespaceName,
                Accessibility = ResolveAccessibility(delegateDecl.Modifiers),
                DelegateReturnType = delegateDecl.ReturnType.ToString(),
                UsingDirectives = usingDirectives
            };

            foreach (ParameterSyntax param in delegateDecl.ParameterList.Parameters)
                symbol.DelegateParameters.Add(new ParameterSymbol
                {
                    Name = param.Identifier.Text,
                    Type = param.Type?.ToString() ?? string.Empty
                });

            symbol.Dependencies = CollectDependencies(symbol);

            return symbol;
        }

        private static List<InvocationSymbol> ExtractInvocations(SyntaxNode body)
        {
            List<InvocationSymbol> invocations = [];

            foreach (InvocationExpressionSyntax invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                LinePosition pos = invocation.GetLocation().GetLineSpan().StartLinePosition;
                string target = String.Empty;
                string methodName;

                switch (invocation.Expression)
                {
                    case MemberAccessExpressionSyntax memberAccess:
                        target = memberAccess.Expression.ToString();
                        methodName = memberAccess.Name.Identifier.Text;
                        break;

                    case IdentifierNameSyntax identifier:
                        methodName = identifier.Identifier.Text;
                        break;

                    case MemberBindingExpressionSyntax memberBinding:
                        // Null-conditional: obj?.Method()
                        methodName = memberBinding.Name.Identifier.Text;
                        break;

                    default:
                        methodName = invocation.Expression.ToString();
                        break;
                }

                invocations.Add(new InvocationSymbol
                {
                    TargetExpression = target,
                    MethodName = methodName,
                    ArgumentCount = invocation.ArgumentList.Arguments.Count,
                    LineNumber = pos.Line + 1
                });
            }

            return invocations;
        }

        private static List<string> ExtractReferencedTypes(SyntaxNode body, List<InvocationSymbol> invocations)
        {
            HashSet<string> types = [with(StringComparer.Ordinal)];

            // Identifier names that look like user types (UpperCase, not system)
            foreach (IdentifierNameSyntax id in body.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (IsUserTypeName(id.Identifier.Text))
                    types.Add(id.Identifier.Text);
            }

            // Also capture the leading segment of invocation targets, e.g. "_orderService" excluded
            // but "OrderService" from "OrderService.Create()" included
            foreach (InvocationSymbol inv in invocations)
            {
                string root = inv.TargetExpression.Split(CharFullStop)[0].Trim();

                if (IsUserTypeName(root))
                    types.Add(root);
            }

            return [.. types.OrderBy(t => t)];
        }

        private static List<string> CollectDependencies(CodeSymbol symbol)
        {
            HashSet<string> deps = [with(StringComparer.Ordinal)];

            foreach (PropertySymbol p in symbol.Properties)
                AddIfUserType(deps, p.Type);

            foreach (FieldSymbol f in symbol.Fields)
                AddIfUserType(deps, f.Type);

            foreach (ConstructorSymbol ctor in symbol.Constructors)
            {
                foreach (ParameterSymbol p in ctor.Parameters)
                    AddIfUserType(deps, p.Type);
                foreach (string rt in ctor.ReferencedTypes)
                    AddIfUserType(deps, rt);
            }

            foreach (MethodSymbol m in symbol.Methods)
            {
                AddIfUserType(deps, m.ReturnType);
                foreach (ParameterSymbol p in m.Parameters)
                    AddIfUserType(deps, p.Type);
                foreach (string rt in m.ReferencedTypes)
                    AddIfUserType(deps, rt);
            }

            foreach (DestructorSymbol dtor in symbol.Destructors)
            {
                foreach (string rt in dtor.ReferencedTypes)
                    AddIfUserType(deps, rt);
            }

            foreach (IndexerSymbol ix in symbol.Indexers)
            {
                AddIfUserType(deps, ix.Type);
                foreach (ParameterSymbol p in ix.Parameters)
                    AddIfUserType(deps, p.Type);
            }

            foreach (OperatorSymbol op in symbol.Operators)
            {
                AddIfUserType(deps, op.ReturnType);
                foreach (ParameterSymbol p in op.Parameters)
                    AddIfUserType(deps, p.Type);
                foreach (string rt in op.ReferencedTypes)
                    AddIfUserType(deps, rt);
            }

            foreach (ParameterSymbol p in symbol.DelegateParameters)
                AddIfUserType(deps, p.Type);

            AddIfUserType(deps, symbol.DelegateReturnType);

            foreach (string bt in symbol.BaseTypes)
                AddIfUserType(deps, bt);

            foreach (string iface in symbol.ImplementedInterfaces)
                AddIfUserType(deps, iface);

            deps.Remove(symbol.Name);
            deps.Remove(symbol.FullyQualifiedName);

            return [.. deps.OrderBy(d => d)];
        }

        private static SymbolKind ResolveKind(BaseTypeDeclarationSyntax typeDecl) => typeDecl switch
        {
            InterfaceDeclarationSyntax => SymbolKind.Interface,
            StructDeclarationSyntax => SymbolKind.Struct,
            EnumDeclarationSyntax => SymbolKind.Enum,
            RecordDeclarationSyntax r when r.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) => SymbolKind.RecordStruct,
            RecordDeclarationSyntax => SymbolKind.Record,
            _ => SymbolKind.Class
        };

        private static string ResolveAccessibility(SyntaxTokenList modifiers)
        {
            if (HasModifier(modifiers, SyntaxKind.PublicKeyword))
                return ModifierPublic;

            if (HasModifier(modifiers, SyntaxKind.ProtectedKeyword) && HasModifier(modifiers, SyntaxKind.InternalKeyword))
                return ModifierProtectedInternal;

            if (HasModifier(modifiers, SyntaxKind.ProtectedKeyword))
                return ModifierProtected;

            if (HasModifier(modifiers, SyntaxKind.InternalKeyword))
                return ModifierInternal;

            return ModifierPrivate;
        }

        private static bool HasModifier(SyntaxTokenList modifiers, SyntaxKind kind) =>
            modifiers.Any(m => m.IsKind(kind));

        private static bool IsInterfaceName(string name)
        {
            string baseName = name.Contains(CharLessThan) ? name[..name.IndexOf(CharLessThan)] : name;
            return baseName.Length >= MinimumInterfaceNameLength && baseName[0] == CharI && char.IsUpper(baseName[1]);
        }

        private static bool IsUserTypeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (!char.IsUpper(name[0]))
                return false;

            return !_systemTypeNames.Contains(name);
        }

        private static void AddIfUserType(HashSet<string> set, string typeExpression)
        {
            if (string.IsNullOrWhiteSpace(typeExpression)) return;

            string cleaned = typeExpression
                .Replace(StringQuestionMark, string.Empty)
                .Replace(StringArrayIdentifier, string.Empty)
                .Trim();

            if (cleaned.Contains(CharLessThan))
            {
                string outer = cleaned[..cleaned.IndexOf(CharLessThan)];
                string inner = cleaned[(cleaned.IndexOf(CharLessThan) + 1)..cleaned.LastIndexOf(CharGreaterThan)];

                if (IsUserTypeName(outer))
                    set.Add(outer);

                foreach (string part in inner.Split(CharComma))
                {
                    string p = part.Trim().Replace(StringQuestionMark, string.Empty);

                    if (IsUserTypeName(p))
                        set.Add(p);
                }

                return;
            }

            if (IsUserTypeName(cleaned))
                set.Add(cleaned);
        }
    }
}