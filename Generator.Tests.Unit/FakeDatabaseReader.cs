namespace Generator.Tests.Unit
{
    using Efrpg.Licensing;
    using Efrpg.Readers;

    public static class FakeDatabaseReader
    {
        public static EfrpgResult CreateResult()
        {
            return new EfrpgResult
            {
                HasIdentityColumnSupport   = false,
                DoNotSpecifySizeForMaxLength = false,
                CanReadStoredProcedures    = true,
                IncludeSchema              = true,
                // Licensed, so no test depends on whether the machine running it has a ReversePOCO.txt.
                Licence                    = new RawLicence { Status = LicenceStatus.Valid, LicenceType = LicenceType.Commercial }
            };
        }
    }
}
