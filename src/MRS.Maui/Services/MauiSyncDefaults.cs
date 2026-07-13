namespace MRS.Maui.Services;

/// <summary>
/// Адрес сервера MRS.Api, вшитый в приложение. Инженеру ничего вводить не нужно.
/// После деплоя на VPS замените <see cref="ProductionServerUrl"/> — см. docs/DEPLOY_VPS.md
/// </summary>
public static class MauiSyncDefaults
{
	/// <summary>Публичный адрес API на VPS (HTTPS). Замените после развёртывания.</summary>
	public const string ProductionServerUrl = "https://api.ваш-домен.ru";

#if ANDROID
#if DEBUG
	// Эмулятор Android на ПК разработчика.
	public const string DefaultServerUrl = "http://10.0.2.2:5080";
#else
	// Release: реальный телефон, мобильный интернет → VPS в облаке.
	public const string DefaultServerUrl = ProductionServerUrl;
#endif
#else
	// Windows: локальная разработка на том же ПК, где запущен MRS.Api.
	public const string DefaultServerUrl = "http://localhost:5080";
#endif

	/// <summary>Техническая учётная запись только для API-синхронизации (не меняет локальную роль инженера).</summary>
	public const string SyncLogin = "demo";
	public const string SyncPassword = "demo123";

	/// <summary>Временно отключить синхронизацию с сервером (кнопка в меню и автозапуск при старте).</summary>
	public const bool ServerSyncEnabled = false;
}
