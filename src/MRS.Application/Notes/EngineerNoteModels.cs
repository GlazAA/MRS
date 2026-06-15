namespace MRS.Application.Notes;

public sealed record EngineerNoteListItem(
	int NoteId,
	string? Title,
	string BodyPreview,
	DateOnly? DeadlineDate,
	bool IsCompleted,
	DateTimeOffset? CompletedAt,
	DateTimeOffset UpdatedAt,
	int? FacilityId,
	string? FacilityLabel,
	int? ScheduledVisitId,
	string? VisitLabel,
	int? ChecklistId,
	string? ChecklistLabel,
	int AuthorUserId,
	string AuthorLabel);

public sealed record EngineerNoteDetail(
	int NoteId,
	string? Title,
	string Body,
	DateOnly? DeadlineDate,
	bool IsCompleted,
	DateTimeOffset? CompletedAt,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt,
	int? FacilityId,
	string? FacilityLabel,
	int? ScheduledVisitId,
	string? VisitLabel,
	int? ChecklistId,
	string? ChecklistLabel,
	int AuthorUserId,
	string AuthorLabel,
	IReadOnlyList<EngineerNoteRevision> Revisions);

public sealed record EngineerNoteRevision(
	int RevisionId,
	string Body,
	DateOnly? DeadlineDate,
	int EditedByUserId,
	string EditedByLabel,
	DateTimeOffset EditedAt);

public sealed record EngineerNoteFilter(
	int? FacilityId,
	int? ScheduledVisitId,
	int? ChecklistId,
	DateOnly? DeadlineOnOrBefore,
	DateOnly? DeadlineOnDay);

public sealed record CreateEngineerNoteRequest(
	int AuthorUserId,
	string Body,
	DateOnly? DeadlineDate,
	string? Title,
	int? FacilityId,
	int? ScheduledVisitId,
	int? ChecklistId);

public sealed record UpdateEngineerNoteRequest(
	int NoteId,
	int EditorUserId,
	string Body,
	DateOnly? DeadlineDate,
	string? Title,
	int? FacilityId,
	int? ScheduledVisitId,
	int? ChecklistId);

public interface IEngineerNoteService
{
	Task<IReadOnlyList<EngineerNoteListItem>> ListAsync(EngineerNoteFilter filter, CancellationToken cancellationToken = default);

	Task<EngineerNoteDetail?> GetDetailAsync(int noteId, CancellationToken cancellationToken = default);

	Task<int> CreateAsync(CreateEngineerNoteRequest request, CancellationToken cancellationToken = default);

	Task UpdateAsync(UpdateEngineerNoteRequest request, CancellationToken cancellationToken = default);

	Task DeleteAsync(int noteId, CancellationToken cancellationToken = default);

	Task SetCompletedAsync(int noteId, bool completed, CancellationToken cancellationToken = default);
}
