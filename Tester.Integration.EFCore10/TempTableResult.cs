namespace V10EfrpgTest;

/// <summary>
///     The rows dbo.MultipleReturnColumnsFromTempTable returns. Discovery cannot read them because they come from a temp
///     table, so EfrpgTest.tt maps the procedure to this class with Settings.StoredProcedureReturnTypes (#833).
/// </summary>
public class TempTableResult
{
    public int Col1 { get; set; }
    public int Col2 { get; set; }
}
