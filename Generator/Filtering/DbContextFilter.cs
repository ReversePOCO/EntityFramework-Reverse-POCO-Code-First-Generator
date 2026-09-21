using System.Collections.Generic;
using System.Linq;
using Efrpg.Readers;

namespace Efrpg.Filtering
{
    /// <summary>
    /// Filtering is done via one or more Regex's and one or more functions, held in FilterSettings.
    /// Gone are the days of a single do-it-all regex, you can now split them up into many smaller Regex's.
    /// It's now up to you how to want to mix and match them.
    /// </summary>
    public class DbContextFilter : IDbContextFilter
    {
        public string SubNamespace { get; set; }
        public Tables Tables { get; set; }
        public List<StoredProcedure> StoredProcs { get; set; }
        public List<Enumeration> Enums { get; set; }
        public List<RawSequence> Sequences { get; set; }
        public bool IncludeViews { get; set; }
        public bool IncludeSynonyms { get; set; }
        public bool IncludeStoredProcedures { get; set; }
        public bool IncludeTableValuedFunctions { get; set; }
        public bool IncludeScalarValuedFunctions { get; set; }

        public List<EnumDefinition> EnumDefinitions { get; set; }
        public List<JsonColumnMapping> JsonColumnMappings;
        public List<OwnedEntityMapping> OwnedEntityMappings;

        protected readonly List<IFilterType<Schema>> SchemaFilters;
        protected readonly List<IFilterType<Table>> TableFilters;
        protected readonly List<IFilterType<Column>> ColumnFilters;
        protected readonly List<IFilterType<StoredProcedure>> StoredProcedureFilters;

        private bool _hasMergedIncludeFilters;

        public DbContextFilter()
        {
            Tables = new Tables();
            StoredProcs = new List<StoredProcedure>();
            Enums = new List<Enumeration>();
            Sequences = new List<RawSequence>();
            SubNamespace = string.Empty;

            IncludeViews = FilterSettings.IncludeViews;
            IncludeSynonyms = FilterSettings.IncludeSynonyms;
            IncludeTableValuedFunctions = FilterSettings.IncludeTableValuedFunctions;
            IncludeScalarValuedFunctions = FilterSettings.IncludeScalarValuedFunctions;
            IncludeStoredProcedures = IncludeScalarValuedFunctions || IncludeTableValuedFunctions || FilterSettings.IncludeStoredProcedures;

            SchemaFilters = FilterSettings.SchemaFilters;
            TableFilters = FilterSettings.TableFilters;
            ColumnFilters = FilterSettings.ColumnFilters;
            StoredProcedureFilters = FilterSettings.StoredProcedureFilters;

            _hasMergedIncludeFilters = false;

            EnumDefinitions = new List<EnumDefinition>();
            Settings.AddEnumDefinitions?.Invoke(EnumDefinitions);

            JsonColumnMappings = new List<JsonColumnMapping>();
            Settings.AddJsonColumnMappings?.Invoke(JsonColumnMappings);

            OwnedEntityMappings = new List<OwnedEntityMapping>();
            Settings.AddOwnedEntityMappings?.Invoke(OwnedEntityMappings);
        }

        public virtual bool IsExcluded(EntityName item)
        {
            if (!_hasMergedIncludeFilters)
            {
                MergeIncludeFilters();
                _hasMergedIncludeFilters = true;
            }

            var schema = item as Schema;
            if (schema != null)
                return SchemaFilters.Any(filter => filter.IsExcluded(schema));

            var table = item as Table;
            if (table != null)
                return TableFilters.Any(filter => filter.IsExcluded(table)) || SchemaFilters.Any(filter => filter.IsExcluded(table.Schema));

            var column = item as Column;
            if (column != null)
                return ColumnFilters.Any(filter => filter.IsExcluded(column));

            var sp = item as StoredProcedure;
            if (sp != null)
                return StoredProcedureFilters.Any(filter => filter.IsExcluded(sp)) || SchemaFilters.Any(filter => filter.IsExcluded(sp.Schema));

            return false;
        }

        public virtual string TableRename(string name, string schema, bool isView)
        {
            // Callback to Settings, which can be set within <database>.tt
            if (Settings.TableRename != null)
                return Settings.TableRename(name, schema, isView);

            return name;
        }

        public virtual string MappingTableRename(string mappingTable, string tableName, string entityName)
        {
            // Callback to Settings, which can be set within <database>.tt
            if (Settings.MappingTableRename != null)
                return Settings.MappingTableRename(mappingTable, tableName, entityName);

            return entityName;
        }

        public virtual void UpdateTable(Table table)
        {
            // Callback to Settings, which can be set within <database>.tt
            Settings.UpdateTable?.Invoke(table);

            // Apply owned entity column groupings
            Settings.ApplyOwnedEntityMappings(table, OwnedEntityMappings);
        }

        public virtual void UpdateColumn(Column column, Table table)
        {
            // Callback to Settings, which can be set within <database>.tt
            Settings.UpdateColumn?.Invoke(column, table, EnumDefinitions, JsonColumnMappings);
        }

        public virtual void AddEnum(Table table)
        {
            Settings.AddEnum?.Invoke(table);
        }

        public virtual void UpdateEnum(Enumeration enumeration)
        {
            Settings.UpdateEnum?.Invoke(enumeration);
        }

        public virtual void UpdateEnumMember(EnumerationMember enumerationMember)
        {
            Settings.UpdateEnumMember?.Invoke(enumerationMember);
        }

        public virtual void ViewProcessing(Table view)
        {
            // Callback to Settings, which can be set within <database>.tt
            Settings.ViewProcessing?.Invoke(view);
        }

        public virtual string StoredProcedureRename(StoredProcedure sp)
        {
            // Callback to Settings, which can be set within <database>.tt
            if (Settings.StoredProcedureRename != null)
                return Settings.StoredProcedureRename(sp);

            return sp.NameHumanCase; // Do nothing by default
        }

        public virtual string StoredProcedureReturnModelRename(string name, StoredProcedure sp)
        {
            // Callback to Settings, which can be set within <database>.tt
            if (Settings.StoredProcedureReturnModelRename != null)
                return Settings.StoredProcedureReturnModelRename(name, sp);

            return name; // Do nothing by default
        }

        public virtual ForeignKey ForeignKeyFilter(ForeignKey fk)
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
        }

        public virtual string[] ForeignKeyAnnotationsProcessing(Table fkTable, Table pkTable, string propName, string fkPropName)
        {
            // Callback to Settings, which can be set within <database>.tt
            if (Settings.ForeignKeyAnnotationsProcessing != null)
                return Settings.ForeignKeyAnnotationsProcessing(fkTable, pkTable, propName, fkPropName);

            return null;
        }

        private void MergeIncludeFilters()
        {
            MergeIncludeFilters(SchemaFilters);
            MergeIncludeFilters(TableFilters);
            MergeIncludeFilters(ColumnFilters);
            MergeIncludeFilters(StoredProcedureFilters);
        }

        private static void MergeIncludeFilters<T>(List<IFilterType<T>> filters)
        {
            var list = filters
                .Where(x => x.GetType() == typeof(RegexIncludeFilter))
                .Select(x => (RegexIncludeFilter) x)
                .ToList();

            if (list.Count < 2)
                return; // Nothing to merge

            var singleRegex = string.Join("|", list.Select(x => x.Pattern()));
            filters.RemoveAll(filter => filter.GetType() == typeof(RegexIncludeFilter));
            var singleIncludeFilter = (IFilterType<T>) new RegexIncludeFilter(singleRegex);
            filters.Add(singleIncludeFilter);
        }
    }
}
