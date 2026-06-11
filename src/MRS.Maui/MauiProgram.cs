using Microsoft.Extensions.Logging;
using MRS.Application.Admin;
using MRS.Application.Checklists;
using MRS.Application.Facilities;
using MRS.Application.Security;
using MRS.Application.Storage;
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
		builder.Services.AddSingleton<IFacilityHierarchyService, SqliteFacilityHierarchyService>();
		builder.Services.AddSingleton<IEquipmentTypeCatalogService, SqliteEquipmentTypeCatalogService>();
		builder.Services.AddSingleton<IChecklistSummaryService, SqliteChecklistSummaryService>();
		builder.Services.AddSingleton<IChecklistFlowService, SqliteChecklistFlowService>();
		builder.Services.AddSingleton<IChecklistTemplateAuthoringService, SqliteChecklistTemplateAuthoringService>();
		builder.Services.AddSingleton<IChecklistSaveService, SqliteChecklistSaveService>();
		builder.Services.AddSingleton<IInstallationQueryService, SqliteInstallationQueryService>();
		builder.Services.AddSingleton<IInstallationEnsureService, SqliteInstallationEnsureService>();
		builder.Services.AddSingleton<IObjectOnboardingService, SqliteObjectOnboardingService>();
		builder.Services.AddSingleton<IChecklistManagementService, SqliteChecklistManagementService>();
		builder.Services.AddSingleton<IChecklistEditService, SqliteChecklistEditService>();
		// Регистрация сервиса экспорта DOC/ZIP.
		// Если появится другая реализация (например, API-based), меняется только эта строка.
		builder.Services.AddSingleton<IChecklistDocumentExportService, SqliteChecklistDocumentExportService>();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
