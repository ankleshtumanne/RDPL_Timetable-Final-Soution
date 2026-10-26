using Rdpl.Timetable.Data;

namespace Rdpl.Timetable.Api.Services.Interfaces;

/// <summary>
/// Source of solver input. The only implementation reads the configured client files (JSON, markdown, Excel) with the
/// existing Core loader: the database stores results but is never used as input.
/// </summary>
public interface IDatasetProvider
{
    LoadedDataset Load();
}
