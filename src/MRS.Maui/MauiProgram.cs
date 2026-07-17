using Microsoft.Extensions.Logging;
using MRS.Application.Admin;
using MRS.Application.Checklists;
using MRS.Application.Contacts;
using MRS.Application.Facilities;
using MRS.Application.Notes;
using MRS.Application.Security;
using MRS.Application.Storage;
using MRS.Application.Sync;
using MRS.Application.Users;
using MRS.Application.Visits;
using MRS.Infrastructure.Sqlite;
using MRS.Maui.Services;

namespace MRS.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// Корень конфигурации приложения MAUI + Blazor.
		// Здесь важно регистрировать все сервисы, чтобы они были доступны через DI.
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddSingleton<ICurrentUserSession, MauiCurrentUserSession>();
		builder.Services.AddSingleton<IAdminSupportRequestService, SqliteAdminSupportRequestService>();
		builder.Services.AddSingleton<ISqlConsoleService, SqliteSqlConsoleService>();
		builder.Services.AddSingleton<ILocalDatabasePath, MauiDatabasePathProvider>();
		builder.Services.AddSingleton<ILocalDatabaseBootstrapper, SqliteDatabaseBootstrapper>();
		builder.Services.AddSingleton<ILocalDatabaseBackupService, SqliteLocalDatabaseBackupService>();
		builder.Services.AddSingleton<IFacilityHierarchyService, SqliteFacilityHierarchyService>();
		builder.Services.AddSingleton<IEquipmentTypeCatalogService, SqliteEquipmentTypeCatalogService>();
		builder.Services.AddSingleton<IChecklistSummaryService, SqliteChecklistSummaryService>();
		builder.Services.AddSingleton<IChecklistFlowService, SqliteChecklistFlowService>();
		builder.Services.AddSingleton<IChecklistTemplateAuthoringService, SqliteChecklistTemplateAuthoringService>();
		builder.Services.AddSingleton<IChecklistSaveService, SqliteChecklistSaveService>();
		builder.Services.AddSingleton<IInstallationQueryService, SqliteInstallationQueryService>();
		builder.Services.AddSingleton<IInstallationEnsureService, SqliteInstallationEnsureService>();
		builder.Services.AddSingleton<IEquipmentModelCatalogService, SqliteEquipmentModelCatalogService>();
		builder.Services.AddSingleton<IObjectOnboardingService, SqliteObjectOnboardingService>();
		builder.Services.AddSingleton<IOrganizationDirectoryService, SqliteOrganizationDirectoryService>();
		builder.Services.AddSingleton<IChecklistManagementService, SqliteChecklistManagementService>();
		builder.Services.AddSingleton<IChecklistEditService, SqliteChecklistEditService>();
		builder.Services.AddSingleton<IScheduledVisitService, SqliteScheduledVisitService>();
		builder.Services.AddSingleton<IEngineerDirectoryService, SqliteEngineerDirectoryService>();
		builder.Services.AddSingleton<IChecklistTemplateOptionService, SqliteChecklistTemplateOptionService>();
		builder.Services.AddSingleton<IEngineerNoteService, SqliteEngineerNoteService>();
		builder.Services.AddSingleton<IOrganizationEmployeeQueryService, SqliteOrganizationEmployeeQueryService>();
		builder.Services.AddSingleton<ISyncOutboxQueryService, SqliteSyncOutboxService>();
		builder.Services.AddSingleton<ISyncOutboxService, SqliteSyncOutboxService>();
		builder.Services.AddSingleton<IChecklistSyncPayloadService, SqliteChecklistSyncPayloadService>();
		builder.Services.AddSingleton<ISyncApplyService, SqliteSyncApplyService>();
		builder.Services.AddSingleton<ISyncPushAckService, SqliteSyncPushAckService>();
		builder.Services.AddSingleton<IServerConnectionSettings, MauiServerConnectionSettings>();
		builder.Services.AddSingleton<MauiUserAuthService>();
		builder.Services.AddSingleton<IUserAuthService>(sp => sp.GetRequiredService<MauiUserAuthService>());
		builder.Services.AddSingleton<IServerSyncService, MauiServerSyncService>();
		builder.Services.AddSingleton<MauiAppStartupService>();
		// Регистрация сервиса экспорта DOC/ZIP.
		// Если появится другая реализация (например, API-based), меняется только эта строка.
		builder.Services.AddSingleton<IChecklistDocumentExportService, SqliteChecklistDocumentExportService>();
		builder.Services.AddSingleton<IActAssemblyPrototypeService, SqliteActAssemblyPrototypeService>();
		builder.Services.AddSingleton<IAppFileSaveService, MauiAppFileSaveService>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
