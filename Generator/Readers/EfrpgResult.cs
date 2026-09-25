using System.Collections.Generic;
using System.Linq;

namespace Efrpg.Readers
{
    public class EfrpgResult
    {
        public string DefaultSchema          { get; set; }
        public string DatabaseDetails        { get; set; } = string.Empty;
        public bool   IncludeSchema          { get; set; } = true;
        public bool   CanReadStoredProcedures { get; set; } = false;
        public bool   HasIdentityColumnSupport { get; set; } = false;
        public bool   DoNotSpecifySizeForMaxLength { get; set; } = false;

        public RawLicence Licence { get; set; } = new RawLicence();

        public List<EfrpgError>             Errors                { get; set; } = new List<EfrpgError>();
        public List<RawTable>                Tables                { get; set; } = new List<RawTable>();
        public List<RawForeignKey>           ForeignKeys           { get; set; } = new List<RawForeignKey>();
        public List<RawIndex>                Indexes               { get; set; } = new List<RawIndex>();
        public List<RawStoredProcedure>      StoredProcedures      { get; set; } = new List<RawStoredProcedure>();
        public List<RawSequence>             Sequences             { get; set; } = new List<RawSequence>();
        public List<RawTrigger>              Triggers              { get; set; } = new List<RawTrigger>();
        public List<RawMemoryOptimisedTable> MemoryOptimisedTables { get; set; } = new List<RawMemoryOptimisedTable>();
        public List<RawExtendedProperty>     ExtendedProperties    { get; set; } = new List<RawExtendedProperty>();

        public bool HasErrors => Errors.Any();
    }
}
