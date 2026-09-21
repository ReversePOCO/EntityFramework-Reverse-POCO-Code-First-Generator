using Efrpg.Filtering;
using Efrpg.Generators;
using Efrpg.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Efrpg
{
    public static class Settings
    {
        // Main settings **********************************************************************************************************************
        // The following entries are the only required settings.
        public static DatabaseType DatabaseType = DatabaseType.SqlServer; // SqlServer, SQLite, PostgreSQL, MySql, Oracle
        public static TemplateType TemplateType = TemplateType.EfCore10; // Ef6, EfCore8-10
        public static bool UseMappingTables = false; // Can only be set to true for EF6. If true, mapping will be used and no mapping tables will be generated. If false, all tables will be generated.
        public static string ConnectionString = ""; // This is used by the generator to reverse engineer your database
        public static string ConnectionStringActions = ""; // EFCore only. Additional method chain to append to the database provider setup in OnConfiguring. e.g. ".EnableRetryOnFailure(maxRetryCount: 10, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null)"
        public static string ConnectionStringName = "MyDbContext"; // ConnectionString key as specified in your app.config/web.config/appsettings.json
        public static string DbContextName = "MyDbContext"; // Class name for the DbContext to be generated.
        public static bool GenerateSeparateFiles = false;
        public static string Namespace = typeof(Settings).Namespace; // Override the default namespace here. Example: Namespace = "CustomNamespace";
        public static bool AddUnitTestingDbContext = true; // Will add a FakeDbContext and FakeDbSet for easy unit testing
        public static bool FakeDbContextInDebugOnlyMode = false; // If true, wraps Fake* classes in #if DEBUG / #endif so they are excluded from Release builds

        // Elements to generate ***************************************************************************************************************
        // Add the elements that should be generated when the template is executed.
        // Multiple projects can now be used that separate the different concerns.
        public static Elements ElementsToGenerate = Elements.Poco | Elements.Context | Elements.Interface | Elements.PocoConfiguration | Elements.Enum; // Combine with |. None, Poco, Context, Interface, PocoConfiguration, Enum

        // Generate files in sub-folders ******************************************************************************************************
        // Only activated if Settings.GenerateSeparateFiles = true
        public static string ContextFolder = ""; // Sub-folder you would like your DbContext to be added to.              e.g. @"Data"
        public static string InterfaceFolder = ""; // Sub-folder you would like your Interface to be added to.              e.g. @"Data\Interface"
        public static string PocoFolder = ""; // Sub-folder you would like your Poco's to be added to.                 e.g. @"Data\Entities"
        public static string PocoConfigurationFolder = ""; // Sub-folder you would like your Configuration mappings to be added to. e.g. @"Data\Configuration"
        public static string OwnedEntityFolder = ""; // Sub-folder for auto-generated owned entity classes; defaults to PocoFolder if empty.   e.g. @"Data\Entities"
        public static bool UseFolderNameInNamespace = false; // If true, appends the sub-folder name to the namespace of each generated file. e.g. PocoFolder = "Entities" => namespace MyProject.Entities


        public static int CommandTimeout = 600; // SQL Command timeout in seconds. 600 is 10 minutes, 0 will wait indefinitely. Some databases can be slow retrieving schema information.
        public static string DbContextInterfaceBaseClasses = "IDisposable"; // Specify what the base classes are for your database context interface
        public static string DbContextBaseClass = "DbContext"; // Specify what the base class is for your DbContext. For ASP.NET Identity use "IdentityDbContext<ApplicationUser>";
        public static OnConfiguration OnConfiguration = OnConfiguration.ConnectionString; // Configuration, ConnectionString, Omit. EFCore only. Determines the code generated within DbContext.OnConfiguration(). Please read https://github.com/ReversePOCO/EntityFramework-Reverse-POCO-Code-First-Generator/wiki/Settings.OnConfiguration
        public static bool AddParameterlessConstructorToDbContext = true; // If true, then DbContext will have a default (parameter-less) constructor which automatically passes in the connection string name, if false then no parameter-less constructor will be created.
        public static string ConfigurationClassName = "Configuration"; // Configuration, Mapping, Map, etc. This is appended to the Poco class name to configure the mappings.
        public static string EntityClassesModifiers = "public"; // "public partial";
        public static string ConfigurationClassesModifiers = "public"; // "public partial";
        public static string DbContextClassModifiers = "public"; // "public partial";
        public static string DbContextInterfaceModifiers = "public"; // "public partial";
        public static string ResultClassModifiers = "public"; // "public partial";

        public static bool UsePascalCase = true; // This will rename the generated C# tables & properties to use PascalCase. If false table & property names will be left alone.
        public static bool UsePascalCaseForEnumMembers = true; // This will rename the generated Enum Members to use PascalCase. If false Enum members will be left alone.
        public static bool UseDataAnnotations = false; // If true, will add data annotations to the poco classes.
        public static bool UsePropertyInitialisers = false; // Removes POCO constructor and instead uses C# 6 property initialisers to set defaults
        public static bool UseLazyLoading = true; // Marks all navigation properties as virtual or not, to support or disable EF Lazy Loading feature
        public static bool UseInheritedBaseInterfaceFunctions = false; // If true, the main DBContext interface functions will come from the DBContextInterfaceBaseClasses and not generated. If false, the functions will be generated.
        public static CommentsStyle IncludeComments = CommentsStyle.AtEndOfField; // None, InSummaryBlock, AtEndOfField. Adds comments to the generated code
        public static CommentsStyle IncludeExtendedPropertyComments = CommentsStyle.InSummaryBlock; // None, InSummaryBlock, AtEndOfField. Adds extended properties as comments to the generated code
        public static bool DisableGeographyTypes = false; // Turns off use of System.Data.Entity.Spatial.DbGeography and System.Data.Entity.Spatial.DbGeometry as OData doesn't support entities with geometry/geography types.
        public static string CollectionInterfaceType = "ICollection"; //  = "System.Collections.Generic.List"; // Determines the declaration type of collections for the Navigation Properties. ICollection is used if not set.
        public static string CollectionType = "List"; // Determines the type of collection for the Navigation Properties. "ObservableCollection" for example. Add "System.Collections.ObjectModel" to AdditionalNamespaces if setting the CollectionType = "ObservableCollection".
        public static bool NullableShortHand = true; // true => T?, false => Nullable<T>
        public static bool AddIDbContextFactory = true; // Will add a default IDbContextFactory<DbContextName> implementation for easy dependency injection
        public static bool UsePrivateSetterForComputedColumns = true; // If the columns is computed, use a private setter.
        public static bool IncludeGeneratorVersionInCode = false; // If true, will include the version number of the generator in the generated code (Settings.ShowLicenseInfo must also be true).
        public static bool TrimCharFields = false; // EF Core option only. If true, will TrimEnd() 'char' fields when read from the database.
        public static bool IncludeFieldNameConstants = false; // Will include public const string {{NameHumanCase}}Field = "{{NameHumanCase}}"; in the generated POCO class. It allows you to use a constant instead of magic strings.
        public static bool UsePropertiesForStoredProcResultSets = false; // Stored procedure result set return models are rendered as fields (false) or properties (true).
        public static bool MergeMultipleStoredProcModelsIfAllSame = true; // Some stored procedures are reported as having multiple result sets when in fact there is only one. Set this to true to merge identical result sets.
        public static List<string> AdditionalNamespaces = new List<string>(); // To include extra namespaces, include them here. i.e. "Microsoft.AspNet.Identity.EntityFramework"
        public static List<string> AdditionalContextInterfaceItems = new List<string>(); //  example: "void SetAutoDetectChangesEnabled(bool flag);"
        public static List<string> AdditionalFileHeaderText = new List<string>(); // This will put additional lines verbatim at the top of each file under the comments, 1 line per entry
        public static List<string> AdditionalFileFooterText = new List<string>(); // This will put additional lines verbatim at the end of each file above the // </auto-generated>, 1 line per entry
        public static OrderProperties OrderProperties = OrderProperties.Ordinal; // Order the properties in the generated POCO classes. Ordinal, Alphabetical

        // Language choices

        // Code suppression *******************************************************************************
        public static bool UseRegions = true;  // If false, suppresses the use of #region
        public static bool UseNamespace = true;  // If false, suppresses the writing of a namespace
        public static bool UseFileScopedNamespaces = false; // If true, uses C# 10 file-scoped namespace syntax (namespace X;) instead of block-scoped (namespace X { })
        public static bool UsePragma = false; // If false, suppresses the writing of #pragma
        public static bool AllowNullStrings = false; // If true, will allow string? properties and will add '#nullable enable' to the top of each file
        public static bool NullableReverseNavigationProperties = false; // If true, reverse navigation properties for one-to-one relationships will be nullable (e.g. MyEntity? MyEntity). The parent entity can exist without the child entity.
        public static bool UseResharper = false; // If true, will add a list of 'ReSharper disable' comments to the top of each file
        public static bool ShowLicenseInfo = false; // If true, will add the licence info comment to the top of each file
        public static bool IncludeConnectionSettingComments = false; // Add comments describing connection settings used to generate file
        public static bool IncludeCodeGeneratedAttribute = false; // If true, will include the [GeneratedCode] attribute before classes, false to remove it.
        public static bool IncludeColumnsWithDefaults = true;  // If true, will set properties to the default value from the database.
        public static bool GenerateHasDefaultValueSql = false; // EFCore only. If true, will emit .HasDefaultValueSql() in the entity configuration for all columns that have a SQL default, making defaults queryable via EF model reflection.

        // Create enumerations from database tables
        // List the enumeration tables you want read and generated for
        // Also look at the AddEnum callback below to add your own during reverse generation of tables.
        public static List<EnumerationSettings> Enumerations = new List<EnumerationSettings>
        {
            // Example
            /*new EnumerationSettings
            {
                Name                        = "DaysOfWeek",          // Enum to generate. e.g. "DaysOfWeek" would result in "public enum DaysOfWeek {...}" if the GroupField is set to a value then {GroupField} must be used in this name. e.g. "DaysOfWeek{GroupField}"
                Table                       = "EnumTest.DaysOfWeek", // Database table containing enum values. e.g. "DaysOfWeek"
                NameField                   = "TypeName",            // Column containing the name for the enum. e.g. "TypeName"
                ValueField                  = "TypeId",              // Column containing the values for the enum. e.g. "TypeId"
                GroupField                  = string.Empty,          // [optional] Column containing the group name for the enum. This is used if multiple Enums are in the same table. if this is populated, use {GroupField} in the Name property. e.g. "{GroupField}Enum"
                DescriptionField            = "Description",         // [optional] Column containing the description for each enum member. When set, emits [Description("...")] on members with a non-empty value.
                GenerateDescriptionFromName = false                  // [optional] If true and no description is available from DescriptionField, generates [Description("...")] from the enum member name as human-readable text.
            },
            new EnumerationSettings
            {
                Name       = "CarOptions",
                Table      = "car_options",
                NameField  = "enum_name",
                ValueField = "value"
            }*/
            // Code will be generated as:
            // public enum Name
            // {
            //     NameField = ValueField,
            //     etc
            // }
        };

        // Use the following list to add use of HiLo sequences for identity columns
        public static List<HiLoSequence> HiLoSequences = new List<HiLoSequence>
        {
            // Example
            // For column dbo.match_table_name.match_column_name this will use .UseHiLo("sequence_name", "sequence_schema") instead of .UseSqlServerIdentityColumn()
            /*new HiLoSequence
            {
                Schema         = "dbo",
                Table          = "match_table_name",
                Column         = "match_column_name",
                SequenceName   = "sequence_name",
                SequenceSchema = "sequence_schema"
            },
            new HiLoSequence
            {
                Schema         = "dbo",
                Table          = "Employees",
                Column         = "EmployeeID",
                SequenceName   = "EmployeeSequence",
                SequenceSchema = "dbo"
            }*/
        };

        // If you need to serialise your entities with the JsonSerializer from Newtonsoft, this would serialise
        // all properties including the Reverse Navigation and Foreign Keys. The simplest way to exclude them is
        // to use the data annotation [JsonIgnore] on reverse navigation and foreign keys.
        // For more control, take a look at ForeignKeyAnnotationsProcessing() further down
        public static string[] AdditionalReverseNavigationsDataAnnotations = new string[]
        {
            // "JsonIgnore" // Also add "Newtonsoft.Json" to the AdditionalNamespaces array above
        };
        public static string[] AdditionalForeignKeysDataAnnotations = new string[]
        {
            // "JsonIgnore" // Also add "Newtonsoft.Json" to the AdditionalNamespaces array above
        };


        // Reference other namespaces *********************************************************************************************************
        // Use these namespaces to specify where the different elements now live. These may even be in different assemblies.
        // NOTE: These are only used if ElementsToGenerate is not set to generate all the elements.
        // Please note the following does not create the files in these locations, it only adds a using statement to say where they are.
        // The way to generate files in other folders is to either specify sub-folders above, or add the "EntityFramework Reverse POCO Code First Generator"
        // into each of the folders you require, then set your <database>.tt files to only generate the relevant section(s) you need by setting:
        //      ElementsToGenerate = Elements.Poco; in your Entity folder,
        //      ElementsToGenerate = Elements.Context | Elements.Interface; in your Context folder,
        //      ElementsToGenerate = Elements.PocoConfiguration; in your Configuration folder.
        // You also need to set the following to the namespace where they now live:
        public static string ContextNamespace = ""; // "YourProject.Data";
        public static string InterfaceNamespace = ""; // "YourProject.Data";
        public static string PocoNamespace = ""; // "YourProject.Data.Entities";
        public static string PocoConfigurationNamespace = ""; // "YourProject.Data.Configuration";


        // Schema *****************************************************************************************************************************
        // If there are multiple schemas, then the table name is prefixed with the schema, except for dbo.
        // Ie. dbo.hello will be Hello.
        //     abc.hello will be Abc_Hello.
        public static bool PrependSchemaName = true; // Control if the schema name is prepended to the table name
        public static string DefaultSchema = null; // Read from the database by the efrpg tool and applied in Generator.Init

        // Enables more granular control if the schema name should be prepended depending on the table
        public static Func<Table, bool> PrependSchemaNameForTable = delegate (Table table)
        {
            // Example:
            //if (table.NameHumanCase.Equals("SomeTable", StringComparison.InvariantCultureIgnoreCase))
            //    return false;

            return true;
        };

        // Enables more granular control if the schema name should be prepended depending on the stored procedure
        public static Func<StoredProcedure, bool> PrependSchemaNameForStoredProcedure = delegate (StoredProcedure sp)
        {
            // Example:
            //if (sp.NameHumanCase.StartsWith("sp_"))
            //    return false;

            return true;
        };

        // Table Suffix ***********************************************************************************************************************
        // Appends the suffix to the generated classes names
        // Ie. If TableSuffix is "Dto" then Order will be OrderDto
        //     If TableSuffix is "Entity" then Order will be OrderEntity
        public static string TableSuffix = null;

        // AddRelationship is a helper function that creates ForeignKey objects and adds them to the foreignKeys list
        public static Action<IDbContextFilter, Generator, List<ForeignKey>, Tables> AddExtraForeignKeys = delegate (IDbContextFilter filter, Generator gen, List<ForeignKey> foreignKeys, Tables tablesAndViews)
        {
            // Northwind example:

            // [Orders] (Table) to [Invoices] (View) is one-to-many using Orders.OrderID = Invoices.OrderID
            // gen.AddRelationship(filter, foreignKeys, tablesAndViews, "orders_to_invoices", Settings.DefaultSchema, "Orders", "OrderID", "dbo", "Invoices", "OrderID", null, null, true);

            // [Orders] (Table) to [Orders Qry] (View) is one-to-zeroOrOne ( [Orders].OrderID = [Orders Qry].OrderID )
            // gen.AddRelationship(filter, foreignKeys, tablesAndViews, "orders_to_ordersQry", Settings.DefaultSchema, "Orders", "OrderID", "dbo", "Orders Qry", "OrderID", "ParentFkName", "ChildFkName", true);

            // [Order Details] (Table) to [Invoices] (View) is one-to-zeroOrOne - but uses a composite-key: ( [Order Details].OrderID,ProductID = [Invoices].OrderID,ProductID )
            // gen.AddRelationship(filter, foreignKeys, tablesAndViews, "orderDetails_to_invoices", Settings.DefaultSchema, "Order Details", new[] { "OrderID", "ProductID" }, "dbo", "Invoices", new[] { "OrderID", "ProductID" }, null, null, true);
        };

        // StoredProcedure return types *******************************************************************************************************
        // Override generation of return models for stored procedures that return entities.
        // If a stored procedure returns an entity, add it to the list below.
        // This will suppress the generation of the return model, and instead return the entity.
        // Example:                       Proc name      Return this entity type instead
        //StoredProcedureReturnTypes.Add("SalesByYear", "SummaryOfSalesByYear");
        public static Dictionary<string, string> StoredProcedureReturnTypes = new Dictionary<string, string>();

        // Enable interception of stored procedure return model when an exception occurs. Typically, when the stored procedure contains temp tables.
        // This allows you render the proper error in comments or fix the return model by manually creating the ReturnModel using a list of DataColumns
        // See https://github.com/ReversePOCO/EntityFramework-Reverse-POCO-Code-First-Generator/wiki/Stored-Procedure-Return-Model-Errors
        public static Action<Exception, StoredProcedure> ReadStoredProcReturnObjectException = delegate (Exception ex, StoredProcedure sp)
        {
            // Store the error so it appears as a comment on the generated method.
            // Override this delegate to suppress the comment or to manually define the return model instead.
            sp.Error = ex.Message;

            // Example of manually defining the return model rather than displaying the error:
            /*if (!sp.ReturnModels.Any() && ex.Message.StartsWith("Invalid object name", StringComparison.OrdinalIgnoreCase))
            {
                if (sp.NameHumanCase.Equals("YourProcNameHere", StringComparison.OrdinalIgnoreCase))
                {
                    sp.ReturnModels.Add(new List<DataColumn>
                    {
                        new DataColumn("Id", typeof(int)) { AllowDBNull = false, Unique = true },
                        new DataColumn("Description", typeof(string))
                    });
                }
            }*/
        };

        // Enable interception of stored procedure return model
        public static Action<StoredProcedure> ReadStoredProcReturnObjectCompleted = delegate (StoredProcedure sp)
        {
            // Example of how to add a row processed boolean column to a stored procedure's return model
            /*if (sp.ReturnModels.Any() && sp.NameHumanCase.Contains("process"))
            {
                var rm = sp.ReturnModels.First();
                rm.Add(new DataColumn("RowProcessed", typeof(bool)) { AllowDBNull = false });
            }*/
        };


        // Renaming ***********************************************************************************************************************
        // Table renaming (single context generation only) ************************************************************************************
        // Use the following function to rename tables such as tblOrders to Orders, Shipments_AB to Shipments, etc.
        // Example:
        public static Func<string, string, bool, string> TableRename = delegate (string name, string schema, bool isView)
        {
            // Example
            //if (name.StartsWith("tbl"))
            //    name = name.Remove(0, 3);
            //name = name.Replace("_AB", "");

            //if(isView)
            //    name = name + "View";

            // If you turn pascal casing off (UsePascalCase = false), and use the pluralisation service, and some of your
            // tables names are all UPPERCASE, some words ending in IES such as CATEGORIES get singularised as CATEGORy.
            // Therefore you can make them lowercase by using the following
            //return Inflector.MakeLowerIfAllCaps(name);

            // If you are using the pluralisation service and you want to rename a table, make sure you rename the table to the plural form.
            // For example, if the table is called Treez (with a z), and your pluralisation entry is
            //     new CustomPluralizationEntry("Tree", "Trees")
            // Use this TableRename function to rename Treez to the plural (not singular) form, Trees:
            //if (name == "Treez") return "Trees";

            return name;
        };

        // Use the following function if you need to apply additional modifications to a table
        // Called just before UpdateColumn
        public static Action<Table> UpdateTable = delegate (Table table)
        {
            // Add an extra comment
            //if (table.NameHumanCase.Equals("SomeTable", StringComparison.InvariantCultureIgnoreCase))
            //    table.AdditionalComment = "Example comment";

            // To add a base class to a table
            //if (table.NameHumanCase == "User")
            //    table.BaseClasses = " : IdentityUser<int, CustomUserLogin, CustomUserRole, CustomUserClaim>";
            //if (table.NameHumanCase == "LogData" || table.NameHumanCase == "ReturnData" || table.NameHumanCase == "DataBlob")            
            //    table.BaseClasses = " : ReportData";

            // To add attributes
            //table.Attributes.Add("[Serializable]");
        };

        // Use the following function if you need to apply additional modifications to a column
        // eg. normalise names etc.
        public static Action<Column, Table, List<EnumDefinition>, List<JsonColumnMapping>> UpdateColumn = delegate (Column column, Table table, List<EnumDefinition> enumDefinitions, List<JsonColumnMapping> jsonColumnMappings)
        {
            // Rename column
            //if (column.IsPrimaryKey && column.NameHumanCase == "PkId")
            //    column.NameHumanCase = "Id";

            // .IsConcurrencyToken() must be manually configured. However .IsRowVersion() can be automatically detected.
            //if (table.NameHumanCase.Equals("SomeTable", StringComparison.InvariantCultureIgnoreCase) && column.NameHumanCase.Equals("SomeColumn", StringComparison.InvariantCultureIgnoreCase))
            //    column.IsConcurrencyToken = true;

            // Remove table name from primary key
            //if (column.IsPrimaryKey && column.NameHumanCase.Equals(table.NameHumanCase + "Id", StringComparison.InvariantCultureIgnoreCase))
            //    column.NameHumanCase = "Id";

            // Remove column from poco class as it will be inherited from a base class
            //if (column.IsPrimaryKey &&
            //    (
            //        table.NameHumanCase.Equals("LogData", StringComparison.InvariantCultureIgnoreCase) ||
            //        table.NameHumanCase.Equals("ReturnData", StringComparison.InvariantCultureIgnoreCase) ||
            //        table.NameHumanCase.Equals("DataBlob", StringComparison.InvariantCultureIgnoreCase)
            //    ))
            //    column.ExistsInBaseClass = true; // If true, does not generate the property for this column as it will exist in a base class

            // Use the extended properties to perform tasks to column
            //if (column.ExtendedProperty == "HIDE")
            //    column.Hidden = true; // Hidden means the generator does not generate any code for this column at all.

            // Apply the "override" access modifier to a specific column.
            //if (column.NameHumanCase == "id")
            //    column.OverrideModifier = true;
            // This will create: public override long id { get; set; }

            // Make property partial, see https://github.com/ReversePOCO/EntityFramework-Reverse-POCO-Code-First-Generator/wiki/Partial-properties
            //if (table.NameHumanCase.Equals("SomeTable", StringComparison.InvariantCultureIgnoreCase) && column.NameHumanCase.Equals("SomeColumn", StringComparison.InvariantCultureIgnoreCase))
            //    column.IsPartial = true;

            // Use ExtendedProperties dictionary to access specific extended property by name.
            // See https://github.com/ReversePOCO/EntityFramework-Reverse-POCO-Code-First-Generator/wiki/Extended-Property-Names-Feature
            // Example: Add JsonPropertyName attribute from extended property
            // In SQL Server, set extended property: EXEC sp_addextendedproperty @name = N'JsonPropertyName', @value = N'id', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'YourTable', @level2type = N'COLUMN', @level2name = N'SystemId'
            ApplyJsonPropertyNameAttribute(column);

            ApplyJsonColumnMappings(column, table, jsonColumnMappings);
            ApplyDataAnnotations(column);
            ApplyEnumTypeReplacement(column, table, enumDefinitions);
        };

        // Update column to include data annotations, enum replacements, and JSON column mappings
        public static void ApplyEnumTypeReplacement(Column column, Table table, List<EnumDefinition> enumDefinitions)
        {
            // Perform Enum property type replacement
            var enumDefinition = enumDefinitions?.FirstOrDefault(e =>
                (e.Schema.Equals(table.Schema.DbName, StringComparison.InvariantCultureIgnoreCase)) &&
                (e.Table == "*" || e.Table.Equals(table.DbName, StringComparison.InvariantCultureIgnoreCase) || e.Table.Equals(table.NameHumanCase, StringComparison.InvariantCultureIgnoreCase)) &&
                (e.Column.Equals(column.DbName, StringComparison.InvariantCultureIgnoreCase) || e.Column.Equals(column.NameHumanCase, StringComparison.InvariantCultureIgnoreCase)));

            if (enumDefinition != null)
            {
                column.PropertyType = enumDefinition.EnumType;

                // Only prepend the cast if it isn't already present, otherwise a column whose enum
                // type is configured via more than one mechanism ends up double-cast: (Type) (Type) 0
                var cast = "(" + enumDefinition.EnumType + ")";
                if (!string.IsNullOrEmpty(column.Default) && !column.Default.StartsWith(cast, StringComparison.Ordinal))
                    column.Default = cast + " " + column.Default;
            }
        }

        // Update column to include data annotations
        public static void ApplyDataAnnotations(Column column)
        {
            if (UseDataAnnotations)
            {
                if (column.IsPrimaryKey)
                    column.Attributes.Add(string.Format("[Key, Column(Order = {0})]", column.Ordinal));

                if (column.IsMaxLength)
                    column.Attributes.Add("[MaxLength]");

                if (column.IsRowVersion)
                    column.Attributes.Add("[Timestamp, ConcurrencyCheck]");

                // SQL Server 2025 vector type - use [Column(TypeName = "vector(n)")] instead of MaxLength
                var isVectorType = column.SqlPropertyType != null && column.SqlPropertyType.StartsWith("vector", StringComparison.InvariantCultureIgnoreCase);
                if (isVectorType)
                {
                    column.Attributes.Add(string.Format("[Column(TypeName = \"{0}\")]", column.SqlPropertyType));
                }
                else if (!column.IsMaxLength && column.MaxLength > 0)
                {
                    column.Attributes.Add(string.Format("[MaxLength({0})]", column.MaxLength));
                    if (column.PropertyType.Equals("string", StringComparison.InvariantCultureIgnoreCase))
                        column.Attributes.Add(string.Format("[StringLength({0})]", column.MaxLength));
                }

                if (!column.IsNullable && !column.IsComputed)
                {
                    if (column.PropertyType.Equals("string", StringComparison.InvariantCultureIgnoreCase) && column.AllowEmptyStrings)
                        column.Attributes.Add("[Required(AllowEmptyStrings = true)]");
                    else
                        column.Attributes.Add("[Required]");
                }

                column.Attributes.Add(string.Format("[Display(Name = \"{0}\")]", column.DisplayName));
            }
        }

        // Perform JSON column to POCO class mapping
        public static void ApplyJsonColumnMappings(Column column, Table table, List<JsonColumnMapping> jsonColumnMappings)
        {
            // Check if this is a JSON column type and if there's a mapping defined
            // Supports: SQL Server "json", PostgreSQL "json" and "jsonb"
            var isJsonColumn = column.SqlPropertyType != null &&
                               (column.SqlPropertyType.Equals("json", StringComparison.InvariantCultureIgnoreCase) ||
                                column.SqlPropertyType.Equals("jsonb", StringComparison.InvariantCultureIgnoreCase));

            if (isJsonColumn)
            {
                var jsonMapping = jsonColumnMappings?.FirstOrDefault(m =>
                    (m.Schema == "*" || m.Schema.Equals(table.Schema.DbName, StringComparison.InvariantCultureIgnoreCase)) &&
                    (m.Table == "*" || m.Table.Equals(table.DbName, StringComparison.InvariantCultureIgnoreCase) ||
                     m.Table.Equals(table.NameHumanCase, StringComparison.InvariantCultureIgnoreCase)) &&
                    (m.Column.Equals(column.DbName, StringComparison.InvariantCultureIgnoreCase) ||
                     m.Column.Equals(column.NameHumanCase, StringComparison.InvariantCultureIgnoreCase)));

                if (jsonMapping != null)
                {
                    column.PropertyType = jsonMapping.PropertyType;
                    column.IsJsonMapped = true;

                    // Add the additional namespace to this table only (not globally)
                    if (!string.IsNullOrEmpty(jsonMapping.AdditionalNamespace))
                    {
                        var namespaces = jsonMapping.AdditionalNamespace.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var ns in namespaces)
                        {
                            var trimmedNs = ns.Trim();
                            if (!string.IsNullOrEmpty(trimmedNs) && !table.AdditionalNamespaces.Contains(trimmedNs))
                                table.AdditionalNamespaces.Add(trimmedNs);
                        }
                    }

                    // When the user is configuring the column in a partial class, suppress the generated
                    // builder.Property(...) call to avoid "property can only be configured once" runtime errors.
                    if (jsonMapping.ExcludePropertyConfiguration)
                        column.ExcludePropertyConfiguration = true;

                    // Clear the default value for JSON columns mapped to custom types
                    column.Default = string.Empty;
                }
            }
        }

        // Apply OwnedEntityMappings to a table: group prefixed columns into owned entity definitions
        public static void ApplyOwnedEntityMappings(Table table, List<OwnedEntityMapping> ownedEntityMappings)
        {
            if (ownedEntityMappings == null || !ownedEntityMappings.Any() || table == null)
                return;

            foreach (var mapping in ownedEntityMappings)
            {
                if (string.IsNullOrEmpty(mapping.ColumnPrefix) || string.IsNullOrEmpty(mapping.PropertyName) || string.IsNullOrEmpty(mapping.PropertyType))
                    continue;

                var schemaMatches = string.IsNullOrEmpty(mapping.Schema) || mapping.Schema == "*" ||
                    mapping.Schema.Equals(table.Schema?.DbName ?? string.Empty, StringComparison.InvariantCultureIgnoreCase);
                var tableMatches = schemaMatches &&
                    (mapping.Table == "*" || mapping.Table.Equals(table.DbName, StringComparison.InvariantCultureIgnoreCase) ||
                                             mapping.Table.Equals(table.NameHumanCase, StringComparison.InvariantCultureIgnoreCase));

                if (!tableMatches)
                    continue;

                var matchedColumns = table.Columns
                    .Where(c => c.DbName.StartsWith(mapping.ColumnPrefix, StringComparison.InvariantCultureIgnoreCase))
                    .ToList();

                if (!matchedColumns.Any())
                    continue;

                // Normalize the prefix to strip trailing separators, then remove non-alphanumeric chars
                // so we can match against NameHumanCase (which has underscores removed and is PascalCase)
                var normalizedPrefix = Regex.Replace(
                    mapping.ColumnPrefix.TrimEnd('_', ' ', '-'),
                    @"[^a-zA-Z0-9]", string.Empty);

                foreach (var col in matchedColumns)
                {
                    col.Hidden = true;

                    // Strip the normalized prefix from NameHumanCase to get the owned entity property name
                    if (!string.IsNullOrEmpty(normalizedPrefix) &&
                        col.NameHumanCase.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        var remainder = col.NameHumanCase.Substring(normalizedPrefix.Length);
                        col.OwnedEntityPropertyName = string.IsNullOrEmpty(remainder) ? col.NameHumanCase : remainder;
                    }
                    else
                    {
                        col.OwnedEntityPropertyName = col.NameHumanCase;
                    }
                }

                table.OwnedEntities.Add(new OwnedEntity
                {
                    PropertyName = mapping.PropertyName,
                    PropertyType = mapping.PropertyType,
                    ColumnPrefix = mapping.ColumnPrefix,
                    Columns = matchedColumns
                });

            }
        }

        // Add [JsonPropertyName] attribute from database extended property
        public static void ApplyJsonPropertyNameAttribute(Column column)
        {
            // Include [JsonPropertyName("{name}")] attribute for .NetCore from extended property in db
            if (IsEfCore8Plus() && column.ExtendedProperties.TryGetValue("JsonPropertyName", out var jsonPropertyName))
            {
                column.Attributes.Add($"[JsonPropertyName(\"{jsonPropertyName}\")]");
                const string jsonNamespace = "System.Text.Json.Serialization";
                if (!AdditionalNamespaces.Contains(jsonNamespace))
                    AdditionalNamespaces.Add(jsonNamespace);
            }
            // Include [JsonProperty("{name}")] attribute for .Net 6 from extended property in db
            if (IsEf6() && column.ExtendedProperties.TryGetValue("JsonPropertyName", out var jsonProperty))
            {
                column.Attributes.Add($"[JsonProperty(\"{jsonProperty}\")]");
                const string jsonNamespace = "Newtonsoft.Json";
                if (!AdditionalNamespaces.Contains(jsonNamespace))
                    AdditionalNamespaces.Add(jsonNamespace);
            }
        }

        // Configures the key property to either use IDENTITY or HILO database feature to generate values for new entities.
        public static Func<Column, string> ColumnIdentity = delegate (Column c)
        {
            if (!IsEfCore8Plus())
                return ".UseSqlServerIdentityColumn()";

            // At this point we are using EFCore 3, 5+ which supports HiLo sequences.
            // Example of using a HiLo sequence using this function.
            /*if (c.ParentTable.NameHumanCase.Equals("SomeTable",  StringComparison.InvariantCultureIgnoreCase) &&
                c.NameHumanCase            .Equals("SomeColumn", StringComparison.InvariantCultureIgnoreCase))
            {
                return ".UseHiLo(\"your_sequence_name\",\"your_sequence_schema\")";
            }*/

            var hiLoSequence = HiLoSequences?.FirstOrDefault(x =>
                x.Schema.Equals(c.ParentTable.Schema.DbName, StringComparison.InvariantCultureIgnoreCase) &&
                (x.Table == "*" || x.Table.Equals(c.ParentTable.DbName, StringComparison.InvariantCultureIgnoreCase)));

            if (hiLoSequence != null)
                return string.Format(".UseHiLo(\"{0}\", \"{1}\")", hiLoSequence.SequenceName, hiLoSequence.SequenceSchema);

            // UseIdentityColumn is a provider extension method, not part of EF Core itself, so it only exists where
            // the provider ships it. SqlServer, Npgsql and Oracle.EntityFrameworkCore all define it; Pomelo spells
            // its equivalent UseMySqlIdentityColumn, so emitting UseIdentityColumn for MySQL produces code that does
            // not compile. ValueGeneratedOnAdd() has already been emitted by this point and is all Pomelo needs to
            // pick up AUTO_INCREMENT, so nothing further is required.
            if (DatabaseType == DatabaseType.MySql)
                return string.Empty;

            return ".UseIdentityColumn()";
        };

        // In order to use this function, Settings.ElementsToGenerate must contain both Elements.Poco and Elements.Enum;
        public static Action<Table> AddEnum = delegate (Table table)
        {
            /*if (table.HasPrimaryKey && table.PrimaryKeys.Count() == 1 && table.Columns.Any(x => x.PropertyType == "string"))
            {
                // Example: choosing tables with certain naming conventions for enums. Please use your own conventions.
                if (table.NameHumanCase.StartsWith("Enum", StringComparison.InvariantCultureIgnoreCase) ||
                    table.NameHumanCase.EndsWith  ("Enum", StringComparison.InvariantCultureIgnoreCase))
                {
                    try
                    {
                        Enumerations.Add(new EnumerationSettings
                        {
                            Name       = table.NameHumanCase.Replace("Enum","").Replace("Enum","") + "Enum",
                            Table      = table.Schema.DbName + "." + table.DbName,
                            NameField  = table.Columns.First(x => x.PropertyType == "string").DbName, // Or specify your own
                            ValueField = table.PrimaryKeys.Single().DbName, // Or specify your own
                            GroupField = string.Empty // Or specify your own
                        });

                        // This will cause this table to not be reverse-engineered.
                        // This means it was only required to generate an enum and can now be removed.
                        table.RemoveTable = true; // Remove this line if you want to keep it in your dbContext.
                    }
                    catch
                    {
                        // Swallow exception
                    }
                }
            }*/
        };

        // Use the following function if you need to apply additional modifications to a enum
        // Called just before UpdateEnumMember
        public static Action<Enumeration> UpdateEnum = delegate (Enumeration enumeration)
        {
            //enumeration.EnumAttributes.Add("[DataContract]");
        };

        // Use the following function if you need to apply additional modifications to a enum member
        public static Action<EnumerationMember> UpdateEnumMember = delegate (EnumerationMember enumerationMember)
        {
            //enumerationMember.Attributes.Add("[EnumMember]");
            //enumerationMember.Attributes.Add("[SomeAttribute(\"" + enumerationMember.AllValues["SomeName"] + " \")]");
        };

        // Writes any boilerplate stuff inside the POCO class body
        public static Func<Table, string> WriteInsideClassBody = delegate (Table t)
        {
            // Example:
            //return "    // " + t.NameHumanCase + Environment.NewLine;

            // Do nothing by default
            return string.Empty;
        };

        // Using Views *****************************************************************************************************************
        // SQL Server does not support the declaration of primary-keys in VIEWs. Entity Framework's EDMX designer (and this generator)
        // assume that all non-null columns in a VIEW are primary-key columns, this will be incorrect for most non-trivial applications.
        // This callback will be invoked for each VIEW found in the database. Use it to declare which columns participate in that VIEW's
        // primary-key by setting 'IsPrimaryKey = true'.
        // If no columns are marked with 'IsPrimaryKey = true' then this T4 template defaults to marking all non-NULL columns as primary key columns.
        // To set-up Foreign-Key relationships between VIEWs and Tables (or even other VIEWs) use the 'AddForeignKeys' callback below.
        public static Action<Table> ViewProcessing = delegate (Table view)
        {
            // Below is example code for the Northwind database that configures the 'VIEW [Orders Qry]' and 'VIEW [Invoices]'
            /*switch (view.DbName)
            {
                case "Orders Qry":
                    // VIEW [Orders Qry] uniquely identifies rows with the 'OrderID' column:
                    view.Columns.Single(col => col.DbName == "OrderID").IsPrimaryKey = true;
                    break;

                case "Invoices":
                    // VIEW [Invoices] has a composite primary key (OrderID+ProductID), so both columns must be marked as a Primary Key:
                    foreach (var col in view.Columns.Where(c => c.DbName == "OrderID" || c.DbName == "ProductID"))
                        col.IsPrimaryKey = true;
                    break;
            }*/
        };

        // StoredProcedure renaming ************************************************************************************************************
        // Use the following function to rename stored procs such as sp_CreateOrderHistory to CreateOrderHistory, my_sp_shipments to Shipments, etc.
        public static Func<StoredProcedure, string> StoredProcedureRename = delegate (StoredProcedure sp)
        {
            // Example:
            //if (sp.NameHumanCase.StartsWith("sp_"))
            //    return sp.NameHumanCase.Remove(0, 3);
            //return sp.NameHumanCase.Replace("my_sp_", "");

            return sp.NameHumanCase; // Do nothing by default
        };

        // Use the following function to rename the return model automatically generated for stored procedure.
        // By default it's <proc_name>ReturnModel.
        public static Func<string, StoredProcedure, string> StoredProcedureReturnModelRename = delegate (string name, StoredProcedure sp)
        {
            // Check if this is a table-valued function
            /*if (sp.IsTableValuedFunction)
            {
                // Example: Rename table-valued function return models
                if (sp.NameHumanCase.Equals("GetCustomerOrders", StringComparison.InvariantCultureIgnoreCase))
                    return "CustomerOrder";

                // Or apply a general pattern for all TVFs
                if (sp.NameHumanCase.StartsWith("Get"))
                    return sp.NameHumanCase.Substring(3); // Remove "Get" prefix
            }*/

            // Handle regular stored procedures
            /*if (sp.IsStoredProcedure)
            {
                if (sp.NameHumanCase.Equals("ComputeValuesForDate", StringComparison.InvariantCultureIgnoreCase))
                    return "ValueSet";
                if (sp.NameHumanCase.Equals("SalesByYear", StringComparison.InvariantCultureIgnoreCase))
                    return "SalesSet";
            }*/

            return name; // Do nothing by default
        };

        // Mapping Table renaming *********************************************************************************************************************
        // By default, name of the properties created relate to the table the foreign key points to and not the mapping table.
        // Use the following function to rename the properties created by ManyToMany relationship tables especially if you have 2 relationships between the same tables.
        // Example:
        public static Func<string, string, string, string> MappingTableRename = delegate (string mappingTable, string tableName, string entityName)
        {
            // Example: If you have two mapping tables such as one being UserRequiredSkills snd one being UserOptionalSkills, this would change the name of one property
            //if (mappingTable == "UserRequiredSkills" && tableName == "User")
            //    return "RequiredSkills";

            // or if you want to give the same property name on both classes
            //if (mappingTable == "UserRequiredSkills")
            //    return "UserRequiredSkills";

            return entityName;
        };

        public static Func<string, ForeignKey, string, Relationship, short, string> ForeignKeyName = delegate (string tableName, ForeignKey foreignKey, string foreignKeyName, Relationship relationship, short attempt)
        {
            string fkName;

            // 5 Attempts to correctly name the foreign key
            switch (attempt)
            {
                case 1:
                    // Try without appending foreign key name
                    fkName = tableName;
                    break;

                case 2:
                    // Only called if foreign key name ends with "id"
                    // Use foreign key name without "id" at end of string
                    fkName = foreignKeyName.Remove(foreignKeyName.Length - 2, 2);
                    break;

                case 3:
                    // Use foreign key name only
                    fkName = foreignKeyName;
                    break;

                case 4:
                    // Use table name and foreign key name
                    fkName = tableName + "_" + foreignKeyName;
                    break;

                case 5:
                    // Used in for loop 1 to 99 to append a number to the end
                    fkName = tableName;
                    break;

                default:
                    // Give up
                    fkName = tableName;
                    break;
            }

            // Apply custom foreign key renaming rules. Can be useful in applying pluralization.
            // For example:
            /*if (tableName == "Employee" && foreignKey.FkColumn == "ReportsTo")
                return "Manager";

            if (tableName == "Territories" && foreignKey.FkTableName == "EmployeeTerritories")
                return "Locations";

            if (tableName == "Employee" && foreignKey.FkTableName == "Orders" && foreignKey.FkColumn == "EmployeeID")
                return "ContactPerson";
            */

            // FK_TableName_FromThisToParentRelationshipName_FromParentToThisChildsRelationshipName
            // (e.g. FK_CustomerAddress_Customer_Addresses will extract navigation properties "address.Customer" and "customer.Addresses")
            // Feel free to use and change the following
            /*if (foreignKey.ConstraintName.StartsWith("FK_") && foreignKey.ConstraintName.Count(x => x == '_') == 3)
            {
                var parts = foreignKey.ConstraintName.Split('_');
                if (!string.IsNullOrWhiteSpace(parts[2]) && !string.IsNullOrWhiteSpace(parts[3]) && parts[1] == foreignKey.FkTableName)
                {
                    if (relationship == Relationship.OneToMany)
                        fkName = parts[3];
                    else if (relationship == Relationship.ManyToOne)
                        fkName = parts[2];
                }
            }*/

            return fkName;
        };

        // This foreign key filter used in addition to DbContextFilter.ForeignKeyFilter()
        // Return null to exclude this foreign key
        public static Func<ForeignKey, ForeignKey> ForeignKeyFilterFunc = delegate (ForeignKey fk)
        {
            // Return null to exclude this foreign key, or set IncludeReverseNavigation = false
            // to include the foreign key but not generate reverse navigation properties.
            // Example, to exclude all foreign keys for the Categories table, use:
            //if (fk.PkTableName == "Categories")
            //    return null;

            // Example, to exclude reverse navigation properties for tables ending with Type, use:
            //if (fk.PkTableName.EndsWith("Type"))
            //    fk.IncludeReverseNavigation = false;

            // You can also change the access modifier of the foreign-key's navigation property:
            //if(fk.PkTableName == "Categories")
            //     fk.AccessModifier = "internal";

            return fk;
        };

        public static Func<Table, Table, string, string, string[]> ForeignKeyAnnotationsProcessing = delegate (Table fkTable, Table pkTable, string propName, string fkPropName)
        {
            // Example:
            // Each navigation property that is a reference to User are left intact
            //if(pkTable.NameHumanCase.Equals("User") && propName.Equals("User"))
            //    return null;

            // all the others are marked with this attribute
            //return new[] { "System.Runtime.Serialization.IgnoreDataMember" };

            return null;
        };

        // Column modification ****************************************************************************************************************
        // Use the following list to replace column byte types with Enums.
        // As long as the type can be mapped to your new type, all is well.
        public static Action<List<EnumDefinition>> AddEnumDefinitions = delegate (List<EnumDefinition> enumDefinitions)
        {
            // Examples:
            //enumDefinitions.Add(new EnumDefinition { Schema = Settings.DefaultSchema, Table = "match_table_name", Column = "match_column_name", EnumType = "name_of_enum" });

            // This will replace OrderHeader.OrderStatus type to be an OrderStatusType enum
            //enumDefinitions.Add(new EnumDefinition { Schema = Settings.DefaultSchema, Table = "OrderHeader", Column = "OrderStatus", EnumType = "OrderStatusType" }); 

            // This will replace any table *.OrderStatus type to be an OrderStatusType enum
            //enumDefinitions.Add(new EnumDefinition { Schema = Settings.DefaultSchema, Table = "*", Column = "OrderStatus", EnumType = "OrderStatusType" });

            // Stored procedure and function return models are covered too. A "*" definition also retypes any integral
            // return column of that name; to scope it to one routine, put the routine's name in Table instead.
            //enumDefinitions.Add(new EnumDefinition { Schema = Settings.DefaultSchema, Table = "GetOrdersByStatus", Column = "OrderStatus", EnumType = "OrderStatusType" });
        };

        // JSON column to POCO class mapping *************************************************************************************************
        // Use the following callback to map JSON database columns to specific C# POCO classes instead of a string.
        // This allows you to work with strongly-typed objects instead of having to deserialize JSON strings manually.
        public static Action<List<JsonColumnMapping>> AddJsonColumnMappings = delegate (List<JsonColumnMapping> jsonColumnMappings)
        {
            // Examples:

            // Map a specific JSON column to a POCO class
            //jsonColumnMappings.Add(new JsonColumnMapping 
            //{ 
            //    Schema = Settings.DefaultSchema, 
            //    Table = "Orders", 
            //    Column = "ShippingAddress", 
            //    PropertyType = "Address",
            //    AdditionalNamespace = "MyApp.Models", // Optional: Add namespace if needed
            //    ExcludePropertyConfiguration = false  // Optional: When true, suppresses the generated builder.Property(...) fluent configuration.
            //                                             for this column. Use this when you are configuring the column yourself in a partial class
            //                                             (e.g. using OwnsMany/ToJson), to avoid the "property can only be configured once" runtime error.
            //});

            // Map all columns named "Metadata" across all tables to a specific type
            //jsonColumnMappings.Add(new JsonColumnMapping 
            //{ 
            //    Schema = "*", 
            //    Table = "*", 
            //    Column = "Metadata", 
            //    PropertyType = "Dictionary<string, object>",
            //    AdditionalNamespace = "System.Collections.Generic", // Optional
            //    ExcludePropertyConfiguration = false                // Optional
            //});

            // Map to a complex type with generics
            //jsonColumnMappings.Add(new JsonColumnMapping 
            //{ 
            //    Schema = Settings.DefaultSchema, 
            //    Table = "Products", 
            //    Column = "Tags", 
            //    PropertyType = "List<string>",
            //    AdditionalNamespace = "System.Collections.Generic",
            //    ExcludePropertyConfiguration = false                // Optional
            //});

            // Map to a custom class with full namespace
            //jsonColumnMappings.Add(new JsonColumnMapping 
            //{ 
            //    Schema = "dbo", 
            //    Table = "Users", 
            //    Column = "Preferences", 
            //    PropertyType = "MyApp.Models.UserPreferences", // Fully qualified type name
            //    ExcludePropertyConfiguration = false           // Optional
            //});
        };

        // Owned entity column grouping *********************************************************************************************************
        // Use the following callback to group columns with a common prefix into an EF Core owned entity property.
        // Matched columns are hidden from direct POCO generation; a single property of the specified type is generated instead.
        // A builder.OwnsOne(...) configuration block is emitted automatically in the configuration class.
        // One owned entity POCO class (e.g. Address, Money) is also generated per unique PropertyType found in the mappings.
        // Set OwnedEntityFolder to place these classes in a sub-folder separate from your regular POCOs.
        public static Action<List<OwnedEntityMapping>> AddOwnedEntityMappings = delegate (List<OwnedEntityMapping> ownedEntityMappings)
        {
            // Examples:

            // Map BillingAddress_* columns on the Customer table to an Address owned entity
            //ownedEntityMappings.Add(new OwnedEntityMapping
            //{
            //    Schema       = Settings.DefaultSchema,
            //    Table        = "Customer",
            //    ColumnPrefix = "BillingAddress_",   // matches BillingAddress_Street, BillingAddress_City, etc.
            //    PropertyName = "BillingAddress",     // property name on Customer POCO
            //    PropertyType = "Address",            // C# type of the owned entity class (auto-generated)
            //});

            // Map ShippingAddress_* columns to a second Address owned entity on the same table
            //ownedEntityMappings.Add(new OwnedEntityMapping
            //{
            //    Schema       = Settings.DefaultSchema,
            //    Table        = "Customer",
            //    ColumnPrefix = "ShippingAddress_",
            //    PropertyName = "ShippingAddress",
            //    PropertyType = "Address"
            //});
        };

        // Helper functions ***************************************************************************************************************
        public static bool DbContextClassIsPartial()
        {
            return DbContextClassModifiers != null && DbContextClassModifiers.Contains("partial");
        }

        public static bool EntityClassesArePartial()
        {
            return EntityClassesModifiers != null && EntityClassesModifiers.Contains("partial");
        }

        public static bool ConfigurationClassesArePartial()
        {
            return ConfigurationClassesModifiers != null && ConfigurationClassesModifiers.Contains("partial");
        }

        private static string _dbContextInterfaceName;
        public static string DbContextInterfaceName
        {
            get { return _dbContextInterfaceName ?? ("I" + DbContextName); }
            set { _dbContextInterfaceName = value; }
        }

        private static bool _explicitDefaultConstructorArgument;
        private static string _defaultConstructorArgument;
        public static string DefaultConstructorArgument
        {
            // = null; // Defaults to "Name=" + ConnectionStringName, use null in order not to call the base constructor
            get { return _explicitDefaultConstructorArgument ? _defaultConstructorArgument : string.Format('"' + "Name={0}" + '"', ConnectionStringName); }
            set { _explicitDefaultConstructorArgument = true; _defaultConstructorArgument = value; }
        }

        // Don't forget to take a look at DbContextFilter and FilterSettings classes!
        // That's it, nothing else to configure ***********************************************************************************************

        public static bool IsEf6() => TemplateType == TemplateType.Ef6;
        public static bool IsEfCore8Plus() => EfCoreVersion() >= 8;
        public static bool IsEfCore9Plus() => EfCoreVersion() >= 9;
        public static bool IsEfCore10Plus() => EfCoreVersion() >= 10;
        public static int EfCoreVersion()
        {
            switch (TemplateType)
            {
                case TemplateType.EfCore8:
                    return 8;

                case TemplateType.EfCore9:
                    return 9;

                case TemplateType.EfCore10:
                    return 10;

                case TemplateType.Ef6:
                default:
                    return 0;
            }
        }

        public static string DatabaseProvider()
        {
            switch (DatabaseType)
            {
                case DatabaseType.PostgreSQL:
                    return "UseNpgsql";

                case DatabaseType.MySql:
                    return "UseMySql";

                case DatabaseType.Oracle:
                    return "UseOracle";

                case DatabaseType.SQLite:
                    return "UseSqlite";

                default:
                    return "UseSqlServer";
            }
        }

        /// <summary>
        ///     Assembly name of the EF Core provider for the current <see cref="DatabaseType" />. This is the value EF Core
        ///     reports through DatabaseFacade.ProviderName, and is what IsSqlServer()/IsNpgsql()/IsSqlite() compare against.
        ///     Returns null for any database type with no EF Core provider, so that every IsXxx() check answers
        ///     false rather than confidently wrong.
        /// </summary>
        public static string DatabaseProviderAssemblyName()
        {
            switch (DatabaseType)
            {
                case DatabaseType.SqlServer:
                    return "Microsoft.EntityFrameworkCore.SqlServer";

                case DatabaseType.PostgreSQL:
                    return "Npgsql.EntityFrameworkCore.PostgreSQL";

                // Pomelo rather than Oracle's MySql.EntityFrameworkCore, to match DatabaseProvider() returning
                // "UseMySql". Oracle's provider spells that method "UseMySQL" and reports "MySql.EntityFrameworkCore".
                case DatabaseType.MySql:
                    return "Pomelo.EntityFrameworkCore.MySql";

                case DatabaseType.Oracle:
                    return "Oracle.EntityFrameworkCore";

                case DatabaseType.SQLite:
                    return "Microsoft.EntityFrameworkCore.Sqlite";

                default:
                    return null;
            }
        }

        public static string SqlParameter()
        {
            switch (DatabaseType)
            {
                case DatabaseType.PostgreSQL:
                    return "NpgsqlParameter";

                case DatabaseType.MySql:
                    return "MySqlParameter";

                case DatabaseType.Oracle:
                    return "OracleParameter";

                case DatabaseType.SQLite:
                    return "SqliteParameter";

                default:
                    return "SqlParameter";
            }
        }

        public static string SqlParameterValue()
        {
            switch (DatabaseType)
            {
                case DatabaseType.PostgreSQL:
                    return "NpgsqlValue";

                case DatabaseType.MySql:
                    return "Value";

                case DatabaseType.Oracle:
                    return "Value";

                case DatabaseType.SQLite:
                    return "Value";

                default:
                    return "SqlValue";
            }
        }

        public static string Root;
        public static string TemplateFile;

        public static bool NeedsNullForgiving() => AllowNullStrings || NullableReverseNavigationProperties;

        public static void CheckSettings()
        {
            if (TemplateType == TemplateType.Ef6)
            {
                // Nullable reference types are only supported in language version 8.0 or greater
                AllowNullStrings = false;
                NullableReverseNavigationProperties = false;
            }
        }
    }
}