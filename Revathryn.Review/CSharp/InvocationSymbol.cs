namespace Revathryn.Review.CSharp
{
    public class InvocationSymbol
    {
        /// <summary>
        /// The receiver expression as written in source, e.g. "_service", "MyClass", "this".
        /// Empty string for simple local method calls with no explicit target.
        /// </summary>
        public string TargetExpression { get; set; } = string.Empty;

        /// <summary>The name of the method being invoked.</summary>
        public string MethodName { get; set; } = string.Empty;

        /// <summary>Number of arguments passed at the call site.</summary>
        public int ArgumentCount { get; set; }

        /// <summary>Source line of the invocation.</summary>
        public int LineNumber { get; set; }
    }
}
