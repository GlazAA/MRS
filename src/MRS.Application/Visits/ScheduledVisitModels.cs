namespace MRS.Application.Visits;

public enum VisitCalendarTone
{
	Ready,
	PrepPending,
	Past
}

public sealed record ScheduledVisitCalendarItem(
	int VisitId,
	int FacilityId,
	string OrganizationName,
	string FacilityLabel,
	DateOnly Day,
	DateOnly PlannedStart,
	DateOnly? PlannedEnd,
	VisitCalendarTone Tone,
	bool HasOpenPrepNote);

public sealed record ScheduledVisitDetail(
	int VisitId,
	int OrganizationId,
	string OrganizationName,
	int FacilityId,
	string FacilityLabel,
	int? ContactEmployeeId,
	string? ContactEmployeeLabel,
	string? ContactManualText,
	DateOnly PlannedStart,
	DateOnly? PlannedEnd,
	string? Notes,
	bool PrepSkipped,
	IReadOnlyList<int> EngineerUserIds,
	IReadOnlyList<string> EngineerLabels,
	VisitCalendarTone Tone,
	IReadOnlyList<LinkedPrepNoteSummary> PrepNotes);

public sealed record LinkedPrepNoteSummary(
	int NoteId,
	string Body,
	DateOnly? DeadlineDate,
	bool IsCompleted);

public sealed record CreateScheduledVisitRequest(
	int FacilityId,
	int? ContactEmployeeId,
	string? ContactManualText,
	DateOnly PlannedStart,
	DateOnly? PlannedEnd,
	IReadOnlyList<int> EngineerUserIds,
	string? Notes);

public sealed record UpdateScheduledVisitDatesRequest(
	int VisitId,
	DateOnly PlannedStart,
	DateOnly? PlannedEnd);

public interface IScheduledVisitService
{
	Task<IReadOnlyList<ScheduledVisitCalendarItem>> GetCalendarMonthAsync(int year, int month, CancellationToken cancellationToken = default);

	Task<ScheduledVisitDetail?> GetDetailAsync(int visitId, CancellationToken cancellationToken = default);

	Task<int> CreateAsync(CreateScheduledVisitRequest request, CancellationToken cancellationToken = default);

	Task UpdateDatesAsync(UpdateScheduledVisitDatesRequest request, CancellationToken cancellationToken = default);

	Task SetPrepSkippedAsync(int visitId, bool skipped, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<VisitFilterOption>> ListForFilterAsync(CancellationToken cancellationToken = default);
}

public sealed record VisitFilterOption(int VisitId, string Label, DateOnly PlannedStart, DateOnly? PlannedEnd);
