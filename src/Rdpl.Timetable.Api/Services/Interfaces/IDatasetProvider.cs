using Rdpl.Timetable.Data;

namespace Rdpl.Timetable.Api.Services.Interfaces;

public interface IDatasetProvider
{
    LoadedDataset Load();
}
