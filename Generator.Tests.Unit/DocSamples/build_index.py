"""Builds Settings-Reference.md as an A-Z index over the per-setting pages.

Each row is: setting, one-line summary, the page it lives on. The mapping below is the only
hand-maintained part; everything else is checked against Settings.cs so a new setting cannot be
silently left out.
"""
import io, os, re, sys

WIKI = r"C:\S\Source (open source)\EntityFramework-Reverse-POCO-Code-First-Generator.wiki"
GEN  = r"C:\S\Source (open source)\EntityFramework-Reverse-POCO-Code-First-Generator"

# setting -> (page, one-line summary). A setting documented on another setting's page names its heading there
# as "Page#anchor", using GitHub's rule for turning a heading into an anchor (see anchor_of); main() checks that
# the heading exists, so a renamed heading fails here rather than dropping the reader at the top of the page.
M = {
 # Core
 "DatabaseType":("Settings.DatabaseType","Which database to read: SqlServer, PostgreSQL, MySql, Oracle, SQLite"),
 "TemplateType":("Settings.DatabaseType#choosing-templatetype","Which code templates to use. Match your EF version, not your .NET version"),
 "ConnectionString":("Settings.ConnectionStringName#settingsconnectionstring","The connection string the **generator** uses at design time"),
 "ConnectionStringName":("Settings.ConnectionStringName","A key your **application** looks up at run time"),
 "ConnectionStringActions":("Settings.ConnectionStringName#settingsconnectionstringactions","Extra fluent calls appended to the provider setup"),
 "CommandTimeout":("Settings.CommandTimeout","How long each schema query may take, in seconds"),

 # Naming and namespaces
 "Namespace":("Settings.Namespace","The namespace all generated code goes into"),
 "UseNamespace":("Settings.Namespace#settingsusenamespace","Suppresses the namespace declaration entirely"),
 "UseFileScopedNamespaces":("Settings.Namespace#settingsusefilescopednamespaces","`namespace X;` instead of `namespace X { }`"),
 "ContextNamespace":("Settings.Namespace#the-four-cross-namespace-settings","A `using` for where the DbContext lives"),
 "InterfaceNamespace":("Settings.Namespace#the-four-cross-namespace-settings","A `using` for where the interface lives"),
 "PocoNamespace":("Settings.Namespace#the-four-cross-namespace-settings","A `using` for where the entities live"),
 "PocoConfigurationNamespace":("Settings.Namespace#the-four-cross-namespace-settings","A `using` for where the configurations live"),
 "UseFolderNameInNamespace":("Settings.PocoFolder#settingsusefoldernameinnamespace","Makes the namespace follow the output folder"),

 # Output layout
 "GenerateSeparateFiles":("Settings.GenerateSeparateFiles","One file per class instead of one big Database.cs"),
 "ContextFolder":("Settings.PocoFolder","Sub-folder for the DbContext"),
 "InterfaceFolder":("Settings.PocoFolder","Sub-folder for the interface"),
 "PocoFolder":("Settings.PocoFolder","Sub-folder for the entity classes"),
 "PocoConfigurationFolder":("Settings.PocoFolder","Sub-folder for the configuration classes"),
 "OwnedEntityFolder":("Settings.PocoFolder","Sub-folder for owned entity classes"),
 "EnumsFileName":("Settings.EnumsFileName","One file for every enum instead of one file per enum"),
 "ElementsToGenerate":("Settings.ElementsToGenerate","Which kinds of class to generate at all"),

 # DbContext
 "DbContextName":("Settings.DbContextName","The generated context class name"),
 "DbContextInterfaceName":("Settings.DbContextName#overriding-just-the-interface","The interface name; empty string means no interface"),
 "DbContextBaseClass":("Settings.DbContextBaseClass","What the context inherits from, e.g. IdentityDbContext"),
 "DbContextInterfaceBaseClasses":("Settings.DbContextBaseClass#extra-interfaces","What the interface extends"),
 "OnConfiguration":("Settings.OnConfiguration","**EF Core.** Connection string, IConfiguration, or nothing"),
 "AddParameterlessConstructorToDbContext":("Settings.AddParameterlessConstructorToDbContext","**EF 6.** Whether a parameterless constructor is generated"),
 "UseInheritedBaseInterfaceFunctions":("Settings.AddParameterlessConstructorToDbContext#settingsuseinheritedbaseinterfacefunctions","Take the interface members from a base interface instead"),
 "AddIDbContextFactory":("Settings.AddIDbContextFactory","Generates the factory `dotnet ef` looks for"),
 "AddUnitTestingDbContext":("Settings.AddUnitTestingDbContext","Generates FakeDbContext and FakeDbSet"),
 "FakeDbContextInDebugOnlyMode":("Settings.AddUnitTestingDbContext","Wraps the fakes in `#if DEBUG`"),
 "AdditionalContextInterfaceItems":("Settings.AdditionalNamespaces#settingsadditionalcontextinterfaceitems","Extra members on the context interface"),

 # Class modifiers
 "EntityClassesModifiers":("Settings.EntityClassesModifiers","Modifiers on entity classes - usually `public partial`"),
 "ConfigurationClassesModifiers":("Settings.EntityClassesModifiers","Modifiers on configuration classes"),
 "DbContextClassModifiers":("Settings.EntityClassesModifiers","Modifiers on the context. `partial` enables OnModelCreatingPartial"),
 "DbContextInterfaceModifiers":("Settings.EntityClassesModifiers","Modifiers on the interface"),
 "ResultClassModifiers":("Settings.EntityClassesModifiers","Modifiers on stored procedure return models"),
 "ConfigurationClassName":("Settings.ConfigurationClassName","Suffix for configuration classes: Configuration, Map, Mapping"),

 # POCO shape
 "UsePascalCase":("Settings.UsePascalCase","`order_line_item` becomes `OrderLineItem`"),
 "UsePascalCaseForEnumMembers":("Settings.Enumerations#settingsusepascalcaseforenummembers","The same, for enum member names"),
 "UseDataAnnotations":("Settings.UseDataAnnotations","Adds [Key], [Required], [MaxLength] and friends"),
 "UsePropertyInitialisers":("Settings.UsePropertyInitialisers","Property initialisers instead of a constructor"),
 "UseLazyLoading":("Settings.UseLazyLoading","Marks navigation properties `virtual`"),
 "UsePrivateSetterForComputedColumns":("Settings.UsePrivateSetterForComputedColumns","`private set;` on computed columns"),
 "IncludeColumnsWithDefaults":("Settings.IncludeColumnsWithDefaults","Copies database defaults into the POCO"),
 "IncludeFieldNameConstants":("Settings.IncludeFieldNameConstants","A `const string` per property holding its own name"),
 "AllowNullStrings":("Settings.AllowNullStrings","`string?` and `#nullable enable`"),
 "NullableShortHand":("Settings.NullableShortHand","`int?` rather than `Nullable<int>`"),
 "NullableReverseNavigationProperties":("Settings.NullableShortHand#settingsnullablereversenavigationproperties","Nullable reverse navigation on one-to-one"),
 "OrderProperties":("Settings.OrderProperties","Column order or alphabetical"),
 "TableSuffix":("Settings.TableSuffix","Appends a suffix to every entity class name"),
 "CollectionType":("Settings.CollectionType","The concrete collection type: List, ObservableCollection, HashSet"),
 "CollectionInterfaceType":("Settings.CollectionType","The declared collection type: ICollection, IList"),
 "TrimCharFields":("Settings.DisableGeographyTypes#settingstrimcharfields","**EF Core.** TrimEnd() on `char` columns"),
 "DisableGeographyTypes":("Settings.DisableGeographyTypes","Skips spatial columns. **On by default**"),
 "UseMappingTables":("Settings.UseMappingTables","**EF 6.** Map many-to-many implicitly"),

 # Comments and file furniture
 "IncludeComments":("Settings.IncludeComments","Column names, keys and lengths as comments"),
 "IncludeExtendedPropertyComments":("Settings.IncludeComments#example-settingsincludeextendedpropertycomments","Your database's own column descriptions"),
 "UseRegions":("Settings.UseRegions","`#region` blocks in single-file output"),
 "UsePragma":("Settings.UsePragma","`#pragma warning disable 1591`"),
 "UseResharper":("Settings.UseResharper","`// ReSharper disable All`. **On by default**"),
 "ShowLicenseInfo":("Settings.ShowLicenseInfo","Licence details in the file header"),
 "IncludeGeneratorVersionInCode":("Settings.ShowLicenseInfo","The generator version. Needs ShowLicenseInfo too"),
 "IncludeConnectionSettingComments":("Settings.ShowLicenseInfo","Database edition and version at generation time"),
 "IncludeCodeGeneratedAttribute":("Settings.IncludeCodeGeneratedAttribute","`[GeneratedCode]` on each class"),
 "AdditionalNamespaces":("Settings.AdditionalNamespaces","Extra `using` lines"),
 "AdditionalFileHeaderText":("Settings.AdditionalFileHeaderText","Your own lines at the top of each file"),
 "AdditionalFileFooterText":("Settings.AdditionalFileHeaderText","Your own lines at the bottom"),
 "AdditionalReverseNavigationsDataAnnotations":("Settings.AdditionalNamespaces#the-two-data-annotation-arrays","Attributes on every reverse navigation"),
 "AdditionalForeignKeysDataAnnotations":("Settings.AdditionalNamespaces#the-two-data-annotation-arrays","Attributes on every foreign key property"),

 # Schema
 "PrependSchemaName":("Settings.PrependSchemaName","`sales.Order` becomes `sales_Order`"),
 "PrependSchemaNameForTable":("Settings.PrependSchemaNameForTable","Per-table control of the above"),
 "PrependSchemaNameForStoredProcedure":("Settings.PrependSchemaNameForStoredProcedure","Per-procedure control of the above"),
 "DefaultSchema":("Settings.Runtime-Values#settingsdefaultschema","The schema that is never prepended. Set by the reader"),

 # Mapping
 "GenerateHasDefaultValueSql":("Settings.GenerateHasDefaultValueSql","**EF Core.** Defaults in the EF model, not just the POCO"),
 "ColumnIdentity":("Settings.ColumnIdentity","The fluent call after ValueGeneratedOnAdd()"),
 "HiLoSequences":("Settings.ColumnIdentity#settingshilosequences","Client-side id blocks from a database sequence"),

 # Stored procedures
 "UsePropertiesForStoredProcResultSets":("Settings.UsePropertiesForStoredProcResultSets","Properties rather than fields on multi-result-set models"),
 "MergeMultipleStoredProcModelsIfAllSame":("Settings.StoredProcedureReturnTypes#settingsmergemultiplestoredprocmodelsifallsame","Collapses identical result sets into one"),
 "StoredProcedureReturnTypes":("Settings.StoredProcedureReturnTypes","Return an entity you already have"),
 "ReadStoredProcReturnObjectException":("Settings.StoredProcedureReturnTypes#settingsreadstoredprocreturnobjectexception","Handle a failed result-shape discovery"),
 "ReadStoredProcReturnObjectCompleted":("Settings.StoredProcedureReturnTypes#settingsreadstoredprocreturnobjectcompleted","Adjust a discovered result shape"),
 "StoredProcedureRename":("Settings.StoredProcedureRename","Rename the generated method"),
 "StoredProcedureReturnModelRename":("Settings.StoredProcedureReturnModelRename","Rename the generated result class"),

 # Enums
 "Enumerations":("Settings.Enumerations","Turn a lookup table's rows into a C# enum"),
 "AddEnum":("Settings.Enumerations#settingsaddenum","Decide which tables become enums, by rule"),
 "UpdateEnum":("Settings.Enumerations#updateenum-and-updateenummember","Attributes on the generated enum"),
 "UpdateEnumMember":("Settings.Enumerations#updateenum-and-updateenummember","Attributes on each enum member"),
 "AddEnumDefinitions":("Settings.Enumerations","Replace a column's type with an enum"),

 # Callbacks
 "TableRename":("Settings.TableRename","Rename a table before anything else happens to it"),
 "UpdateTable":("Settings.UpdateTable","Base classes, attributes, or drop the table"),
 "UpdateColumn":("Settings.UpdateColumn","Rename, retype, hide or annotate a column"),
 "ViewProcessing":("Settings.ViewProcessing","Declare which columns identify a row in a view"),
 "WriteInsideClassBody":("Settings.WriteInsideClassBody","Inject C# into every entity class"),
 "ForeignKeyName":("Settings.ForeignKeyName","Name the navigation properties"),
 "ForeignKeyFilterFunc":("Settings.ForeignKeyFilterFunc","Drop a relationship, or just its reverse navigation"),
 "ForeignKeyAnnotationsProcessing":("Settings.ForeignKeyAnnotationsProcessing","Attributes on navigation properties"),
 "AddExtraForeignKeys":("Settings.AddExtraForeignKeys","Relationships the database does not declare"),
 "MappingTableRename":("Settings.MappingTableRename","Name collections from a many-to-many table"),
 "AddJsonColumnMappings":("Settings.AddJsonColumnMappings","Map a JSON column to a real class"),
 "AddOwnedEntityMappings":("Settings.AddOwnedEntityMappings","Group prefixed columns into an owned entity"),

 # Runtime values
 "Root":("Settings.Runtime-Values#settingsroot","The folder holding your .tt file. Read-only"),
 "TemplateFile":("Settings.Runtime-Values#settingstemplatefile","The .tt file name. Read-only"),
 "DefaultConstructorArgument":("Settings.Runtime-Values#settingsdefaultconstructorargument","**EF 6.** What the parameterless constructor passes to base"),
}

def settings_from_source():
    s = io.open(os.path.join(GEN, 'Generator', 'Settings.cs'), encoding='utf-8-sig').read()
    seen, out = set(), []
    for m in re.finditer(r'^\s*public static (?:readonly )?([A-Za-z0-9_<>,\[\]\. ?]+?)\s+([A-Za-z_][A-Za-z0-9_]*)\s*(=|;|\{|\()', s, re.M):
        n, tail = m.group(2), m.group(3)
        if tail == '(' or n == 'Settings' or n in seen: continue
        seen.add(n); out.append(n)
    return out

def anchor_of(heading):
    """GitHub's anchor for a markdown heading: lower case, punctuation dropped, spaces to hyphens."""
    text = re.sub(r'[^\w\- ]', '', heading.strip().lower())
    return text.replace(' ', '-')

def heading_anchors(path):
    return set(anchor_of(m.group(1))
               for m in re.finditer(r'^#{1,6}\s+(.*?)\s*$', io.open(path, encoding='utf-8').read(), re.M))

def main():
    names = settings_from_source()
    missing = [n for n in names if n not in M]
    if missing:
        sys.exit("Settings with no index entry: " + ", ".join(missing))
    stale = [n for n in M if n not in names]
    if stale:
        sys.exit("Index entries for settings that no longer exist: " + ", ".join(stale))

    for link in sorted(set(p for p, _ in M.values())):
        page, _, anchor = link.partition('#')
        path = os.path.join(WIKI, page + '.md')
        if not os.path.exists(path):
            sys.exit("Index points at a page that does not exist: " + page)
        if anchor and anchor not in heading_anchors(path):
            sys.exit("Index points at a heading that does not exist: " + link)

    rows = []
    for n in sorted(names, key=str.lower):
        page, summary = M[n]
        rows.append("| [`Settings.%s`](%s) | %s |" % (n, page, summary))

    body = io.open(os.path.join(os.path.dirname(__file__), 'index_header.md'), encoding='utf-8').read()
    body += "\n".join(rows) + "\n"
    io.open(os.path.join(WIKI, 'Settings-Reference.md'), 'w', encoding='utf-8', newline='').write(body)

    # The same mapping, for BuildTT to stamp into settings-metadata so the GUI can link each setting to its page
    pages = "{\n" + ",\n".join('  "%s": "%s"' % (n, M[n][0]) for n in sorted(names, key=str.lower)) + "\n}\n"
    io.open(os.path.join(GEN, 'BuildTT', 'SettingsMetadata', 'wiki-pages.json'), 'w', encoding='utf-8', newline='').write(pages)
    print("Settings-Reference.md rebuilt: %d settings across %d pages"
          % (len(names), len(set(p.partition('#')[0] for p, _ in M.values()))))

if __name__ == '__main__':
    main()
