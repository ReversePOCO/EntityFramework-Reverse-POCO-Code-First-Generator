namespace Efrpg.Licensing
{
    // Parallel copy of the enum the efrpg tool declares. It crosses the process boundary by name, never by ordinal.
    public enum LicenceStatus
    {
        Valid,
        NotFound,
        Expired,
        Invalid
    }
}
