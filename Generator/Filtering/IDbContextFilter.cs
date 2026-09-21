using System.Collections.Generic;
using Efrpg.Readers;

namespace Efrpg.Filtering
{
    public interface IDbContextFilter
    {
        string SubNamespace { get; set; }
        Tables Tables { get; set; }
        List<StoredProcedure> StoredProcs { get; set; }
        List<Enumeration> Enums { get; set; }
        List<RawSequence> Sequences { get; set; }
        List<EnumDefinition> EnumDefinitions { get; set; }
        bool IncludeViews { get; set; }
        bool IncludeSynonyms { get; set; }
        bool IncludeStoredProcedures { get; set; }
        bool IncludeTableValuedFunctions { get; set; } // If true, you must set IncludeStoredProcedures = true, and install the "EntityFramework.CodeFirstStoreFunctions" Nuget Package.
        bool IncludeScalarValuedFunctions { get; set; } // If true, you must set IncludeStoredProcedures = true.

        bool IsExcluded(EntityName item);
        string TableRename(string name, string schema, bool isView);
        string MappingTableRename(string mappingTable, string tableName, string entityName);
        void UpdateTable(Table table);
        void UpdateColumn(Column column, Table table);
        void AddEnum(Table table);
        void UpdateEnum(Enumeration enumeration);
        void UpdateEnumMember(EnumerationMember enumerationMember);
        void ViewProcessing(Table view);
        string StoredProcedureRename(StoredProcedure sp);
        string StoredProcedureReturnModelRename(string name, StoredProcedure sp);
        ForeignKey ForeignKeyFilter(ForeignKey fk);
        string[] ForeignKeyAnnotationsProcessing(Table fkTable, Table pkTable, string propName, string fkPropName);
    }
}