using System;
using Efrpg.Licensing;

namespace Efrpg.Readers
{
    // The licence the efrpg tool read the database under. The tool enforces it; the template only reports it.
    // Defaults to a trial with no file found, so a result that never carried one cannot pass for a licensed run.
    public class RawLicence
    {
        public LicenceStatus Status       { get; set; } = LicenceStatus.NotFound;
        public string        File         { get; set; } = string.Empty;
        public string        RegisteredTo { get; set; } = string.Empty;
        public string        Company      { get; set; } = string.Empty;
        public LicenceType   LicenceType  { get; set; } = LicenceType.Trial;
        public string        NumLicences  { get; set; } = "1";
        public DateTime      ValidUntil   { get; set; } = DateTime.MaxValue;
    }
}
