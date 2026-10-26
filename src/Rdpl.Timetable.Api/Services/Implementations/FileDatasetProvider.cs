using Microsoft.Extensions.Options;
using Rdpl.Timetable.Api.Options;
using Rdpl.Timetable.Api.Services.Interfaces;
using Rdpl.Timetable.Data;

namespace Rdpl.Timetable.Api.Services.Implementations;

/// <summary>
/// Reads the client package from the server-configured folders with the existing Core <see cref="DatasetLoader"/>
/// (path, size, symlink and XXE guards included). Clients can never supply paths.
/// </summary>
public sealed class FileDatasetProvider : IDatasetProvider
{
    private readonly IOptionsMonitor<TimetableApiOptions> _options;
    private readonly IWebHostEnvironment _environment;

    public FileDatasetProvider(IOptionsMonitor<TimetableApiOptions> options, IWebHostEnvironment environment)
    {
        _options = options;
        _environment = environment;
    }

    public LoadedDataset Load() => DatasetLoader.Load(BuildPaths());

    private DatasetPaths BuildPaths()
    {
        var o = _options.CurrentValue;
        var allowedRoot = Path.GetFullPath(Path.IsPathRooted(o.AllowedRoot) ? o.AllowedRoot : Path.Combine(_environment.ContentRootPath, o.AllowedRoot));
        return new DatasetPaths
        {
            AllowedRoot = allowedRoot,
            DataRoot = o.DataRoot,
            AssignmentsWorkbook = o.AssignmentsWorkbook,
            AssignmentsSheet = o.AssignmentsSheet,
            ConfigRoot = o.ConfigRoot,
        };
    }
}
