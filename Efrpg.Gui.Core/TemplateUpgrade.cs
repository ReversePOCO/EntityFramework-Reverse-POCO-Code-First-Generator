using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Efrpg.Gui
{
    /// <summary>
    ///     Upgrades a v3 <c>Database.tt</c> to v4, or refuses and says why.
    /// </summary>
    /// <remarks>
    ///     Six edits are required, because without them the template does not compile or does not run. Twelve other
    ///     blocks differ between a stock v3 and a stock v4 file and are **deliberately not touched**: the version
    ///     header, two mentions of the v3 include inside comment prose, and the trailing-comment improvements made
    ///     in v4. A customer's file will already differ there, and rewriting comments they may have edited
    ///     themselves is exactly the over-reach the refusal rule exists to prevent.
    ///
    ///     **Refusing matters more than upgrading.** When 24 in-repo templates were migrated by script it took two
    ///     passes, because some carried an extra commented-out line inside the block the first pattern expected -
    ///     and those were files under one person's control. Customer files vary more. A half-applied migration
    ///     leaves a template that neither compiles nor matches the upgrade guide, which is worse than not offering
    ///     the button at all.
    /// </remarks>
    public sealed class TemplateUpgrade
    {
        public const string V3Include = "EF.Reverse.POCO.v3.ttinclude";
        public const string V4Include = "EF.Reverse.POCO.v4.ttinclude";

        /// <summary>
        ///     The entry point block v4 requires, which replaces the v3 one wholesale rather than being patched:
        ///     <c>fileManagement</c> moves above the try block, so the whole span has to go.
        /// </summary>
        /// <remarks>
        ///     <c>TemplateUpgradeTests</c> asserts this is byte for byte what the shipped Database.tt carries, so a
        ///     change to BuildTT's footer fails the build rather than leaving the upgrade emitting last year's code.
        /// </remarks>
        public const string V4EntryPoint =
            "    var outer = (GeneratedTextTransformation) this;\r\n" +
            "    var fileManagement = new FileManagementService(outer);\r\n" +
            "\r\n" +
            "    EfrpgResult toolResult = null;\r\n" +
            "    var efrpgToolOk = true;\r\n" +
            "    try\r\n" +
            "    {\r\n" +
            "        // Connection strings are passed to the tool over stdin, never on the command line, so they stay out of\r\n" +
            "        // process listings and command-line audit logs. See SecretsXml and EfrpgToolRunner.\r\n" +
            "        toolResult = EfrpgToolRunner.ReadDatabase(\r\n" +
            "            FilterSettings.IncludeStoredProcedures || FilterSettings.IncludeTableValuedFunctions || FilterSettings.IncludeScalarValuedFunctions,\r\n" +
            "            FilterSettings.IncludeSynonyms);\r\n" +
            "    }\r\n" +
            "    catch (Exception efrpgEx)\r\n" +
            "    {\r\n" +
            "        fileManagement.Error(\"// -----------------------------------------------------------------------------------------\");\r\n" +
            "        if (efrpgEx is System.ComponentModel.Win32Exception)\r\n" +
            "            fileManagement.Error(\"// efrpg tool not found. Install it with: dotnet tool install -g Efrpg\");\r\n" +
            "        else\r\n" +
            "            fileManagement.Error(\"// efrpg tool reported an error:\");\r\n" +
            "        fileManagement.Error(\"// \" + efrpgEx.Message.Replace(\"\\r\\n\", \" \").Replace(\"\\n\", \" \"));\r\n" +
            "        fileManagement.Error(\"// -----------------------------------------------------------------------------------------\");\r\n" +
            "        efrpgToolOk = false;\r\n" +
            "    }\r\n" +
            "\r\n" +
            "    if (efrpgToolOk)\r\n" +
            "    {\r\n" +
            "        var generator = GeneratorFactory.Create(toolResult, fileManagement);\r\n" +
            "        if (generator != null && generator.InitialisationOk)\r\n" +
            "        {\r\n" +
            "            generator.ReadDatabase();\r\n" +
            "            generator.GenerateCode();\r\n" +
            "        }\r\n" +
            "        fileManagement.Process(true);\r\n" +
            "    }\r\n" +
            "#>";

        /// <summary>
        ///     Every shape the v3 entry point has shipped in, with comments stripped and all whitespace removed.
        ///     v3.0.8 to v3.5.0 tested <c>generator.InitialisationOk</c> alone;
        ///     v3.6.0 added the null check. The machine.config comment moved in v3.11.0, but comments are ignored so
        ///     that needs no shape of its own.
        /// </summary>
        private static readonly string[] V3EntryPointShapes =
        {
            NormaliseCode(
                "var outer = (GeneratedTextTransformation) this; " +
                "var fileManagement = new FileManagementService(outer); " +
                "var generator = GeneratorFactory.Create(fileManagement, FileManagerFactory.GetFileManagerType()); " +
                "if (generator != null && generator.InitialisationOk) { generator.ReadDatabase(); generator.GenerateCode(); } " +
                "fileManagement.Process(true);#>"),
            NormaliseCode(
                "var outer = (GeneratedTextTransformation) this; " +
                "var fileManagement = new FileManagementService(outer); " +
                "var generator = GeneratorFactory.Create(fileManagement, FileManagerFactory.GetFileManagerType()); " +
                "if (generator.InitialisationOk) { generator.ReadDatabase(); generator.GenerateCode(); } " +
                "fileManagement.Process(true);#>")
        };

        /// <summary>
        ///     Settings v3 had and v4 does not, because multi-context generation was removed before v4 shipped.
        ///     Each is deleted as a whole statement; the last four are multi-line delegates in a stock v3 file.
        /// </summary>
        private static readonly string[] MultiContextSettings =
        {
            "GenerateSingleDbContext",
            "MultiContextSettingsConnectionString",
            "MultiContextSettingsPlugin",
            "MultiContextAttributeDelimiter",
            "MultiContextAllFieldsColumnProcessing",
            "MultiContextAllFieldsTableProcessing",
            "MultiContextAllFieldsStoredProcedureProcessing",
            "MultiContextAllFieldsFunctionProcessing"
        };

        private static readonly Regex MultiContextInUse =
            new Regex(@"^[ \t]*Settings\.GenerateSingleDbContext[ \t]*=[ \t]*false\b", RegexOptions.Multiline);

        private static readonly Regex FileBasedInUse =
            new Regex(@"^[ \t]*Settings\.TemplateType[ \t]*=[ \t]*TemplateType\.FileBased", RegexOptions.Multiline);

        private static readonly Regex CustomGeneratorInUse =
            new Regex(@"^[ \t]*Settings\.GeneratorType[ \t]*=[ \t]*GeneratorType\.Custom\b", RegexOptions.Multiline);

        private static readonly Regex SqlCeInUse =
            new Regex(@"^[ \t]*Settings\.DatabaseType[ \t]*=[ \t]*DatabaseType\.SqlCe\b", RegexOptions.Multiline);

        /// <summary>
        ///     The stock v3 data-annotations block asks whether the database is SQL CE before deciding on
        ///     <c>[MaxLength]</c>. v4 has no SQL CE, so the comparison becomes <c>false</c> and the block keeps working.
        /// </summary>
        private static readonly Regex SqlCeComparison =
            new Regex(@"Settings\.DatabaseType\s*==\s*DatabaseType\.SqlCe\b");

        private static readonly Regex IncludeDirective =
            new Regex(@"^<#@\s*include\s+file\s*=\s*""(?<include>[^""]+)""\s*#>", RegexOptions.Multiline);

        private static readonly Regex EntryPoint =
            new Regex(@"^[ \t]*var\s+outer\s*=\s*\(GeneratedTextTransformation\)", RegexOptions.Multiline);

        private readonly string _text;
        private readonly List<TemplateUpgradeChange> _changes = new List<TemplateUpgradeChange>();
        private readonly List<string> _blockers = new List<string>();

        private TemplateUpgrade(string text)
        {
            _text = text;
        }

        /// <summary>
        ///     True when the include directive names the v3 file. This is the only reliable marker: the version
        ///     comment underneath it is prose a user may have edited or removed.
        /// </summary>
        public static bool IsV3(string templateText)
        {
            var match = IncludeDirective.Match(templateText ?? string.Empty);

            return match.Success &&
                   match.Groups["include"].Value.IndexOf(V3Include, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static TemplateUpgradeResult Upgrade(string templateText)
        {
            if (templateText == null)
                throw new ArgumentNullException(nameof(templateText));

            return new TemplateUpgrade(templateText).Run();
        }

        private TemplateUpgradeResult Run()
        {
            if (!IsV3(_text))
                return TemplateUpgradeResult.Refused(new[]
                {
                    "This template does not include " + V3Include + ", so there is nothing to upgrade."
                });

            // A template that generates several contexts has nowhere to go: v4 removed the feature, and v3 keeps
            // working. Said before any edit, so the user is not shown a list of changes that cannot be applied.
            if (MultiContextInUse.IsMatch(_text))
                return TemplateUpgradeResult.Refused(new[]
                {
                    "This template generates multiple DbContexts (Settings.GenerateSingleDbContext = false), which " +
                    "v4 removed. Stay on v3 for this project; v3 remains downloadable and continues to work."
                });

            if (FileBasedInUse.IsMatch(_text))
                return TemplateUpgradeResult.Refused(new[]
                {
                    "This template uses a file-based template type, which v4 removed along with Settings.TemplateFolder. " +
                    "Stay on v3 for this project, or switch to the built-in EfCore or Ef6 template type first."
                });

            if (CustomGeneratorInUse.IsMatch(_text))
                return TemplateUpgradeResult.Refused(new[]
                {
                    "This template uses GeneratorType.Custom, which v4 removed. Stay on v3 for this project, or switch " +
                    "to GeneratorType.EfCore or GeneratorType.Ef6 first."
                });

            if (SqlCeInUse.IsMatch(_text))
                return TemplateUpgradeResult.Refused(new[]
                {
                    "This template reads a SQL Server Compact database (DatabaseType.SqlCe), which v4 removed. " +
                    "Stay on v3 for this project; v3 remains downloadable and continues to work."
                });

            var text = _text;

            text = SwapInclude(text);
            text = DeleteSetting(text, "FileManagerType",
                "Settings.FileManagerType no longer exists in v4 - the file manager is chosen automatically.");
            text = DeleteSetting(text, "DatabaseReaderPlugin",
                "Settings.DatabaseReaderPlugin no longer exists in v4 - database reading moved into the efrpg tool.");

            foreach (var setting in MultiContextSettings)
                text = DeleteStatement(text, setting,
                    "Settings." + setting + " no longer exists in v4 - multi-context generation was removed.");

            text = DeleteSetting(text, "TemplateFolder",
                "Settings.TemplateFolder no longer exists in v4 - file-based templates were removed.");
            text = DeleteSetting(text, "GeneratorType",
                "Settings.GeneratorType no longer exists in v4 - TemplateType alone decides which generator runs.");
            text = DeleteSetting(text, "GenerationLanguage",
                "Settings.GenerationLanguage no longer exists in v4 - the generator only writes C#.");
            text = DeleteSetting(text, "FileExtension",
                "Settings.FileExtension no longer exists in v4 - generated files are always .cs.");
            text = DeleteSetting(text, "IncludeQueryTraceOn9481Flag",
                "Settings.IncludeQueryTraceOn9481Flag no longer exists in v4 - the SQL Server 2014 workaround was retired.");
            text = DeleteSetting(text, "ForeignKeyNamingStrategy",
                "Settings.ForeignKeyNamingStrategy no longer exists in v4 - only the Current strategy remains; Beta was never completed.");
            text = SimplifySeparateFilesCondition(text);
            text = RenameCleanUp(text);
            text = AddJsonColumnMappingsToUpdateColumn(text);
            text = RemoveSqlCeComparison(text);
            text = RaiseTemplateTypeToEfCore8(text);
            text = ConvertStringArraysToLists(text);
            text = SplitApplyColumnCustomizations(text);
            text = ReplaceEntryPoint(text);

            // Only when everything else worked. A refused entry point still contains
            // FileManagerFactory.GetFileManagerType(), so checking here anyway would add a second blocker
            // that is a consequence of the first rather than an independent problem to fix.
            //
            // One check per removed name, not per spelling of it, so a line mentioning both
            // Settings.FileManagerType and FileManagerType.Null does not produce two identical blockers.
            if (_blockers.Count == 0)
            {
                LeftoverCheckInCode(text, "FileManagerType");
                LeftoverCheckInCode(text, "DatabaseReaderPlugin");
                LeftoverCheckInCode(text, "DatabaseReader.");
                LeftoverCheckInCode(text, "Settings.MultiContext");
                LeftoverCheckInCode(text, "Settings.GenerateSingleDbContext");
                LeftoverCheckInCode(text, "Settings.TemplateFolder");
                LeftoverCheckInCode(text, "TemplateType.FileBased");
                LeftoverCheckInCode(text, "GeneratorType.Custom");
                LeftoverCheckInCode(text, "ForeignKeyNamingStrategy");
                LeftoverCheckInCode(text, "DatabaseType.SqlCe");
            }

            return _blockers.Count > 0
                ? TemplateUpgradeResult.Refused(_blockers)
                : TemplateUpgradeResult.Upgraded(text, _changes);
        }

        private string SwapInclude(string text)
        {
            var match = IncludeDirective.Match(text);
            var before = match.Value;
            var after = before.Replace(V3Include, V4Include);

            Record("Point the include directive at the v4 template.", before, after);

            return text.Substring(0, match.Index) + after + text.Substring(match.Index + match.Length);
        }

        /// <summary>
        ///     Removes a whole setting line, including its line ending, so nothing is left behind but the settings
        ///     around it - which keep their alignment because only complete lines are removed.
        /// </summary>
        private string DeleteSetting(string text, string settingName, string why)
        {
            var pattern = new Regex(@"^[ \t]*Settings\." + Regex.Escape(settingName) + @"[ \t]*=[^\r\n]*\r?\n",
                RegexOptions.Multiline);
            var match = pattern.Match(text);

            // Already absent is not a problem. A user who deleted it themselves has done half the upgrade.
            if (!match.Success)
                return text;

            Record(why, match.Value.TrimEnd('\r', '\n'), string.Empty);

            return text.Substring(0, match.Index) + text.Substring(match.Index + match.Length);
        }

        /// <summary>
        ///     Removes a whole statement, however many lines it spans, through the same scanner the settings editor
        ///     uses. The one-line <see cref="DeleteSetting"/> would leave the body of a delegate behind.
        /// </summary>
        private string DeleteStatement(string text, string settingName, string why)
        {
            var document   = TemplateSettingsDocument.Parse(text);
            var assignment = document.Assignments.FirstOrDefault(a => a.Name == settingName && !a.IsCommentedOut);

            if (assignment == null)
                return text;

            Record(why, document.StatementText(assignment), string.Empty);

            return document.WithoutAssignment(assignment).Text;
        }

        /// <summary>
        ///     Anything still naming a type or setting that v4 removed will not compile, so it is a refusal rather
        ///     than something to leave for the user to find at generation time. Line comments are ignored: stock v3
        ///     files mention the removed settings in prose - v3.5.0 to v3.6.0 said "Only activated if
        ///     Settings.FileManagerType = FileManagerType.EfCore" above the sub-folder block - and prose is not a
        ///     compile error.
        /// </summary>
        private void LeftoverCheckInCode(string text, string fragment)
        {
            foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
            {
                var scanner = new StatementScanner();
                scanner.Feed(line);

                var code = scanner.LineCommentIndex >= 0 ? line.Substring(0, scanner.LineCommentIndex) : line;
                if (code.IndexOf(fragment, StringComparison.Ordinal) < 0)
                    continue;

                _blockers.Add("This template still refers to '" + fragment +
                              "', which v4 removed, in a place this upgrade does not know how to change: " + line.Trim());
                return;
            }
        }

        private string SimplifySeparateFilesCondition(string text)
        {
            var pattern = new Regex(
                @"if[ \t]*\([ \t]*Settings\.GenerateSeparateFiles[ \t]*&&[ \t]*Settings\.FileManagerType[ \t]*==[ \t]*FileManagerType\.\w+[ \t]*\)");
            var match = pattern.Match(text);

            if (!match.Success)
                return text;

            const string after = "if (Settings.GenerateSeparateFiles)";
            Record("Drop the FileManagerType half of the sub-folder condition.", match.Value, after);

            return text.Substring(0, match.Index) + after + text.Substring(match.Index + match.Length);
        }

        private string RenameCleanUp(string text)
        {
            if (text.IndexOf("DatabaseReader.CleanUp", StringComparison.Ordinal) < 0)
                return text;

            Record("DatabaseReader moved into the efrpg tool; CleanUp now lives on NamingHelper.",
                "DatabaseReader.CleanUp", "NamingHelper.CleanUp");

            return text.Replace("DatabaseReader.CleanUp", "NamingHelper.CleanUp");
        }

        /// <summary>
        ///     v4 gave <c>Settings.UpdateColumn</c> a fourth parameter for JSON column mappings. The v3 three-parameter
        ///     delegate is a compile error against the v4 include, so the parameter is appended. The body is left
        ///     alone: it is the user's code, and nothing in it needs the new parameter.
        /// </summary>
        private string AddJsonColumnMappingsToUpdateColumn(string text)
        {
            var pattern = new Regex(
                @"Settings\.UpdateColumn\s*=\s*delegate\s*\(\s*Column\s+(?<column>\w+)\s*,\s*Table\s+(?<table>\w+)\s*,\s*List<EnumDefinition>\s+(?<enums>\w+)\s*\)");
            var match = pattern.Match(text);

            if (!match.Success)
                return text;

            var after = "Settings.UpdateColumn = delegate(Column " + match.Groups["column"].Value +
                        ", Table " + match.Groups["table"].Value +
                        ", List<EnumDefinition> " + match.Groups["enums"].Value +
                        ", List<JsonColumnMapping> jsonColumnMappings)";

            Record("Settings.UpdateColumn takes a fourth parameter in v4, the JSON column mappings.", match.Value, after);

            return text.Substring(0, match.Index) + after + text.Substring(match.Index + match.Length);
        }

        /// <summary>
        ///     v4 generates for EF Core 8 and later only. A template targeting EF Core 2 to 7 is moved to EfCore8, the
        ///     lowest v4 offers, and the change says so: the user's project has to be on EF Core 8 or later for v4
        ///     to be of any use to it.
        /// </summary>
        private string RaiseTemplateTypeToEfCore8(string text)
        {
            var pattern = new Regex(@"^(?<lead>[ \t]*Settings\.TemplateType[ \t]*=[ \t]*)TemplateType\.EfCore[2-7]\b", RegexOptions.Multiline);
            var match = pattern.Match(text);

            if (!match.Success)
                return text;

            var after = match.Groups["lead"].Value + "TemplateType.EfCore8";

            Record("v4 generates for EF Core 8 and later only. The template type is moved to EfCore8; make sure the " +
                   "project itself is on EF Core 8 or later, and pick EfCore9 or EfCore10 if it is.",
                match.Value.Trim(), after.Trim());

            return text.Substring(0, match.Index) + after + text.Substring(match.Index + match.Length);
        }

        /// <summary>
        ///     For three weeks in November 2019 the stock v3.0.8 template assigned <c>new []{ "" }</c> to two
        ///     settings that were already lists by the next revision. A string array is a compile error against
        ///     a <c>List&lt;string&gt;</c> setting, so the initialiser is rewritten and the elements kept.
        /// </summary>
        private string ConvertStringArraysToLists(string text)
        {
            var pattern = new Regex(
                @"^(?<lead>[ \t]*Settings\.(AdditionalNamespaces|AdditionalContextInterfaceItems|AdditionalFileHeaderText|AdditionalFileFooterText)[ \t]*=[ \t]*)new[ \t]*(string)?[ \t]*\[[ \t]*\][ \t]*(?=\{)",
                RegexOptions.Multiline);

            foreach (Match match in pattern.Matches(text))
                Record("This setting is a List<string> in v4, not a string array.", match.Value.Trim(),
                    (match.Groups["lead"].Value + "new List<string>").Trim());

            return pattern.Replace(text, "${lead}new List<string>");
        }

        /// <summary>
        ///     For two weeks in early 2026, master only, the stock UpdateColumn called one
        ///     <c>Settings.ApplyColumnCustomizations</c> that v3.12.0 split into four calls. Anyone who took the
        ///     template from master in that window gets the four calls, in the order the split made them.
        /// </summary>
        private string SplitApplyColumnCustomizations(string text)
        {
            var pattern = new Regex(
                @"^(?<indent>[ \t]*)Settings\.ApplyColumnCustomizations\s*\(\s*(?<column>\w+)\s*,\s*(?<table>\w+)\s*,\s*(?<enums>\w+)\s*,\s*(?<json>\w+)\s*\)\s*;",
                RegexOptions.Multiline);
            var match = pattern.Match(text);

            if (!match.Success)
                return text;

            var indent = match.Groups["indent"].Value;
            var column = match.Groups["column"].Value;
            var table  = match.Groups["table"].Value;
            var enums  = match.Groups["enums"].Value;
            var json   = match.Groups["json"].Value;
            var newLine = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";

            var after = indent + "Settings.ApplyJsonPropertyNameAttribute(" + column + ");" + newLine +
                        indent + "Settings.ApplyJsonColumnMappings(" + column + ", " + table + ", " + json + ");" + newLine +
                        indent + "Settings.ApplyDataAnnotations(" + column + ");" + newLine +
                        indent + "Settings.ApplyEnumTypeReplacement(" + column + ", " + table + ", " + enums + ");";

            Record("Settings.ApplyColumnCustomizations was split into four calls before v3.12.0 and does not exist in v4.",
                match.Value.Trim(), after.Trim());

            return text.Substring(0, match.Index) + after + text.Substring(match.Index + match.Length);
        }

        private string RemoveSqlCeComparison(string text)
        {
            var match = SqlCeComparison.Match(text);

            if (!match.Success)
                return text;

            Record("DatabaseType.SqlCe no longer exists in v4, so the comparison is always false.", match.Value, "false");

            return SqlCeComparison.Replace(text, "false");
        }

        /// <summary>
        ///     Replaces the whole tail of the file, because in v3 <c>fileManagement</c> is created after the
        ///     commented-out machine.config lines and in v4 it moves above the try block.
        /// </summary>
        private string ReplaceEntryPoint(string text)
        {
            var match = EntryPoint.Match(text);

            if (!match.Success)
            {
                _blockers.Add("The entry point block could not be found. It normally starts with " +
                              "'var outer = (GeneratedTextTransformation) this;' near the end of the file.");
                return text;
            }

            var tail = text.Substring(match.Index);

            if (!IsRecognisedV3EntryPoint(tail))
            {
                _blockers.Add("The entry point block has been changed from the standard v3 one, so it cannot be " +
                              "replaced safely. Upgrade this file by hand using the v3 to v4 guide.");
                return text;
            }

            // The replacement is written with CRLF; a file that uses bare LF keeps it, because a whole-file line
            // ending change would show up as every line differing in the user's next commit.
            var replacement = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0
                ? V4EntryPoint
                : V4EntryPoint.Replace("\r\n", "\n");

            Record("Replace the entry point with the version that calls the efrpg tool.", tail, replacement);

            return text.Substring(0, match.Index) + replacement;
        }

        /// <summary>
        ///     Compares the code only. Comments are dropped and every whitespace character with them, so a file
        ///     carrying its own notes, tabs or odd indentation inside the block still upgrades, while one that has
        ///     been genuinely restructured does not.
        /// </summary>
        private static bool IsRecognisedV3EntryPoint(string tail)
        {
            var code = tail
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal));

            return V3EntryPointShapes.Contains(NormaliseCode(string.Join(" ", code)), StringComparer.Ordinal);
        }

        /// <summary>
        ///     Removes every whitespace character, so two spellings of the same statements compare equal whatever
        ///     the indentation, brace placement or line endings.
        /// </summary>
        private static string NormaliseCode(string code)
        {
            return Regex.Replace(code, @"\s+", string.Empty);
        }

        private void Record(string description, string before, string after)
        {
            _changes.Add(new TemplateUpgradeChange(description, before, after));
        }
    }
}
