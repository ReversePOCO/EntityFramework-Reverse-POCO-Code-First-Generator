using System;
using System.Collections.Generic;
using System.Linq;

namespace Efrpg
{
    public class Tables : List<Table>
    {
        /// <summary>
        ///     Finds a table by its database name, preferring an exact match.
        /// </summary>
        /// <remarks>
        ///     Two tables in one schema can legitimately differ only by case - PostgreSQL's <c>categories</c> and
        ///     <c>"CATEGORIES"</c>, MySQL on a case sensitive filesystem, Oracle or SQL Server with quoted or
        ///     case sensitive identifiers. A case insensitive match finds both, so this used SingleOrDefault and
        ///     threw; LoadTables() caught that, abandoned the rest of the model, and every table ended up reported
        ///     as having no primary key because SetPrimaryKeys() never ran.
        ///     The exact match is tried first so indexes, triggers, comments and foreign keys attach to the right
        ///     one of the pair. The case insensitive pass remains as a fallback for the dialects that fold case,
        ///     where the catalogue may report a different casing from the one used to create the object.
        /// </remarks>
        public Table GetTable(string tableName, string schema)
        {
            var exact = this.FirstOrDefault(x =>
                string.Equals(x.DbName, tableName, StringComparison.Ordinal) &&
                string.Equals(x.Schema.DbName, schema, StringComparison.Ordinal));

            if (exact != null)
                return exact;

            return this.FirstOrDefault(x =>
                string.Compare(x.DbName, tableName, StringComparison.OrdinalIgnoreCase) == 0 &&
                string.Compare(x.Schema.DbName, schema, StringComparison.OrdinalIgnoreCase) == 0);
        }

        public void IdentifyMappingTables(List<ForeignKey> fkList, bool checkForFkNameClashes, bool includeSchema)
        {
            foreach (var tbl in this.Where(x => x.HasForeignKey))
            {
                tbl.IdentifyMappingTable(fkList, this, checkForFkNameClashes, includeSchema);
            }
        }

        public void ResetNavigationProperties()
        {
            foreach (var tbl in this)
            {
                tbl.ResetNavigationProperties();
            }
        }
    }
}