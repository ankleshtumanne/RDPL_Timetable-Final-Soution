namespace Rdpl.Timetable.Api.DTOs;

public sealed record PagedResultDto<T>(IReadOnlyList<T> Items, int Skip, int Take, int Total, string Storage);
