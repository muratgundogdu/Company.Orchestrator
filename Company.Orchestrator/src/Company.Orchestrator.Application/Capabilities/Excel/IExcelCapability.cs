using Company.Orchestrator.Application.Artifacts;

namespace Company.Orchestrator.Application.Capabilities.Excel;

/// <summary>
/// Capability for reading and writing Excel workbooks.
/// Implementations back this with EPPlus, ClosedXML, or NPOI.
/// </summary>
public interface IExcelCapability : ICapability
{
    /// <summary>Creates a new empty workbook artifact with the given name.</summary>
    Task<ArtifactReference> CreateWorkbookAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Returns the list of sheet names inside the workbook artifact.</summary>
    Task<IReadOnlyList<string>> GetSheetNamesAsync(ArtifactReference workbook, CancellationToken cancellationToken = default);

    /// <summary>Reads all rows of a sheet as a list of column-name → value dictionaries.</summary>
    Task<IReadOnlyList<Dictionary<string, object?>>> ReadSheetAsync(
        ArtifactReference workbook,
        string sheetName = "Sheet1",
        bool hasHeaderRow = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes rows into the specified sheet.
    /// Returns an updated workbook artifact (the original is not mutated in the store).
    /// </summary>
    Task<ArtifactReference> WriteSheetAsync(
        ArtifactReference workbook,
        string sheetName,
        IEnumerable<Dictionary<string, object?>> rows,
        bool includeHeader = true,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a single named cell value.</summary>
    Task<object?> ReadCellAsync(ArtifactReference workbook, string sheetName, string cellAddress, CancellationToken cancellationToken = default);

    /// <summary>Sets a single cell value and returns an updated workbook artifact.</summary>
    Task<ArtifactReference> WriteCellAsync(ArtifactReference workbook, string sheetName, string cellAddress, object? value, CancellationToken cancellationToken = default);

    /// <summary>Exports the workbook artifact to CSV text.</summary>
    Task<string> ToCsvAsync(ArtifactReference workbook, string sheetName = "Sheet1", CancellationToken cancellationToken = default);
}
