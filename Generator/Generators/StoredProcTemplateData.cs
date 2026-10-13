using System.Collections.Generic;

namespace Efrpg.Generators
{
    public class StoredProcTemplateData
    {
        public bool HasNoReturnModels { get; set; } // sp.ReturnModels.Count == 0
        public bool HasReturnModels { get; } // sp.ReturnModels.Count > 0
        public bool SingleReturnModel { get; } // sp.ReturnModels.Count == 1
        public bool MultipleReturnModels { get; } // sp.ReturnModels.Count > 1
        public string ReturnType { get; } // sp.WriteStoredProcReturnType()
        public string ReturnModelName { get; } // sp.WriteStoredProcReturnModelName()
        public string FunctionName { get; } // sp.WriteStoredProcFunctionName()
        public string WriteStoredProcFunctionParamsFalseFalse { get; } // WriteStoredProcFunctionParams(false, false, false)
        public string WriteStoredProcFunctionParamsFalseTrue { get; } // WriteStoredProcFunctionParams(false, true, false)
        public string WriteStoredProcFunctionParamsTrueFalse { get; } // WriteStoredProcFunctionParams(true, false, false)
        public string WriteStoredProcFunctionParamsTrueTrue { get; } // WriteStoredProcFunctionParams(true, true, false)
        public string WriteStoredProcFunctionParamsFalseFalseToken { get; } // WriteStoredProcFunctionParams(false, false, true)
        public string WriteStoredProcFunctionParamsFalseTrueToken { get; } // WriteStoredProcFunctionParams(false, true, true)
        public string WriteStoredProcFunctionParamsTrueFalseToken { get; } // WriteStoredProcFunctionParams(true, false, true)
        public string WriteStoredProcFunctionParamsTrueTrueToken { get; } // WriteStoredProcFunctionParams(true, true, true)
        public bool AsyncFunctionCannotBeCreated { get; } // !sp.StoredProcCanExecuteAsync()
        public string WriteStoredProcFunctionOverloadCall { get; } // sp.WriteStoredProcFunctionOverloadCall()
        public string WriteStoredProcFunctionSetSqlParametersFalse { get; } // WriteStoredProcFunctionSetSqlParameters(false)
        public string WriteStoredProcFunctionSetSqlParametersTrue { get; } // WriteStoredProcFunctionSetSqlParameters(true)
        public string Exec { get; }
        public string AsyncExec { get; }
        public string WriteStoredProcReturnModelName { get; } // sp.WriteStoredProcReturnModelName()
        public string WriteStoredProcFunctionSqlParameterAnonymousArrayTrue { get; } // sp.WriteStoredProcFunctionSqlParameterAnonymousArray(true, true, false)
        public string WriteStoredProcFunctionSqlParameterAnonymousArrayFalse { get; } // sp.WriteStoredProcFunctionSqlParameterAnonymousArray(false, true, false)
        public string WriteStoredProcFunctionSqlParameterAnonymousArrayTrueToken { get; } // sp.WriteStoredProcFunctionSqlParameterAnonymousArray(true, true, true)
        public string WriteStoredProcFunctionSqlParameterAnonymousArrayFalseToken { get; } // sp.WriteStoredProcFunctionSqlParameterAnonymousArray(false, true, true)
        public string WriteStoredProcFunctionDeclareSqlParameterTrue { get; } // sp.WriteStoredProcFunctionDeclareSqlParameter(true)
        public string WriteStoredProcFunctionDeclareSqlParameterFalse { get; } // sp.WriteStoredProcFunctionDeclareSqlParameter(false)
        public List<string> Parameters { get; }
        public int ReturnModelsCount { get; }
        public string ExecWithNoReturnModel { get; }
        public List<ResultSetResultReaderCommand> ReturnModelResultSetReaderCommand { get; }
        public bool CreateDbSetForReturnModel { get; set; }
        public List<string> ColumnMappings { get; set; } = new List<string>();
        public bool HasColumnMappings => ColumnMappings != null && ColumnMappings.Count > 0;
        public bool HasError { get; }
        public string Error { get; }
        public bool HasProcResult { get; set; } // StoredProcedure.ReturnsProcResult()
        public bool ExecutesAsCommand { get; set; } // sp.ExecutesAsStoredProcedureCommand()
        public string CommandText { get; set; } // sp.WriteStoredProcFunctionQualifiedName()
        public string CommandParameters { get; set; } // new[] { aParam, bParam }

        public StoredProcTemplateData(
            bool hasNoReturnModels,
            bool hasReturnModels,
            bool singleReturnModel,
            bool multipleReturnModels,
            string returnType,
            string returnModelName,
            string functionName,
            string writeStoredProcFunctionParamsFalseFalse,
            string writeStoredProcFunctionParamsFalseTrue,
            string writeStoredProcFunctionParamsTrueFalse,
            string writeStoredProcFunctionParamsTrueTrue,
            string writeStoredProcFunctionParamsFalseFalseToken,
            string writeStoredProcFunctionParamsFalseTrueToken,
            string writeStoredProcFunctionParamsTrueFalseToken,
            string writeStoredProcFunctionParamsTrueTrueToken,
            bool asyncFunctionCannotBeCreated,
            string writeStoredProcFunctionOverloadCall,
            string writeStoredProcFunctionSetSqlParametersFalse,
            string writeStoredProcFunctionSetSqlParametersTrue,
            string exec,
            string asyncExec,
            string writeStoredProcReturnModelName,
            string writeStoredProcFunctionSqlParameterAnonymousArrayTrue,
            string writeStoredProcFunctionSqlParameterAnonymousArrayFalse,
            string writeStoredProcFunctionSqlParameterAnonymousArrayTrueToken,
            string writeStoredProcFunctionSqlParameterAnonymousArrayFalseToken,
            string writeStoredProcFunctionDeclareSqlParameterTrue,
            string writeStoredProcFunctionDeclareSqlParameterFalse,
            List<string> parameters,
            int returnModelsCount,
            string execWithNoReturnModel,
            bool hasError,
            string error)
        {
            HasNoReturnModels = hasNoReturnModels;
            HasReturnModels = hasReturnModels;
            SingleReturnModel = singleReturnModel;
            MultipleReturnModels = multipleReturnModels;
            ReturnType = returnType;
            ReturnModelName = returnModelName;
            FunctionName = functionName;
            WriteStoredProcFunctionParamsFalseFalse = writeStoredProcFunctionParamsFalseFalse;
            WriteStoredProcFunctionParamsFalseTrue = writeStoredProcFunctionParamsFalseTrue;
            WriteStoredProcFunctionParamsTrueFalse = writeStoredProcFunctionParamsTrueFalse;
            WriteStoredProcFunctionParamsTrueTrue = writeStoredProcFunctionParamsTrueTrue;
            WriteStoredProcFunctionParamsFalseFalseToken = writeStoredProcFunctionParamsFalseFalseToken;
            WriteStoredProcFunctionParamsFalseTrueToken = writeStoredProcFunctionParamsFalseTrueToken;
            WriteStoredProcFunctionParamsTrueFalseToken = writeStoredProcFunctionParamsTrueFalseToken;
            WriteStoredProcFunctionParamsTrueTrueToken = writeStoredProcFunctionParamsTrueTrueToken;
            AsyncFunctionCannotBeCreated = asyncFunctionCannotBeCreated;
            WriteStoredProcFunctionOverloadCall = writeStoredProcFunctionOverloadCall;
            WriteStoredProcFunctionSetSqlParametersFalse = writeStoredProcFunctionSetSqlParametersFalse;
            WriteStoredProcFunctionSetSqlParametersTrue = writeStoredProcFunctionSetSqlParametersTrue;
            Exec = exec;
            AsyncExec = asyncExec;
            WriteStoredProcReturnModelName = writeStoredProcReturnModelName;
            WriteStoredProcFunctionSqlParameterAnonymousArrayTrue = writeStoredProcFunctionSqlParameterAnonymousArrayTrue;
            WriteStoredProcFunctionSqlParameterAnonymousArrayFalse = writeStoredProcFunctionSqlParameterAnonymousArrayFalse;
            WriteStoredProcFunctionSqlParameterAnonymousArrayTrueToken = writeStoredProcFunctionSqlParameterAnonymousArrayTrueToken;
            WriteStoredProcFunctionSqlParameterAnonymousArrayFalseToken = writeStoredProcFunctionSqlParameterAnonymousArrayFalseToken;
            WriteStoredProcFunctionDeclareSqlParameterTrue = writeStoredProcFunctionDeclareSqlParameterTrue;
            WriteStoredProcFunctionDeclareSqlParameterFalse = writeStoredProcFunctionDeclareSqlParameterFalse;
            Parameters = parameters;
            ReturnModelsCount = returnModelsCount;
            ExecWithNoReturnModel = execWithNoReturnModel;
            HasError = hasError;
            Error = error;

            CreateDbSetForReturnModel = true;

            ReturnModelResultSetReaderCommand = new List<ResultSetResultReaderCommand>(returnModelsCount);
            for (var n = 1; n <= returnModelsCount; ++n)
            {
                var lastRecord = n == returnModelsCount;
                ReturnModelResultSetReaderCommand.Add(new ResultSetResultReaderCommand(n, lastRecord ? "Close" : "NextResult", !lastRecord, writeStoredProcReturnModelName));
            }
        }
    }
}