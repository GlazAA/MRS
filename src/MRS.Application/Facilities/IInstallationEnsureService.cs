namespace MRS.Application.Facilities;

/// <summary>Создаёт или находит установку по системе, типу оборудования и подписи (номер/имя).</summary>
public interface IInstallationEnsureService
{
	Task<int> EnsureAsync(int facilitySystemId, int equipmentTypeId, string unitLabel, CancellationToken cancellationToken = default);
}
