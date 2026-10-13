using System.Collections.Generic;

namespace Efrpg.Generators
{
    public class TableValuedFunctionsTemplateData
    {
        public bool SingleReturnModel { get; }
        public string SingleReturnColumnName { get; }
        public string ExecName { get; }
        public string ReturnClassName { get; }
        public string PluralReturnClassName { get; }
        public string WriteStoredProcFunctionParamsFalseTrue { get; }
        public string WriteStoredProcFunctionParamsFalseFalse { get; }
        public string Name { get; }
        public string Schema { get; }
        public string WriteTableValuedFunctionDeclareSqlParameter { get; }
        public string WriteTableValuedFunctionSqlParameterAnonymousArray { get; }
        public string WriteStoredProcFunctionSqlAtParams { get; }
        public string FromSql { get; }
        public string QueryString { get; }
        public string ModelBuilderCommand { get; }
        public string ModelBuilderPostCommand { get; }
        public bool IncludeModelBuilder { get; }
        public string QualifiedName { get; set; } // tvf.WriteStoredProcFunctionQualifiedName()
        public List<string> ColumnMappings { get; set; } = new List<string>();
        public bool HasColumnMappings => ColumnMappings != null && ColumnMappings.Count > 0;

        public TableValuedFunctionsTemplateData(bool singleReturnModel,
            string singleReturnColumnName,
            string execName,
            string returnClassName,
            string writeStoredProcFunctionParamsFalseTrue,
            string writeStoredProcFunctionParamsFalseFalse,
            string name,
            string schema,
            string writeTableValuedFunctionDeclareSqlParameter,
            string writeTableValuedFunctionSqlParameterAnonymousArray,
            string writeStoredProcFunctionSqlAtParams,
            string fromSql,
            string queryString,
            string modelBuilderCommand,
            string modelBuilderPostCommand,
            bool includeModelBuilder)
        {
            SingleReturnModel = singleReturnModel && !string.IsNullOrEmpty(singleReturnColumnName);
            SingleReturnColumnName = singleReturnColumnName;
            ExecName = execName;
            ReturnClassName = returnClassName;
            PluralReturnClassName = Inflector.MakePlural(returnClassName);
            WriteStoredProcFunctionParamsFalseTrue = writeStoredProcFunctionParamsFalseTrue;
            WriteStoredProcFunctionParamsFalseFalse = writeStoredProcFunctionParamsFalseFalse;
            Name = name;
            Schema = schema;
            WriteTableValuedFunctionDeclareSqlParameter = writeTableValuedFunctionDeclareSqlParameter;
            WriteTableValuedFunctionSqlParameterAnonymousArray = writeTableValuedFunctionSqlParameterAnonymousArray;
            WriteStoredProcFunctionSqlAtParams = writeStoredProcFunctionSqlAtParams;
            FromSql = fromSql;
            QueryString = queryString;
            ModelBuilderCommand = modelBuilderCommand;
            ModelBuilderPostCommand = modelBuilderPostCommand;
            IncludeModelBuilder = includeModelBuilder;
        }
    }
}