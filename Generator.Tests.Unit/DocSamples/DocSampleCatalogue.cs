using System;
using System.Collections.Generic;
using System.Linq;
using Efrpg;
using Efrpg.Filtering;

namespace Generator.Tests.Unit.DocSamples
{
    /// <summary>
    ///     Every code snippet that appears on a Settings.* wiki page, keyed by the name the page cites.
    /// </summary>
    /// <remarks>
    ///     A wiki page marks each generated block with <c>&lt;!-- docsample: Key --&gt;</c> immediately above the
    ///     fence. <see cref="WikiSnippetDriftTests"/> looks the key up here, regenerates it, and fails if the page
    ///     has fallen behind. So a snippet on the wiki is never hand-maintained: change the templates, run the
    ///     tests, paste the new output.
    ///     Adding a sample: add an entry here, run <c>DocSampleTests.Write_all_samples_to_disk</c>, and paste the
    ///     file it writes into the page under the matching marker.
    /// </remarks>
    public static class DocSampleCatalogue
    {
        public static IEnumerable<string> Keys
        {
            get { return Samples().Keys.OrderBy(x => x, StringComparer.Ordinal); }
        }

        public static string Get(string key)
        {
            Func<string> sample;
            if (!Samples().TryGetValue(key, out sample))
                throw new KeyNotFoundException(string.Format(
                    "No doc sample named '{0}'. Either the wiki cites a sample that was renamed or deleted, or a new marker was added to a page without adding the sample here.", key));

            return sample();
        }

        private static Dictionary<string, Func<string>> Samples()
        {
            return new Dictionary<string, Func<string>>(StringComparer.Ordinal)
            {
                // ---- Settings.UsePrivateSetterForComputedColumns -------------------------------------------
                {
                    "UsePrivateSetterForComputedColumns/true", () => Product(
                        () => Settings.UsePrivateSetterForComputedColumns = true)
                },
                {
                    "UsePrivateSetterForComputedColumns/false", () => Product(
                        () => Settings.UsePrivateSetterForComputedColumns = false)
                },

                // ---- Settings.ElementsToGenerate ------------------------------------------------------------
                {
                    "ElementsToGenerate/all", () => Outline(Default())
                },
                {
                    "ElementsToGenerate/poco-only", () => Outline(
                        DocSampleRunner.Generate(() => Settings.ElementsToGenerate = Elements.Poco))
                },
                {
                    "ElementsToGenerate/context-and-interface", () => Outline(
                        DocSampleRunner.Generate(() => Settings.ElementsToGenerate = Elements.Context | Elements.Interface))
                },
                {
                    "ElementsToGenerate/poco-and-configuration", () => Outline(
                        DocSampleRunner.Generate(() => Settings.ElementsToGenerate = Elements.Poco | Elements.PocoConfiguration))
                },

                // ---- Settings.UpdateColumn -----------------------------------------------------------------
                {
                    "UpdateColumn/before", () => Product(() => { })
                },
                {
                    "UpdateColumn/after", () => Product(() =>
                        Settings.UpdateColumn = (column, table, enumDefinitions, jsonColumnMappings) =>
                        {
                            // Rename a primary key called <Table>Id to just Id
                            if (column.IsPrimaryKey && column.NameHumanCase == table.NameHumanCase + "Id")
                                column.NameHumanCase = "Id";

                            // Keep an internal column out of the model entirely
                            if (column.NameHumanCase == "Notes")
                                column.Hidden = true;

                            Settings.ApplyDataAnnotations(column);
                        })
                },

                // ---- Settings.PrependSchemaNameForTable ----------------------------------------------------
                {
                    "PrependSchemaNameForTable/default", () => Outline(Default())
                },
                {
                    "PrependSchemaNameForTable/sales-excluded", () => Outline(
                        DocSampleRunner.Generate(() => Settings.PrependSchemaNameForTable = table =>
                            !table.Schema.DbName.Equals("sales", StringComparison.OrdinalIgnoreCase)))
                },

                // ---- Settings.OnConfiguration --------------------------------------------------------------
                {
                    "OnConfiguration/ConnectionString", () => DocSampleExtractor.Section(
                        Default(), "protected override void OnConfiguring")
                },
                {
                    // What switching to Configuration adds and changes, rather than the whole DbContext
                    "OnConfiguration/Configuration-diff", () => DocSampleExtractor.ChangedRegion(
                        DocSampleRunner.Generate(() => Settings.OnConfiguration = OnConfiguration.Configuration),
                        Default(),
                        context: 1)
                },
                {
                    // What switching to Omit takes away
                    "OnConfiguration/Omit-removes", () => DocSampleExtractor.ChangedRegion(
                        Default(),
                        DocSampleRunner.Generate(() => Settings.OnConfiguration = OnConfiguration.Omit),
                        context: 0)
                },

                // ---- POCO shape --------------------------------------------------------------------------
                { "UsePascalCase/true",  () => Extras(() => Settings.UsePascalCase = true,  "class OrderLineItem") },
                { "UsePascalCase/false", () => Extras(() => Settings.UsePascalCase = false, "class order_line_item") },

                { "UseDataAnnotations/false", () => Product(() => Settings.UseDataAnnotations = false) },
                { "UseDataAnnotations/true",  () => Product(() => Settings.UseDataAnnotations = true) },

                { "UsePropertyInitialisers/false", () => Product(() => Settings.UsePropertyInitialisers = false) },
                { "UsePropertyInitialisers/true",  () => Product(() => Settings.UsePropertyInitialisers = true) },

                { "UseLazyLoading/false", () => Product(() => Settings.UseLazyLoading = false) },
                { "UseLazyLoading/true",  () => Product(() => Settings.UseLazyLoading = true) },

                { "NullableShortHand/true",  () => Extras(() => Settings.NullableShortHand = true,  "public class Document") },
                { "NullableShortHand/false", () => Extras(() => Settings.NullableShortHand = false, "public class Document") },

                { "AllowNullStrings/false", () => Product(() => Settings.AllowNullStrings = false) },
                { "AllowNullStrings/true",  () => Product(() => Settings.AllowNullStrings = true) },

                { "IncludeFieldNameConstants/false", () => Category(() => Settings.IncludeFieldNameConstants = false) },
                { "IncludeFieldNameConstants/true",  () => Category(() => Settings.IncludeFieldNameConstants = true) },

                { "IncludeColumnsWithDefaults/true",  () => Product(() => Settings.IncludeColumnsWithDefaults = true) },
                { "IncludeColumnsWithDefaults/false", () => Product(() => Settings.IncludeColumnsWithDefaults = false) },

                { "OrderProperties/Ordinal",      () => Product(() => Settings.OrderProperties = OrderProperties.Ordinal) },
                { "OrderProperties/Alphabetical", () => Product(() => Settings.OrderProperties = OrderProperties.Alphabetical) },

                { "TableSuffix/none", () => Outline(Default()) },
                { "TableSuffix/Dto",  () => Outline(DocSampleRunner.Generate(() => Settings.TableSuffix = "Dto")) },

                { "IncludeComments/AtEndOfField",   () => Category(() => Settings.IncludeComments = CommentsStyle.AtEndOfField) },
                { "IncludeComments/InSummaryBlock", () => Category(() => Settings.IncludeComments = CommentsStyle.InSummaryBlock) },
                { "IncludeComments/None",           () => Category(() => Settings.IncludeComments = CommentsStyle.None) },

                { "CollectionType/List",                 () => Category(() => { Settings.CollectionType = "List"; Settings.CollectionInterfaceType = "ICollection"; }) },
                { "CollectionType/ObservableCollection", () => Category(() => { Settings.CollectionType = "ObservableCollection"; Settings.CollectionInterfaceType = "IList"; }) },

                // ---- Class and file furniture ------------------------------------------------------------
                { "EntityClassesModifiers/public",         () => Line(Default(), "public class Category") },
                { "EntityClassesModifiers/public-partial", () => Line(DocSampleRunner.Generate(() => Settings.EntityClassesModifiers = "public partial"), "class Category") },

                { "ConfigurationClassName/Configuration", () => Outline(Default()) },
                { "ConfigurationClassName/Map",           () => Outline(DocSampleRunner.Generate(() => Settings.ConfigurationClassName = "Map")) },

                { "DbContextName/MyDbContext",   () => Outline(Default()) },
                { "DbContextName/NorthwindData", () => Outline(DocSampleRunner.Generate(() => Settings.DbContextName = "NorthwindData")) },

                { "DbContextInterfaceName/custom",  () => Outline(DocSampleRunner.Generate(() => Settings.DbContextInterfaceName = "INorthwind")) },

                { "AddIDbContextFactory/true",  () => Outline(Default()) },
                { "AddIDbContextFactory/false", () => Outline(DocSampleRunner.Generate(() => Settings.AddIDbContextFactory = false)) },

                { "AddUnitTestingDbContext/false", () => Outline(Default()) },
                { "AddUnitTestingDbContext/true",  () => Outline(DocSampleRunner.Generate(() => Settings.AddUnitTestingDbContext = true)) },

                { "DbContextBaseClass/DbContext", () => Line(Default(), "public class MyDbContext") },
                { "DbContextBaseClass/Identity",  () => Line(DocSampleRunner.Generate(() => Settings.DbContextBaseClass = "IdentityDbContext<ApplicationUser>"), "class MyDbContext") },

                { "DbContextInterfaceBaseClasses/IDisposable", () => Line(Default(), "public interface IMyDbContext") },
                { "DbContextInterfaceBaseClasses/custom",      () => Line(DocSampleRunner.Generate(() => Settings.DbContextInterfaceBaseClasses = "IDisposable, IUnitOfWork"), "public interface IMyDbContext") },

                { "UseRegions/true",  () => Regions(Default()) },
                { "UseRegions/false", () => Regions(DocSampleRunner.Generate(() => Settings.UseRegions = false)) },

                { "UseFileScopedNamespaces/false", () => Line(Default(), "namespace MyApp.Data") },
                { "UseFileScopedNamespaces/true",  () => Line(DocSampleRunner.Generate(() => Settings.UseFileScopedNamespaces = true), "namespace MyApp.Data") },

                { "UsePragma/true",    () => Head(DocSampleRunner.Generate(() => Settings.UsePragma = true), 3) },
                { "UseResharper/true", () => Head(DocSampleRunner.Generate(() => Settings.UseResharper = true), 8) },

                { "AdditionalFileHeaderText/set", () => Head(DocSampleRunner.Generate(() =>
                    Settings.AdditionalFileHeaderText = new List<string> { "// Owned by the Platform team", "// Do not edit by hand" }), 4) },

                { "IncludeCodeGeneratedAttribute/false", () => Category(() => Settings.IncludeCodeGeneratedAttribute = false) },
                { "IncludeCodeGeneratedAttribute/true",  () => Category(() => Settings.IncludeCodeGeneratedAttribute = true) },

                // ---- Configuration mapping ---------------------------------------------------------------
                { "GenerateHasDefaultValueSql/false", () => ProductConfiguration(() => Settings.GenerateHasDefaultValueSql = false) },
                { "GenerateHasDefaultValueSql/true",  () => ProductConfiguration(() => Settings.GenerateHasDefaultValueSql = true) },

                { "PrependSchemaName/true",  () => Outline(Default()) },
                { "PrependSchemaName/false", () => Outline(DocSampleRunner.Generate(() => Settings.PrependSchemaName = false)) },

                // ---- Output layout -----------------------------------------------------------------------
                { "GenerateSeparateFiles/true", () => DocSampleRunner.GenerateFileListing(() => { }) },

                { "PocoFolder/foldered", () => DocSampleRunner.GenerateFileListing(() =>
                    {
                        Settings.ContextFolder           = "Data";
                        Settings.InterfaceFolder         = "Data/Interface";
                        Settings.PocoFolder              = "Data/Entities";
                        Settings.PocoConfigurationFolder = "Data/Configuration";
                    }) },

                { "EnumsFileName/empty", () => DocSampleRunner.GenerateFileListing(() => { }, DocSampleRunner.Enums()) },
                { "EnumsFileName/Enums", () => DocSampleRunner.GenerateFileListing(() => Settings.EnumsFileName = "Enums", DocSampleRunner.Enums()) },
                { "EnumsFileName/file",  () =>
                    DocSampleRunner.GenerateSeparateFile(() => Settings.EnumsFileName = "Enums", DocSampleRunner.Enums(), "Enums.cs") },

                // ---- Callbacks ---------------------------------------------------------------------------
                { "TableRename/before", () => Outline(Default()) },
                { "TableRename/after",  () => Outline(DocSampleRunner.Generate(() =>
                    Settings.TableRename = (name, schema, isView) => name == "Product" ? "CatalogueItem" : name)) },

                { "UpdateTable/before", () => Line(Default(), "public class Product") },
                { "UpdateTable/after",  () => Section(DocSampleRunner.Generate(() =>
                    Settings.UpdateTable = table =>
                    {
                        if (table.NameHumanCase == "Product")
                        {
                            table.BaseClasses = " : IAuditable";
                            table.Attributes.Add("[Serializable]");
                        }
                    }), "class Product") },

                { "WriteInsideClassBody/after", () => Section(DocSampleRunner.Generate(() =>
                    Settings.WriteInsideClassBody = t => t.NameHumanCase != "Category" ? string.Empty :
                        "        public override string ToString()" + Environment.NewLine +
                        "        {" + Environment.NewLine +
                        "            return CategoryName;" + Environment.NewLine +
                        "        }" + Environment.NewLine), "public class Category") },

                { "ForeignKeyName/before", () => Product(() => { }) },
                { "ForeignKeyName/after",  () => Product(() =>
                    Settings.ForeignKeyName = (tableName, fk, fkName, relationship, attempt) =>
                        fk.FkColumn == "CategoryId" && attempt == 1 ? "Group" : tableName) },

                { "ForeignKeyFilterFunc/before", () => Category(() => { }) },
                { "ForeignKeyFilterFunc/after",  () => Category(() =>
                    Settings.ForeignKeyFilterFunc = fk => { fk.IncludeReverseNavigation = false; return fk; }) },

                { "ForeignKeyAnnotationsProcessing/after", () => Product(() =>
                    Settings.ForeignKeyAnnotationsProcessing = (fkTable, pkTable, propName, fkPropName) =>
                        new[] { "System.Text.Json.Serialization.JsonIgnore" }) },

                { "AdditionalReverseNavigationsDataAnnotations/after", () => Category(() =>
                    Settings.AdditionalReverseNavigationsDataAnnotations = new[] { "JsonIgnore" }) },

                // ---- Extras schema -----------------------------------------------------------------------
                { "MappingTables/outline", () => Outline(DocSampleRunner.Generate(DocSampleRunner.Schema.Extras, () => { })) },

                { "IncludeViews/true",  () => Outline(DocSampleRunner.Generate(DocSampleRunner.Schema.Extras, () => FilterSettings.IncludeViews = true)) },
                { "IncludeViews/false", () => Outline(DocSampleRunner.Generate(DocSampleRunner.Schema.Extras, () => FilterSettings.IncludeViews = false)) },

                { "IncludeExtendedPropertyComments/InSummaryBlock", () => Extras(() => Settings.IncludeExtendedPropertyComments = CommentsStyle.InSummaryBlock, "public class Document") },
                { "IncludeExtendedPropertyComments/None",           () => Extras(() => Settings.IncludeExtendedPropertyComments = CommentsStyle.None, "public class Document") },

                { "PrependSchemaNameForStoredProcedure/default",        () => StoredProcSignatures(() => { }) },
                { "PrependSchemaNameForStoredProcedure/sales-excluded", () => StoredProcSignatures(() =>
                    Settings.PrependSchemaNameForStoredProcedure = sp => !sp.Schema.DbName.Equals("sales", StringComparison.OrdinalIgnoreCase)) },

                { "StoredProcedureRename/before", () => StoredProcSignatures(() => { }) },
                { "StoredProcedureRename/after",  () => StoredProcSignatures(() =>
                    Settings.StoredProcedureRename = sp => sp.NameHumanCase.StartsWith("Get") ? sp.NameHumanCase.Substring(3) : sp.NameHumanCase) },

                { "StoredProcedureReturnModelRename/before", () => StoredProcSignatures(() => { }) },
                { "StoredProcedureReturnModelRename/after",  () => StoredProcSignatures(() =>
                    Settings.StoredProcedureReturnModelRename = (name, sp) => sp.NameHumanCase == "GetStudentsByCourse" ? "StudentSummary" : name) },

                { "UsePropertiesForStoredProcResultSets/false", () => Extras(() => Settings.UsePropertiesForStoredProcResultSets = false, "class GetCourseReportReturnModel") },
                { "UsePropertiesForStoredProcResultSets/true",  () => Extras(() => Settings.UsePropertiesForStoredProcResultSets = true,  "class GetCourseReportReturnModel") },

                { "RowVersion/document", () => Extras(() => { }, "public class Document") },

                // ---- Compared-with-the-Microsoft-scaffolder -------------------------------------------------
                { "Comparison/stored-procedure-model",  () => Dedent(Extras(() => FilterSettings.IncludeStoredProcedures = true, "class GetStudentsByCourseReturnModel")) },
                { "Comparison/interface",               () => InterfaceOutline() },
                { "Comparison/fake-context",            () => FakeContextHead() },
            };
        }

        /// <summary>
        ///     Generation with nothing changed from the shipped Database.tt defaults.
        /// </summary>
        private static string Default()
        {
            return DocSampleRunner.Generate(() => { });
        }

        /// <summary>
        ///     The generated Product entity class, which is where most column-level settings show their effect.
        /// </summary>
        private static string Product(Action configure)
        {
            return DocSampleExtractor.Section(DocSampleRunner.Generate(configure), "public class Product");
        }

        /// <summary>
        ///     The generated Category entity class - smaller than Product, for settings whose effect shows on
        ///     any class at all.
        /// </summary>
        private static string Category(Action configure)
        {
            return Section(DocSampleRunner.Generate(configure), "class Category");
        }

        /// <summary>
        ///     The generated Product entity configuration, for settings that change the Fluent API mapping
        ///     rather than the property declaration.
        /// </summary>
        private static string ProductConfiguration(Action configure)
        {
            return Section(DocSampleRunner.Generate(configure), "class ProductConfiguration");
        }

        /// <summary>
        ///     A named block generated from the Extras schema.
        /// </summary>
        private static string Extras(Action configure, string marker)
        {
            return Section(DocSampleRunner.Generate(DocSampleRunner.Schema.Extras, configure), marker);
        }

        private static string Section(string generated, string marker)
        {
            return DocSampleExtractor.Section(generated, marker);
        }

        /// <summary>
        ///     A single line, for settings whose whole effect is one declaration.
        /// </summary>
        private static string Line(string generated, string marker)
        {
            foreach (var line in generated.Replace("\r\n", "\n").Split('\n'))
                if (line.Contains(marker))
                    return line.Trim();

            throw new ArgumentException(string.Format("No line in the generated output contains '{0}'.", marker));
        }

        /// <summary>
        ///     The first n non-blank lines, for settings that only change the top of the file.
        /// </summary>
        private static string Head(string generated, int count)
        {
            return string.Join("\n", generated
                .Replace("\r\n", "\n")
                .Split('\n')
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Take(count));
        }

        /// <summary>
        ///     Just the #region and #endregion directives, which is the whole of what Settings.UseRegions does.
        /// </summary>
        private static string Regions(string generated)
        {
            var regions = generated
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.StartsWith("#region") || l.StartsWith("#endregion"))
                .ToList();

            return regions.Count == 0 ? "// No #region directives are written." : string.Join("\n", regions);
        }

        /// <summary>
        ///     The generated stored procedure caller signatures, minus the async and out-parameter overloads,
        ///     which triple the length without saying anything extra.
        /// </summary>
        private static string StoredProcSignatures(Action configure)
        {
            var generated = DocSampleRunner.Generate(DocSampleRunner.Schema.Extras, () =>
            {
                FilterSettings.IncludeStoredProcedures = true;
                if (configure != null) configure();
            });

            var lines = generated
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(l => l.Trim())
                // A caller signature, not its async or out-parameter overloads, which triple the length
                // without saying anything extra. Deliberately does not match on "ReturnModel": the rename
                // samples change exactly that word, and filtering on it hid the renamed line.
                .Where(l => l.StartsWith("List<") && l.EndsWith(");")
                            && !l.Contains("Async") && !l.Contains("out int"))
                .Distinct()
                .ToList();

            return lines.Count == 0 ? "// No stored procedure callers were generated." : string.Join("\n", lines);
        }

        /// <summary>
        ///     Strips the indentation a nested type carries inside its namespace, for a page that shows a class on
        ///     its own rather than in the context of the file.
        /// </summary>
        private static string Dedent(string block)
        {
            var lines = Lines(block);
            var indent = lines.Where(l => l.Trim().Length > 0).Min(l => l.Length - l.TrimStart().Length);
            return string.Join("\n", lines.Select(l => l.Length >= indent ? l.Substring(indent) : l.TrimStart()));
        }

        /// <summary>
        ///     The context interface with the forty DbContext members elided, leaving the DbSets and the stored
        ///     procedure callers, which are the parts that show what the interface is for.
        /// </summary>
        private static string InterfaceOutline()
        {
            var lines = Lines(Extras(() => FilterSettings.IncludeStoredProcedures = true, "public interface IMyDbContext"));
            var dbSetsEnd = lines.FindIndex(string.IsNullOrWhiteSpace);
            var procs = lines.FindIndex(l => l.Contains("// Stored Procedures"));

            return string.Join("\n", lines.Take(dbSetsEnd)
                .Concat(new[] { "", "        // ... SaveChanges, Set<TEntity>, Add, Attach, Entry, Find, Remove and Update, as on DbContext", "" })
                .Concat(lines.Skip(procs)));
        }

        /// <summary>
        ///     The top of the fake context: its DbSets and the constructor that wires them up. The rest is a
        ///     stub for every DbContext member and a fake caller per stored procedure, which the page describes.
        /// </summary>
        private static string FakeContextHead()
        {
            var lines = Lines(Extras(() =>
            {
                FilterSettings.IncludeStoredProcedures = true;
                Settings.AddUnitTestingDbContext = true;
            }, "public class FakeMyDbContext"));

            var head = lines.Take(lines.FindIndex(l => l.Contains("public virtual int SaveChanges(bool"))).ToList();
            while (string.IsNullOrWhiteSpace(head[head.Count - 1]))
                head.RemoveAt(head.Count - 1);

            head.Add("");
            head.Add("        // ... every other DbContext member, and a fake caller for each stored procedure");
            head.Add("    }");
            return string.Join("\n", head);
        }

        private static List<string> Lines(string text)
        {
            return text.Replace("\r\n", "\n").Split('\n').ToList();
        }

        /// <summary>
        ///     Reduces a generated file to its type declarations, for samples about which types get generated
        ///     rather than what is inside them.
        /// </summary>
        internal static string Outline(string generated)
        {
            var declarations = generated
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => line.Trim())
                .Where(IsTypeDeclaration)
                .ToList();

            return declarations.Count == 0
                ? "// Nothing is generated."
                : string.Join("\n", declarations);
        }

        private static bool IsTypeDeclaration(string line)
        {
            if (line.StartsWith("//") || line.Contains("("))
                return false;

            return (line.Contains(" class ") || line.Contains(" interface ") || line.Contains(" enum "))
                && (line.StartsWith("public") || line.StartsWith("internal"));
        }
    }
}
