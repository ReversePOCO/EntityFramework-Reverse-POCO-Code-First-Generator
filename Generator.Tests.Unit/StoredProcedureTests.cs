using System.Collections.Generic;
using Efrpg;
using NUnit.Framework;
using System.Data;
using System.Linq;
using Generator.Tests.Common;

namespace Generator.Tests.Unit
{
    [TestFixture]
    [Category(Constants.CI)]
    public class StoredProcedureTests
    {
        private StoredProcedure _sut;

        [SetUp]
        public void Init()
        {
            _sut = new StoredProcedure
            {
                Schema = new Schema("dbo"),
                DbName = "name",
                NameHumanCase = "some_sp",
                IsStoredProcedure = true,
                ReturnModels = new List<List<DataColumn>>(),
                Parameters = new List<StoredProcedureParameter>
                {
                    new StoredProcedureParameter
                    {
                        Mode = StoredProcedureParameterMode.In,
                        PropertyType = "DateTime",
                        NameHumanCase = "A",
                        Ordinal = 1
                    },
                    new StoredProcedureParameter
                    {
                        Mode = StoredProcedureParameterMode.InOut,
                        PropertyType = "DateTime",
                        NameHumanCase = "B",
                        Ordinal = 2
                    },
                    new StoredProcedureParameter
                    {
                        Mode = StoredProcedureParameterMode.In,
                        PropertyType = "DateTime",
                        NameHumanCase = "C",
                        Ordinal = 3
                    },
                    new StoredProcedureParameter
                    {
                        Mode = StoredProcedureParameterMode.Out, // Ignored
                        PropertyType = "DateTime",
                        NameHumanCase = "X",
                        Ordinal = 4
                    },
                    new StoredProcedureParameter
                    {
                        Mode = StoredProcedureParameterMode.In,
                        PropertyType = "DateTime",
                        NameHumanCase = "D",
                        Ordinal = 5
                    }
                }
            };
        }

        [Description("Issue #286")]
        [TestCase("JSON_F52E2B61-18A1-11d1-B105-00805F49916B", "JSON_Value")]
        [TestCase("XML_F52E2B61-18A1-11d1-B105-00805F49916B", "XML_Value")]
        public void ColumnNameForXmlOrJsonReturnType(string exampleServerGenerated, string expected)
        {
            // Arrange
            var col = new DataColumn
            {
                DataType = typeof(string),
                ColumnName = exampleServerGenerated,
            };

            // Act
            var result = _sut.WriteStoredProcReturnColumn(col);

            // Assert
            Assert.AreEqual($"public string {expected} {{ get; set; }}", result);
        }

        [TestCase(false, false, false, "DateTime? A, ref DateTime? B, DateTime? C = null, DateTime? D = null")]
        [TestCase(false, true,  false, "DateTime? A, ref DateTime? B, DateTime? C, DateTime? D")]
        [TestCase(true,  false, false, "DateTime? A, ref DateTime? B, DateTime? C = null, DateTime? D = null")]
        [TestCase(true,  true,  false, "DateTime? A, ref DateTime? B, DateTime? C, DateTime? D")]
        [TestCase(false, false, true, "DateTime? A, ref DateTime? B, DateTime? C = null, DateTime? D = null")]
        [TestCase(false, true,  true, "DateTime? A, ref DateTime? B, DateTime? C, DateTime? D")]
        [TestCase(true,  false, true, "DateTime? A, ref DateTime? B, DateTime? C, DateTime? D, out int procResult")]
        [TestCase(true,  true,  true, "DateTime? A, ref DateTime? B, DateTime? C, DateTime? D, out int procResult")]
        public void WriteStoredProcFunctionParams_HasTailNullable(bool includeProcResult, bool forInterface, bool hasReturnModel, string expected)
        {
            // Arrange
            if (hasReturnModel)
                _sut.ReturnModels = new List<List<DataColumn>> { new List<DataColumn> { new DataColumn("test")} };

            // Act
            var result = _sut.WriteStoredProcFunctionParams(includeProcResult, forInterface);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expected, result);
        }

        [TestCase(false, false, false, "DateTime? A, ref DateTime? B, DateTime? C, ref DateTime? D")]
        [TestCase(false, true,  false, "DateTime? A, ref DateTime? B, DateTime? C, ref DateTime? D")]
        [TestCase(true,  false, false, "DateTime? A, ref DateTime? B, DateTime? C, ref DateTime? D")]
        [TestCase(true,  true,  false, "DateTime? A, ref DateTime? B, DateTime? C, ref DateTime? D")]
        [TestCase(false, false, true, "DateTime? A, ref DateTime? B, DateTime? C, ref DateTime? D")]
        [TestCase(false, true,  true, "DateTime? A, ref DateTime? B, DateTime? C, ref DateTime? D")]
        [TestCase(true,  false, true, "DateTime? A, ref DateTime? B, DateTime? C, ref DateTime? D, out int procResult")]
        [TestCase(true,  true,  true, "DateTime? A, ref DateTime? B, DateTime? C, ref DateTime? D, out int procResult")]
        public void WriteStoredProcFunctionParams_NoTailNullable(bool includeProcResult, bool forInterface, bool hasReturnModel, string expected)
        {
            // Arrange - Set last to be an InOut ('ref') parameter.
            _sut.Parameters.Single(x => x.NameHumanCase == "D").Mode = StoredProcedureParameterMode.InOut;
            
            if (hasReturnModel)
                _sut.ReturnModels = new List<List<DataColumn>> { new List<DataColumn> { new DataColumn("test")} };

            // Act
            var result = _sut.WriteStoredProcFunctionParams(includeProcResult, forInterface);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expected, result);
        }

        [Test]
        [TestCase(false, false, false, 3)]
        [TestCase(false, true, false,  0)]
        [TestCase(true,  false, false, 0)]
        [TestCase(true,  true, false,  0)]
        [TestCase(false, false, true, 0)]
        [TestCase(false, true, true,  0)]
        [TestCase(true,  false, true, 0)]
        [TestCase(true,  true, true,  0)]
        public void WhichTailEndParametersCanBeNullable(bool includeProcResult, bool forInterface, bool noTailNullable, int expected)
        {
            if (noTailNullable)
            {
                // Arrange - Set last to be an InOut ('ref') parameter.
                _sut.Parameters.Single(x => x.NameHumanCase == "D").Mode = StoredProcedureParameterMode.InOut;
            }

            // Act
            var result = _sut.WhichTailEndParametersCanBeNullable(GetParams(), includeProcResult, forInterface);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void WhichTailEndParametersCanBeNullable_AllOutParameters()
        {
            foreach (var parameter in _sut.Parameters)
            {
                parameter.Mode = StoredProcedureParameterMode.InOut;
            }

            // Act
            var result = _sut.WhichTailEndParametersCanBeNullable(GetParams(), false, false);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result);
        }

        [Test]
        public void WhichTailEndParametersCanBeNullable_AllInParameters()
        {
            foreach (var parameter in _sut.Parameters)
            {
                parameter.Mode = StoredProcedureParameterMode.In;
            }

            // Act
            var result = _sut.WhichTailEndParametersCanBeNullable(GetParams(), false, false);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result);
        }

        [Test]
        public void WhichTailEndParametersCanBeNullable_AllInOutParameters()
        {
            foreach (var parameter in _sut.Parameters)
            {
                parameter.Mode = StoredProcedureParameterMode.InOut;
            }

            // Act
            var result = _sut.WhichTailEndParametersCanBeNullable(GetParams(), false, false);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result);
        }

        [Test]
        public void ThisHasMixedOutParameters()
        {
            // Arrange
            _sut.Parameters = new List<StoredProcedureParameter>
            {
                new StoredProcedureParameter
                {
                    Mode = StoredProcedureParameterMode.In,
                    NameHumanCase = "foo",
                    PropertyType = "DateTime",
                    Ordinal = 1
                },
                new StoredProcedureParameter
                {
                    Mode = StoredProcedureParameterMode.InOut,
                    NameHumanCase = "firstOutParam",
                    PropertyType = "int",
                    Ordinal = 2
                },
                new StoredProcedureParameter
                {
                    Mode = StoredProcedureParameterMode.In,
                    NameHumanCase = "bar",
                    PropertyType = "DateTime",
                    Ordinal = 3
                },
                new StoredProcedureParameter
                {
                    Mode = StoredProcedureParameterMode.InOut,
                    NameHumanCase = "secondOutParam",
                    PropertyType = "int",
                    Ordinal = 4
                },
                new StoredProcedureParameter
                {
                    Mode = StoredProcedureParameterMode.In,
                    NameHumanCase = "baz",
                    PropertyType = "DateTime",
                    Ordinal = 5
                }
            };

            // Act
            var resultStringFf = _sut.WriteStoredProcFunctionParams(false, false);
            var resultStringTf = _sut.WriteStoredProcFunctionParams(true, false); // End up being false due to having no ReturnModels

            // Assert
            Assert.IsNotNull(resultStringFf);
            Assert.IsNotNull(resultStringTf);
            Assert.AreEqual(resultStringFf, resultStringTf);
            Assert.AreEqual("DateTime? foo, ref int? firstOutParam, DateTime? bar, ref int? secondOutParam, DateTime? baz = null", resultStringTf);
        }

        [Test]
        public void ParameterWithNotNullableType()
        {
            // Arrange
            _sut.Parameters = new List<StoredProcedureParameter>
            {
                new StoredProcedureParameter
                {
                    Mode = StoredProcedureParameterMode.In,
                    NameHumanCase = "a",
                    PropertyType = "int",
                    Ordinal = 1
                },
                new StoredProcedureParameter
                {
                    Mode = StoredProcedureParameterMode.In,
                    NameHumanCase = "type",
                    PropertyType = "DataTable",
                    Ordinal = 3
                },
                new StoredProcedureParameter
                {
                    Mode = StoredProcedureParameterMode.In,
                    NameHumanCase = "b",
                    PropertyType = "int",
                    Ordinal = 5
                }
            };

            // Act
            var result = _sut.WriteStoredProcFunctionParams(false, false);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("int? a, DataTable type, int? b = null", result);
        }

        [Test]
        [Description("Issue #859 - with NRT enabled: string params with NULL default become string? = null")]
        public void WriteStoredProcFunctionParams_WithDbDefaults_StringNullDefault_NrtEnabled()
        {
            Settings.AllowNullStrings = true;
            Settings.NullableReverseNavigationProperties = false;
            try
            {
                _sut.Parameters = new List<StoredProcedureParameter>
                {
                    new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "int",    NameHumanCase = "userId",          Ordinal = 1 },
                    new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "string", NameHumanCase = "clientId",         Ordinal = 2 },
                    new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "string", NameHumanCase = "tokenProvider",    Ordinal = 3, HasDefault = true, DefaultValue = "'FCV'" },
                    new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "string", NameHumanCase = "referringUrl",     Ordinal = 4, HasDefault = true, DefaultValue = null },
                    new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "string", NameHumanCase = "callbackUrl",      Ordinal = 5, HasDefault = true, DefaultValue = null },
                };

                var result = _sut.WriteStoredProcFunctionParams(false, false);

                Assert.AreEqual(
                    "int? userId, string clientId, string tokenProvider = \"FCV\", string? referringUrl = null, string? callbackUrl = null",
                    result);
            }
            finally
            {
                Settings.AllowNullStrings = false;
                Settings.NullableReverseNavigationProperties = false;
            }
        }

        [Test]
        [Description("Issue #859 - with NRT disabled (EF6): string params with NULL default become string = null (no ?)")]
        public void WriteStoredProcFunctionParams_WithDbDefaults_StringNullDefault_NrtDisabled()
        {
            Settings.AllowNullStrings = false;
            Settings.NullableReverseNavigationProperties = false;
            try
            {
                _sut.Parameters = new List<StoredProcedureParameter>
                {
                    new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "int",    NameHumanCase = "userId",          Ordinal = 1 },
                    new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "string", NameHumanCase = "clientId",         Ordinal = 2 },
                    new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "string", NameHumanCase = "tokenProvider",    Ordinal = 3, HasDefault = true, DefaultValue = "'FCV'" },
                    new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "string", NameHumanCase = "referringUrl",     Ordinal = 4, HasDefault = true, DefaultValue = null },
                    new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "string", NameHumanCase = "callbackUrl",      Ordinal = 5, HasDefault = true, DefaultValue = null },
                };

                var result = _sut.WriteStoredProcFunctionParams(false, false);

                // No ? on string — valid C# 7.3 (EF6 / .NET Framework)
                Assert.AreEqual(
                    "int? userId, string clientId, string tokenProvider = \"FCV\", string referringUrl = null, string callbackUrl = null",
                    result);
            }
            finally
            {
                Settings.AllowNullStrings = false;
                Settings.NullableReverseNavigationProperties = false;
            }
        }

        [Test]
        [Description("Issue #859 - int param with numeric default, decimal param with decimal default")]
        public void WriteStoredProcFunctionParams_WithDbDefaults_NumericDefaults()
        {
            _sut.Parameters = new List<StoredProcedureParameter>
            {
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "int",     NameHumanCase = "userId",       Ordinal = 1, HasDefault = true, DefaultValue = "12" },
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "int",     NameHumanCase = "userIdNull",   Ordinal = 2, HasDefault = true, DefaultValue = null },
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "string",  NameHumanCase = "clientName",   Ordinal = 3, HasDefault = true, DefaultValue = "'Hello'" },
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "string",  NameHumanCase = "clientDesc",   Ordinal = 4, HasDefault = true, DefaultValue = "'World'" },
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "decimal", NameHumanCase = "decimalValue", Ordinal = 5, HasDefault = true, DefaultValue = "1.234" },
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "float",   NameHumanCase = "realValue",    Ordinal = 6, HasDefault = true, DefaultValue = "9.876" },
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "double",  NameHumanCase = "floatValue",   Ordinal = 7, HasDefault = true, DefaultValue = "6.54" },
            };

            var result = _sut.WriteStoredProcFunctionParams(false, false);

            Assert.AreEqual(
                "int? userId = 12, int? userIdNull = null, string clientName = \"Hello\", string clientDesc = \"World\", decimal? decimalValue = 1.234m, float? realValue = 9.876f, double? floatValue = 6.54",
                result);
        }

        [Test]
        [Description("Issue #859 - DB defaults break the tail: only contiguous trailing defaults become optional")]
        public void WriteStoredProcFunctionParams_WithDbDefaults_OnlyTailBecomesOptional()
        {
            // @Required INT (no default), @Optional1 INT = 5, @Required2 INT (no default), @Optional2 INT = 10
            // Only @Optional2 is a contiguous tail default, so only it becomes optional.
            _sut.Parameters = new List<StoredProcedureParameter>
            {
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "int", NameHumanCase = "required",  Ordinal = 1 },
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "int", NameHumanCase = "optional1", Ordinal = 2, HasDefault = true, DefaultValue = "5" },
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "int", NameHumanCase = "required2", Ordinal = 3 },
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "int", NameHumanCase = "optional2", Ordinal = 4, HasDefault = true, DefaultValue = "10" },
            };

            var result = _sut.WriteStoredProcFunctionParams(false, false);

            Assert.AreEqual("int? required, int? optional1, int? required2, int? optional2 = 10", result);
        }

        [Test]
        [Description("Issue #859 - ExtractSqlServerParamDefault correctly extracts various default types")]
        public void ExtractSqlServerParamDefault_VariousDefaults()
        {
            const string definition = @"
CREATE OR ALTER PROCEDURE dbo.TestProc
    @UserId INT = 12,
    @ClientId NVARCHAR(50),
    @TokenProvider NVARCHAR(50) = 'FCV',
    @NullableStr NVARCHAR(MAX) = NULL,
    @DecimalVal DECIMAL(18, 4) = 1.234,
    @FloatVal FLOAT = 6.54
AS BEGIN SELECT 1 END";

            Assert.AreEqual("12",      NamingHelper.ExtractSqlServerParamDefault(definition, "@UserId"));
            Assert.IsNull(             NamingHelper.ExtractSqlServerParamDefault(definition, "@ClientId"));
            Assert.AreEqual("'FCV'",   NamingHelper.ExtractSqlServerParamDefault(definition, "@TokenProvider"));
            Assert.AreEqual("NULL",    NamingHelper.ExtractSqlServerParamDefault(definition, "@NullableStr"));
            Assert.AreEqual("1.234",   NamingHelper.ExtractSqlServerParamDefault(definition, "@DecimalVal"));
            Assert.AreEqual("6.54",    NamingHelper.ExtractSqlServerParamDefault(definition, "@FloatVal"));
        }

        [Test]
        [Description("Issue #859 - NormaliseParamDefault strips type casts and outer parens")]
        public void NormaliseParamDefault_StripsTypeCastsAndParens()
        {
            Assert.IsNull(  NamingHelper.NormaliseParamDefault("NULL"));
            Assert.IsNull(  NamingHelper.NormaliseParamDefault("NULL::integer"));
            Assert.IsNull(  NamingHelper.NormaliseParamDefault("(NULL)"));
            Assert.AreEqual("'FCV'",  NamingHelper.NormaliseParamDefault("'FCV'::character varying"));
            Assert.AreEqual("'FCV'",  NamingHelper.NormaliseParamDefault("'FCV'"));
            Assert.AreEqual("12",     NamingHelper.NormaliseParamDefault("12"));
            Assert.AreEqual("1.234",  NamingHelper.NormaliseParamDefault("1.234"));
        }

        // -----------------------------------------------------------------------
        // Issue #868 - InOut parameter support
        // -----------------------------------------------------------------------

        [Test]
        [Description("Issue #868 - InOut parameter emits 'ref' in overload call, Out emits 'out'")]
        public void WriteStoredProcFunctionOverloadCall_InOutUsesRef_OutUsesOut()
        {
            // Arrange
            _sut.Parameters = new List<StoredProcedureParameter>
            {
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In,    NameHumanCase = "clientId", Ordinal = 1 },
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.InOut, NameHumanCase = "groupId",  Ordinal = 2 },
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.Out,   NameHumanCase = "result",   Ordinal = 3 }
            };

            // Act
            var result = _sut.WriteStoredProcFunctionOverloadCall();

            // Assert
            StringAssert.Contains("ref groupId",  result);
            StringAssert.Contains("out result",   result);
            StringAssert.DoesNotContain("out groupId",  result);
            StringAssert.DoesNotContain("ref result",   result);
        }

        [Test]
        [Description("Issue #868 - InOut param generates ParameterDirection.InputOutput and sets Value")]
        public void WriteStoredProcFunctionDeclareSqlParameter_InOutParam_InputOutputDirectionAndValueSet()
        {
            // Arrange
            _sut.Parameters = new List<StoredProcedureParameter>
            {
                new StoredProcedureParameter
                {
                    Ordinal       = 1,
                    Name          = "@groupId",
                    NameHumanCase = "groupId",
                    Mode          = StoredProcedureParameterMode.InOut,
                    SqlDbType     = "Int",
                    PropertyType  = "int",
                    Precision     = 10,
                    Scale         = 0,
                    MaxLength     = 0
                }
            };

            // Act
            var result = _sut.WriteStoredProcFunctionDeclareSqlParameter(false);

            // Assert - direction must be InputOutput (not Output) and value must be passed in
            StringAssert.Contains("Direction = ParameterDirection.InputOutput", result);
            StringAssert.Contains("Value = groupId.GetValueOrDefault()",        result);
            StringAssert.DoesNotContain("Direction = ParameterDirection.Output", result);
        }

        [Test]
        [Description("Issue #868 - Out param generates ParameterDirection.Output and does NOT set Value")]
        public void WriteStoredProcFunctionDeclareSqlParameter_OutParam_OutputDirectionNoValue()
        {
            // Arrange
            _sut.Parameters = new List<StoredProcedureParameter>
            {
                new StoredProcedureParameter
                {
                    Ordinal       = 1,
                    Name          = "@result",
                    NameHumanCase = "result",
                    Mode          = StoredProcedureParameterMode.Out,
                    SqlDbType     = "Int",
                    PropertyType  = "int",
                    Precision     = 10,
                    Scale         = 0,
                    MaxLength     = 0
                }
            };

            // Act
            var result = _sut.WriteStoredProcFunctionDeclareSqlParameter(false);

            // Assert - direction is Output, no Value assignment
            StringAssert.Contains("Direction = ParameterDirection.Output", result);
            StringAssert.DoesNotContain("Value = result",                   result);
        }

        [Test]
        [Description("Issue #868 - Full scenario: groupId OUTPUT parameter uses ref, InputOutput, Value")]
        public void Issue868_InOut_GroupIdOutputParam_GeneratesCorrectCode()
        {
            // SQL: @groupId INT OUTPUT  -- SQL Server always reports PARAMETER_MODE = INOUT
            //      @clientId INT
            //      @displayName VARCHAR(30)
            _sut.Parameters = new List<StoredProcedureParameter>
            {
                new StoredProcedureParameter { Ordinal = 1, Name = "@groupId",     NameHumanCase = "groupId",     Mode = StoredProcedureParameterMode.InOut, SqlDbType = "Int",     PropertyType = "int",    Precision = 10 },
                new StoredProcedureParameter { Ordinal = 2, Name = "@clientId",    NameHumanCase = "clientId",    Mode = StoredProcedureParameterMode.In,    SqlDbType = "Int",     PropertyType = "int",    Precision = 10 },
                new StoredProcedureParameter { Ordinal = 3, Name = "@displayName", NameHumanCase = "displayName", Mode = StoredProcedureParameterMode.In,    SqlDbType = "VarChar", PropertyType = "string", MaxLength = 30 }
            };

            var funcParams  = _sut.WriteStoredProcFunctionParams(false, false);
            var sqlParams   = _sut.WriteStoredProcFunctionDeclareSqlParameter(false);
            var overload    = _sut.WriteStoredProcFunctionOverloadCall();

            // Function signature: groupId should be ref (caller passes a value AND receives one back)
            StringAssert.Contains("ref int? groupId",                    funcParams);
            StringAssert.DoesNotContain("out int? groupId",              funcParams);

            // SqlParameter: direction must be InputOutput and value must be pre-populated from the argument
            StringAssert.Contains("Direction = ParameterDirection.InputOutput", sqlParams);
            StringAssert.Contains("Value = groupId.GetValueOrDefault()",        sqlParams);

            // Overload call: ref keyword
            StringAssert.Contains("ref groupId",                         overload);
        }

        // -----------------------------------------------------------------------
        // Nullable warnings in generated EF Core code under AllowNullStrings
        // (the file carries '#nullable enable', so every mismatch is a warning).
        // -----------------------------------------------------------------------

        [Test]
        [Description("CS8620 - the array holds SqlParameter locals, which are never null, so an (object?) cast only made it object?[]")]
        public void WriteStoredProcFunctionSqlParameterAnonymousArray_EfCore8PlusWithAllowNullStrings_DoesNotCastToNullableObject()
        {
            // Arrange
            Settings.AllowNullStrings = true;

            // Act
            var result = _sut.WriteStoredProcFunctionSqlParameterAnonymousArray(true, true, true, true);

            // Assert
            Assert.That(result, Is.EqualTo(",  new[] {AParam, BParam, CParam, XParam, DParam, procResultParam}, cancellationToken"));
        }

        [Description("CS8604 - a NULL-default string must be string? in every overload, or the short overload passes string? to string")]
        [TestCase(false, false, "int? userId, string? referringUrl = null")]
        [TestCase(true,  false, "int? userId, string? referringUrl, out int procResult")]
        [TestCase(false, true,  "int? userId, string? referringUrl")]
        [TestCase(true,  true,  "int? userId, string? referringUrl, out int procResult")]
        public void WriteStoredProcFunctionParams_StringNullDefaultWithAllowNullStrings_IsNullableInEveryOverload(bool includeProcResult, bool forInterface, string expected)
        {
            // Arrange
            Settings.AllowNullStrings = true;
            _sut.ReturnModels = new List<List<DataColumn>> { new List<DataColumn> { new DataColumn("Id", typeof(int)) } };
            _sut.Parameters = new List<StoredProcedureParameter>
            {
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "int",    NameHumanCase = "userId",       Ordinal = 1 },
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "string", NameHumanCase = "referringUrl", Ordinal = 2, HasDefault = true, DefaultValue = null },
            };

            // Act
            var result = _sut.WriteStoredProcFunctionParams(includeProcResult, forInterface);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Description("CS8625 - an INOUT string comes back NULL whenever the database returns NULL, and the generated code assigns that to the ref argument")]
        [TestCase(true,  "int? someValue, ref string? someString")]
        [TestCase(false, "int? someValue, ref string someString")]
        public void WriteStoredProcFunctionParams_InOutString_IsNullableOnlyWithAllowNullStrings(bool allowNullStrings, string expected)
        {
            // Arrange
            Settings.AllowNullStrings = allowNullStrings;
            _sut.Parameters = new List<StoredProcedureParameter>
            {
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In,    PropertyType = "int",    NameHumanCase = "someValue",  Ordinal = 1 },
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.InOut, PropertyType = "string", NameHumanCase = "someString", Ordinal = 2 },
            };

            // Act
            var result = _sut.WriteStoredProcFunctionParams(false, false);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Description("CS8604 - a TVF passes its arguments straight to FromSqlRaw's params object[], whose elements are non-nullable under NRT")]
        [TestCase(true,  ", (object?)id ?? DBNull.Value, name, (object?)filter ?? DBNull.Value")]
        [TestCase(false, ", id, name, filter")]
        public void WriteStoredProcFunctionSqlParameterAnonymousArray_TvfArguments_CoalesceNullableArgumentsOnlyUnderNrt(bool allowNullStrings, string expected)
        {
            // Arrange
            Settings.AllowNullStrings = allowNullStrings;
            _sut.IsStoredProcedure     = false;
            _sut.IsTableValuedFunction = true;
            _sut.Parameters = new List<StoredProcedureParameter>
            {
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "int",    NameHumanCase = "id",     Ordinal = 1 },
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "string", NameHumanCase = "name",   Ordinal = 2 },
                new StoredProcedureParameter { Mode = StoredProcedureParameterMode.In, PropertyType = "string", NameHumanCase = "filter", Ordinal = 3, HasDefault = true, DefaultValue = null },
            };

            // Act
            var result = _sut.WriteStoredProcFunctionSqlParameterAnonymousArray(false, false);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        // -----------------------------------------------------------------------
        // Issue #885 - stored proc return model columns must respect DB nullability
        // under nullable reference types, otherwise EF Core 8+ infers the column as
        // required and throws SqlNullValueException when the database returns NULL.
        // -----------------------------------------------------------------------

        [TearDown]
        public void TearDown()
        {
            // Settings is static; restore the values this fixture changes so other fixtures are unaffected.
            Settings.AllowNullStrings                    = false;
            Settings.NullableReverseNavigationProperties = false;
            Settings.NullableShortHand                   = true;
        }

        [Description("Issue #885 - DB-nullable string return columns become string? under NRT; NOT NULL columns keep = null!")]
        [TestCase(false, true,  "public string SomeText { get; set; }")]          // Oblivious: unchanged
        [TestCase(false, false, "public string SomeText { get; set; }")]          // Oblivious: unchanged
        [TestCase(true,  true,  "public string? SomeText { get; set; }")]         // NRT: DB-nullable => string?
        [TestCase(true,  false, "public string SomeText { get; set; } = null!;")] // NRT: NOT NULL => null-forgiving
        public void WriteStoredProcReturnColumn_String_RespectsDbNullability(bool allowNullStrings, bool allowDbNull, string expected)
        {
            // Arrange
            Settings.AllowNullStrings = allowNullStrings;
            var col = new DataColumn("SomeText", typeof(string)) { AllowDBNull = allowDbNull };

            // Act
            var result = _sut.WriteStoredProcReturnColumn(col);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [Description("Issue #885 - byte[] return columns follow the same rules as string")]
        [TestCase(false, true,  "public byte[] Blob { get; set; }")]
        [TestCase(true,  true,  "public byte[]? Blob { get; set; }")]
        [TestCase(true,  false, "public byte[] Blob { get; set; } = null!;")]
        public void WriteStoredProcReturnColumn_ByteArray_RespectsDbNullability(bool allowNullStrings, bool allowDbNull, string expected)
        {
            // Arrange
            Settings.AllowNullStrings = allowNullStrings;
            var col = new DataColumn("Blob", typeof(byte[])) { AllowDBNull = allowDbNull };

            // Act
            var result = _sut.WriteStoredProcReturnColumn(col);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [Description("Issue #885 - value type return columns are unaffected by AllowNullStrings")]
        [TestCase(false, true,  "public int? Num { get; set; }")]
        [TestCase(true,  true,  "public int? Num { get; set; }")]
        [TestCase(true,  false, "public int Num { get; set; }")]
        public void WriteStoredProcReturnColumn_ValueType_Unaffected(bool allowNullStrings, bool allowDbNull, string expected)
        {
            // Arrange
            Settings.AllowNullStrings = allowNullStrings;
            var col = new DataColumn("Num", typeof(int)) { AllowDBNull = allowDbNull };

            // Act
            var result = _sut.WriteStoredProcReturnColumn(col);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [Test]
        [Description("Issue #885 - reference types always use the ? annotation; Nullable<string> is not valid C#")]
        public void WriteStoredProcReturnColumn_String_NullableShortHandOff_StillUsesQuestionMark()
        {
            // Arrange
            Settings.AllowNullStrings  = true;
            Settings.NullableShortHand = false; // value types render as Nullable<T>
            var col = new DataColumn("SomeText", typeof(string)) { AllowDBNull = true };

            // Act
            var result = _sut.WriteStoredProcReturnColumn(col);

            // Assert
            Assert.AreEqual("public string? SomeText { get; set; }", result);
        }

        [Test]
        [Description("Issue #885 - NRT via NullableReverseNavigationProperties only: string stays non-nullable, so the fluent config must carry IsRequired(false)")]
        public void GetReturnColumnMappings_NrtWithoutAllowNullStrings_EmitsIsRequiredFalse()
        {
            // Arrange - NeedsNullForgiving() is true but AllowNullStrings is false, so the property is
            // emitted as a plain 'string' inside a '#nullable enable' file. Without explicit configuration
            // EF Core 8+ would infer the column as required.
            Settings.AllowNullStrings                    = false;
            Settings.NullableReverseNavigationProperties = true;
            _sut.ReturnModels = new List<List<DataColumn>>
            {
                new List<DataColumn>
                {
                    new DataColumn("SomeText", typeof(string)) { AllowDBNull = true },
                    new DataColumn("NotNullText", typeof(string)) { AllowDBNull = false },
                    new DataColumn("Num", typeof(int)) { AllowDBNull = true }
                }
            };

            // Act
            var mappings = _sut.GetReturnColumnMappings("Entity", "MyReturnModel");

            // Assert
            CollectionAssert.Contains(mappings, "modelBuilder.Entity<MyReturnModel>().Property(e => e.SomeText).IsRequired(false);");
            Assert.IsFalse(mappings.Any(x => x.Contains("NotNullText")), "NOT NULL columns must not be marked optional");
            Assert.IsFalse(mappings.Any(x => x.Contains(".Num)")), "value types carry their nullability in the type; no mapping needed");
        }

        [Test]
        [Description("Issue #885 - with AllowNullStrings the property is already string?, so no IsRequired(false) mapping is emitted")]
        public void GetReturnColumnMappings_AllowNullStrings_NoIsRequiredMapping()
        {
            // Arrange
            Settings.AllowNullStrings = true;
            _sut.ReturnModels = new List<List<DataColumn>>
            {
                new List<DataColumn> { new DataColumn("SomeText", typeof(string)) { AllowDBNull = true } }
            };

            // Act
            var mappings = _sut.GetReturnColumnMappings("Entity", "MyReturnModel");

            // Assert
            Assert.IsFalse(mappings.Any(x => x.Contains("IsRequired")), "string? already tells EF Core the column is nullable");
        }

        [Test]
        [Description("Issue #885 - default (nullable-oblivious) settings produce no IsRequired(false) mappings; output is unchanged")]
        public void GetReturnColumnMappings_Oblivious_NoIsRequiredMapping()
        {
            // Arrange - defaults: AllowNullStrings = false, NullableReverseNavigationProperties = false
            _sut.ReturnModels = new List<List<DataColumn>>
            {
                new List<DataColumn> { new DataColumn("SomeText", typeof(string)) { AllowDBNull = true } }
            };

            // Act
            var mappings = _sut.GetReturnColumnMappings("Entity", "MyReturnModel");

            // Assert
            Assert.IsFalse(mappings.Any(x => x.Contains("IsRequired")), "oblivious code needs no explicit nullability; EF Core already treats reference types as nullable");
        }

        private List<StoredProcedureParameter> GetParams()
        {
            return _sut.Parameters.Where(x => x.Mode != StoredProcedureParameterMode.Out).OrderBy(x => x.Ordinal).ToList();
        }
    }
}