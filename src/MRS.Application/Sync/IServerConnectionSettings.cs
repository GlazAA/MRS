namespace MRS.Application.Sync;

public interface IServerConnectionSettings
{
	string? ServerBaseUrl { get; set; }

	DateTimeOffset? LastPullAt { get; set; }

	event Action? Changed;
}
