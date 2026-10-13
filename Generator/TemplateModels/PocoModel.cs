using System.Collections.Generic;

namespace Efrpg.TemplateModels
{
    public class PocoModel
    {
        public bool UseHasNoKey { get; set; }
        public bool HasNoPrimaryKey { get; set; }
        public string Name { get; set; }
        public string NameHumanCaseWithSuffix { get; set; }
        public string ClassModifier { get; set; }
        public string ClassComment { get; set; }
        public string ExtendedComments { get; set; }
        public string ClassAttributes { get; set; }
        public string BaseClasses { get; set; }
        public string InsideClassBody { get; set; }
        public List<PocoColumnModel> Columns { get; set; }
        public bool HasReverseNavigation { get; set; }
        public List<PocoReverseNavigationPropertyModel> ReverseNavigationProperty { get; set; }
        public bool HasForeignKey { get; set; }
        public string ForeignKeyTitleComment { get; set; }
        public List<PocoForeignKeyModel> ForeignKeys { get; set; }
        public bool CreateConstructor { get; set; }
        public List<PocoColumnsWithDefaultsModel> ColumnsWithDefaults { get; set; }
        public List<string> ReverseNavigationCtor { get; set; }
        public bool EntityClassesArePartial { get; set; }
        public bool HasHierarchyId { get; set; }
        public bool HasSpatial { get; set; }
        public bool HasSqlVector { get; set; }
        public List<string> ColumnTypeNamespaces { get; set; } = new List<string>();
        public bool HasOwnedEntities { get; set; }
        public List<PocoOwnedEntityModel> OwnedEntities { get; set; }
    }

    public class PocoColumnModel
    {
        public bool AddNewLineBefore { get; set; }
        public bool HasSummaryComments { get; set; }
        public string SummaryComments { get; set; }
        public List<string> Attributes { get; set; }
        public bool OverrideModifier { get; set; }
        public bool IncludeFieldNameConstants { get; set; }
        public string WrapIfNullable { get; set; }
        public string NameHumanCase { get; set; }
        public string PrivateSetterForComputedColumns { get; set; }
        public string PropertyInitialisers { get; set; }
        public string InlineComments { get; set; }
        public bool IsPartial { get; set; }
    }

    public class PocoReverseNavigationPropertyModel
    {
        public bool ReverseNavHasComment { get; set; }
        public string ReverseNavComment { get; set; }
        public string[] AdditionalReverseNavigationsDataAnnotations { get; set; }
        public string[] AdditionalDataAnnotations { get; set; }
        public string Definition { get; set; }
    }

    public class PocoForeignKeyModel
    {
        public bool HasFkComment { get; set; }
        public string FkComment { get; set; }
        public string[] AdditionalForeignKeysDataAnnotations { get; set; }
        public string[] AdditionalDataAnnotations { get; set; }
        public string Definition { get; set; }
    }

    public class PocoColumnsWithDefaultsModel
    {
        public string NameHumanCase { get; set; }
        public string Default { get; set; }
    }

    public class PocoOwnedEntityModel
    {
        public string PropertyType { get; set; }
        public string PropertyName { get; set; }
        public string PropertyInitialiser { get; set; }
    }
}