namespace Efrpg.Generators
{
    public class ScalarValuedFunctionsTemplateData
    {
        public string ExecName { get; }
        public string ReturnType { get; }
        public string WriteStoredProcFunctionParamsFalseTrue { get; }
        public string WriteStoredProcFunctionParamsFalseFalse { get; }
        public string Name { get; }
        public string Schema { get; }
        public string DbFunctionArguments { get; set; } // svf.WriteDbFunctionArguments()

        public ScalarValuedFunctionsTemplateData(
            string execName,
            string returnType,
            string writeStoredProcFunctionParamsFalseTrue,
            string writeStoredProcFunctionParamsFalseFalse,
            string name,
            string schema)
        {
            ExecName = execName;
            ReturnType = returnType;
            WriteStoredProcFunctionParamsFalseTrue = writeStoredProcFunctionParamsFalseTrue;
            WriteStoredProcFunctionParamsFalseFalse = writeStoredProcFunctionParamsFalseFalse;
            Name = name;
            Schema = schema;
        }
    }
}