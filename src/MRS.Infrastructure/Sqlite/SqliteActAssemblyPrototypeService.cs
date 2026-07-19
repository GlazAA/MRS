using System.Globalization;
using System.IO.Compression;
using System.Text;
using MRS.Application;
using MRS.Application.Checklists;
using MRS.Application.Facilities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MRS.Infrastructure.Sqlite;

/// <summary>
/// Прототип сборки актов из нескольких КЛ.
/// Не меняет схему БД: читает ответы через IChecklistDocumentExportService.
/// </summary>
public sealed class SqliteActAssemblyPrototypeService : IActAssemblyPrototypeService
{
	private const string BrandRed = "#E31E24";
	private const string LogoResourceName = "MRS.Infrastructure.Resources.brand-schutz-logo.png";
	private static readonly Lock LogoLock = new();
	private static string? _logoDataUri;

	private static readonly (string Label, string[] Keywords)[] InstallationWorkCatalog =
	[
		("Проверка газоразделительного модуля", ["газораздел", "грм"]),
		("Проверка холодильного осушителя", ["осушит"]),
		("Проверка винтового компрессора", ["компресс"]),
		("Проверка фильтров тонкой/грубой очистки", ["фильтр"]),
		("Проверка циклонного сепаратора", ["циклон", "сепаратор"]),
		("Проверка конденсатоотводчиков", ["конденсат"]),
		("Проверка водомасляного сепаратора", ["водомасл", "вмс"]),
		("Проверка ресиверов воздуха/N2", ["ресивер"]),
		("Проверка адсорбера", ["адсорб", "угольн"]),
		("Проверка манометров", ["манометр"]),
		("Проверка трубопроводов", ["трубопровод"]),
		("Проверка запорной арматуры", ["запорн", "арматур"]),
		("Проверка шкафа управления", ["шкаф", "цшу", "шузз"]),
		("Проверка ПЭД / электродвигателя", ["пэд", "электродвигател", "двигател"])
	];

	private readonly IChecklistManagementService _management;
	private readonly IChecklistDocumentExportService _export;

	public SqliteActAssemblyPrototypeService(
		IChecklistManagementService management,
		IChecklistDocumentExportService export)
	{
		_management = management;
		_export = export;
	}

	public async Task<ActAssemblyPreview> BuildPreviewAsync(
		IReadOnlyList<int> checklistIds,
		CancellationToken cancellationToken = default)
	{
		var idSet = checklistIds.Where(id => id > 0).Distinct().ToHashSet();
		if (idSet.Count == 0)
			throw new InvalidOperationException("Не выбраны контрольные листы.");

		var all = await _management.GetAllAsync(cancellationToken).ConfigureAwait(false);
		var rows = all.Where(r => idSet.Contains(r.ChecklistId)).ToList();
		if (rows.Count == 0)
			throw new InvalidOperationException("Выбранные контрольные листы не найдены.");

		var models = new List<(ChecklistManagementRow Row, ChecklistDocumentExportModel Model)>();
		foreach (var row in rows.OrderBy(r => r.StartedAt).ThenBy(r => r.ChecklistId))
		{
			var model = await _export.GetDocumentModelAsync(row.ChecklistId, cancellationToken).ConfigureAwait(false);
			models.Add((row, model));
		}

		var installation = BuildInstallationDraft(rows, models);
		var units = models.Select(m => BuildUnitDraft(m.Row, m.Model)).ToList();

		return new ActAssemblyPreview(rows, installation, units);
	}

	public ChecklistDocumentExportFile RenderDraftDoc(ActDraft draft)
	{
		var html = BuildSimpleHtml(draft);
		var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
		var profileToken = draft.Profile switch
		{
			ActBlankProfile.Dryer => "dryer",
			ActBlankProfile.Compressor => "compressor",
			_ => "installation"
		};
		var unit = Sanitize(draft.InstallationLabel);
		var fileName = $"act_{profileToken}_{unit}_{stamp}.doc";
		var payload = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(html)).ToArray();
		return new ChecklistDocumentExportFile(fileName, "application/msword", payload);
	}

	public ChecklistDocumentExportFile RenderDraftPdf(ActDraft draft)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		EnsurePdfFont();

		var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
		var profileToken = draft.Profile switch
		{
			ActBlankProfile.Dryer => "dryer",
			ActBlankProfile.Compressor => "compressor",
			_ => "installation"
		};
		var unitToken = Sanitize(draft.InstallationLabel);
		var fileName = $"act_{profileToken}_{unitToken}_{stamp}.pdf";

		var subtitle = draft.Profile switch
		{
			ActBlankProfile.Dryer => "Технического обслуживания осушителя",
			ActBlankProfile.Compressor => "Технического обслуживания компрессора",
			_ => "Технического обслуживания компонентов установки"
		};

		var hours = string.IsNullOrWhiteSpace(draft.OperatingHours) ? "___________" : draft.OperatingHours!;
		var objectAddress = string.IsNullOrWhiteSpace(draft.ObjectAddress) ? "___________" : draft.ObjectAddress;
		var unit = string.IsNullOrWhiteSpace(draft.InstallationLabel) ? "___________" : draft.InstallationLabel;
		var dates = string.IsNullOrWhiteSpace(draft.WorkDates) ? "___________" : draft.WorkDates;
		var customer = string.IsNullOrWhiteSpace(draft.Customer) ? "___________" : draft.Customer;
		var workKind = string.IsNullOrWhiteSpace(draft.WorkKind) ? "___________" : draft.WorkKind;
		var logoBytes = GetLogoBytes();
		var brandRed = Color.FromHex(BrandRed);

		var mid = (draft.WorkLines.Count + 1) / 2;
		var leftWorks = draft.WorkLines.Take(mid).ToList();
		var rightWorks = draft.WorkLines.Skip(mid).ToList();
		var workRows = Math.Max(leftWorks.Count, rightWorks.Count);

		var (stateMidLeft, stateMidRight) = draft.Profile switch
		{
			ActBlankProfile.Dryer => ("Осушитель в работе", "Осушитель выключен"),
			ActBlankProfile.Installation => ("Установка под нагрузкой", "Установка выключена"),
			_ => ("Компрессор под нагрузкой", "Компрессор выключен")
		};

		var bytes = Document.Create(container =>
		{
			container.Page(page =>
			{
				page.MarginTop(56);
				page.MarginRight(56);
				page.MarginBottom(56);
				page.MarginLeft(91); // 3.2 см
				page.DefaultTextStyle(x => x.FontFamily(_pdfFontFamily).FontSize(10));
				page.Content().Column(col =>
				{
					col.Spacing(4);

					// Шапка: логотип слева, контакты прижаты к правому краю, тонкая красная полоса
					col.Item().Row(row =>
					{
						row.ConstantItem(95).Height(95).Element(e =>
						{
							if (logoBytes is { Length: > 0 })
								e.Image(logoBytes).FitArea();
							else
								e.Text("Brand Schutz").Bold().FontSize(12).FontColor(brandRed);
						});

						row.RelativeItem().AlignRight().Element(right =>
						{
							right.Width(178).Row(contactRow =>
							{
								contactRow.ConstantItem(0.8f).Background(brandRed);
								contactRow.RelativeItem().PaddingLeft(6).Column(c =>
								{
									c.Spacing(1);
									foreach (var line in new[]
									         {
										         "ООО «Бранд Шутц»",
										         "+7(495) 363-8916",
										         "117648, г. Москва, вн.тер.г.",
										         "Муниципальный Округ Чертаново",
										         "Северное, мкр. Северное Чертаново,",
										         "д. 4, к. 402, пом. 6/2Т"
									         })
									{
										c.Item().Text(line).FontSize(9).FontColor(brandRed);
									}
								});
							});
						});
					});

					col.Item().PaddingTop(10).AlignCenter().Text("АКТ _____ /_____").Bold().FontSize(14);
					col.Item().AlignCenter().Text(subtitle).Bold().FontSize(12);

					col.Item().PaddingTop(8).Row(r =>
					{
						r.RelativeItem().Element(e => MetaField(e, "Заказчик:", customer));
						r.ConstantItem(12);
						r.RelativeItem().Element(e => MetaField(e, "Дата проведения работ:", dates));
					});

					if (draft.Profile == ActBlankProfile.Installation)
					{
						col.Item().Element(e => MetaField(e, "Объект:", objectAddress));
						col.Item().Row(r =>
						{
							r.RelativeItem().Element(e => MetaField(e, "Часы эксплуатации:", hours));
							r.ConstantItem(12);
							r.RelativeItem().Element(e => MetaField(e, "Номер установки:", unit));
						});
					}
					else
					{
						col.Item().Element(e => MetaField(e, "Объект:", objectAddress));
						col.Item().Row(r =>
						{
							r.RelativeItem().Element(e => MetaField(e, "Тип оборудования:", draft.EquipmentTypeName ?? "___________"));
							r.ConstantItem(12);
							r.RelativeItem().Element(e => MetaField(e, "Модель:", string.IsNullOrWhiteSpace(draft.ModelName) ? "___________" : draft.ModelName!));
						});
						col.Item().Row(r =>
						{
							r.RelativeItem().Element(e => MetaField(e, "Серийный номер:", string.IsNullOrWhiteSpace(draft.SerialNumber) ? "___________" : draft.SerialNumber!));
							r.ConstantItem(12);
							r.RelativeItem().Element(e => MetaField(e, "Номер установки:", unit));
						});
						col.Item().Element(e => MetaField(e, "Часы эксплуатации:", hours));
					}

					col.Item().Element(e => MetaField(e, "Вид работ:", workKind));

					var state = ParseEquipmentState(draft.EquipmentStateDisplay, draft.Profile);

					// Состояние оборудования — галочки рисуем явно (Unicode ☐/☑ в PDF часто не видно)
					col.Item().PaddingTop(8).Text("Состояние оборудования:").Bold();
					col.Item().Border(1).BorderColor(Colors.Black).Padding(6).Row(r =>
					{
						r.RelativeItem().Column(c =>
						{
							c.Spacing(3);
							PdfStateLine(c, state.Working, "Рабочее на дату прибытия");
							PdfStateMidLine(c, state.UnderLoad, stateMidLeft, state.Off, stateMidRight);
							PdfStateLine(c, state.NotWorking, "Не рабочее на дату прибытия");
						});
						r.ConstantItem(8);
						r.RelativeItem().Column(c =>
						{
							c.Spacing(3);
							PdfStateLine(c, state.Working, "Рабочее на дату убытия");
							PdfStateMidLine(c, state.UnderLoad, stateMidLeft, state.Off, stateMidRight);
							PdfStateLine(c, state.NotWorking, "Не рабочее на дату убытия");
						});
					});

					col.Item().PaddingTop(8).Text("Перечень выполненных работ:").Bold();
					col.Item().Table(table =>
					{
						table.ColumnsDefinition(c =>
						{
							c.RelativeColumn(4);
							c.ConstantColumn(28);
							c.RelativeColumn(4);
							c.ConstantColumn(28);
						});

						for (var i = 0; i < workRows; i++)
						{
							var l = i < leftWorks.Count ? leftWorks[i] : null;
							var rr = i < rightWorks.Count ? rightWorks[i] : null;
							table.Cell().Border(0.5f).Padding(3).Text(l?.Label ?? " ");
							table.Cell().Border(0.5f).AlignCenter().AlignMiddle().Text(l?.Mark ?? " ");
							table.Cell().Border(0.5f).Padding(3).Text(rr?.Label ?? " ");
							table.Cell().Border(0.5f).AlignCenter().AlignMiddle().Text(rr?.Mark ?? " ");
						}

						if (draft.Profile == ActBlankProfile.Compressor)
						{
							table.Cell().ColumnSpan(4).Border(0.5f).Padding(3)
								.Text("К — контроль; Ч — чистка; З — замена; П — параметры; — — не выполн.").FontSize(8);
						}
					});

					col.Item().PaddingTop(8).Text("ДОПОЛНИТЕЛЬНЫЕ РАБОТЫ:").Bold();
					col.Item().Element(e => ContentLinesBlock(e, draft.ExtraWorksText));

					col.Item().PaddingTop(6).Text("ЗАМЕЧАНИЯ И РЕКОМЕНДАЦИИ:").Bold();
					col.Item().Element(e => ContentLinesBlock(e, draft.RemarksText));

					col.Item().PaddingTop(14).Row(r =>
					{
						r.RelativeItem().Column(c =>
						{
							c.Item().Text("Представитель").Bold();
							c.Item().Text("ИСПОЛНИТЕЛЯ").Bold();
							c.Item().Text("Работу выполнил.");
							c.Item().PaddingTop(8).Element(e => MetaField(e, "Должность:", " "));
							c.Item().Element(e => MetaField(e, "ФИО:", " "));
							c.Item().Element(e => MetaField(e, "Подпись:", " "));
						});
						r.ConstantItem(16);
						r.RelativeItem().Column(c =>
						{
							c.Item().Text("Представитель").Bold();
							c.Item().Text("ЗАКАЗЧИКА:").Bold();
							c.Item().Text("Выполнение работ подтверждаю.");
							c.Item().PaddingTop(8).Element(e => MetaField(e, "Должность:", " "));
							c.Item().Element(e => MetaField(e, "ФИО:", " "));
							c.Item().Element(e => MetaField(e, "Подпись:", " "));
						});
					});

					col.Item().PaddingTop(18).LineHorizontal(1.5f).LineColor(brandRed);
					col.Item().PaddingTop(6).Row(r =>
					{
						r.RelativeItem().Text("www.brandschutz.ru").FontColor(brandRed);
						r.RelativeItem().AlignRight().Text("ИНН 7726589854").FontColor(brandRed);
					});
				});
			});
		}).GeneratePdf();

		return new ChecklistDocumentExportFile(fileName, "application/pdf", bytes);

		static void MetaField(IContainer container, string label, string value)
		{
			container.Column(c =>
			{
				c.Item().Text(label);
				c.Item().BorderBottom(0.7f).BorderColor(Colors.Black).PaddingBottom(2).Text(string.IsNullOrWhiteSpace(value) ? " " : value);
			});
		}

		static void ContentLinesBlock(IContainer container, string? text)
		{
			var parts = SplitContentLines(text);
			if (parts.Count == 0)
				return;

			container.Column(c =>
			{
				foreach (var line in parts)
					c.Item().PaddingTop(2).BorderBottom(0.5f).BorderColor(Colors.Grey.Medium).PaddingBottom(2).Text(line);
			});
		}

		static void PdfStateLine(ColumnDescriptor column, bool on, string label)
		{
			column.Item().Row(r =>
			{
				r.ConstantItem(11).Element(e => PdfCheckBox(e, on));
				r.ConstantItem(5);
				r.RelativeItem().AlignMiddle().Text(label).FontSize(9);
			});
		}

		static void PdfStateMidLine(
			ColumnDescriptor column,
			bool leftOn,
			string leftLabel,
			bool rightOn,
			string rightLabel)
		{
			column.Item().Row(r =>
			{
				r.RelativeItem().Row(inner =>
				{
					inner.ConstantItem(11).Element(e => PdfCheckBox(e, leftOn));
					inner.ConstantItem(4);
					inner.RelativeItem().AlignMiddle().Text(leftLabel).FontSize(8);
				});
				r.ConstantItem(4);
				r.RelativeItem().Row(inner =>
				{
					inner.ConstantItem(11).Element(e => PdfCheckBox(e, rightOn));
					inner.ConstantItem(4);
					inner.RelativeItem().AlignMiddle().Text(rightLabel).FontSize(8);
				});
			});
		}

		static void PdfCheckBox(IContainer container, bool on)
		{
			container
				.Width(11)
				.Height(11)
				.Border(0.8f)
				.BorderColor(Colors.Black)
				.AlignCenter()
				.AlignMiddle()
				.Text(on ? "V" : " ")
				.FontSize(7)
				.Bold();
		}
	}

	private static string _pdfFontFamily = "Arial";
	private static bool _pdfFontReady;
	private static readonly Lock PdfFontLock = new();

	private static void EnsurePdfFont()
	{
		lock (PdfFontLock)
		{
			if (_pdfFontReady)
				return;

			// Системные шрифты с кириллицей; RegisterFontWithCustomName — API QuestPDF 2024+.
			foreach (var path in PdfFontCandidates())
			{
				if (!File.Exists(path))
					continue;
				try
				{
					using var stream = File.OpenRead(path);
					QuestPDF.Drawing.FontManager.RegisterFontWithCustomName("MrsActFont", stream);
					_pdfFontFamily = "MrsActFont";
					_pdfFontReady = true;
					return;
				}
				catch
				{
					// пробуем следующий кандидат
				}
			}

			_pdfFontFamily = "Arial";
			_pdfFontReady = true;
		}
	}

	private static IEnumerable<string> PdfFontCandidates()
	{
		var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
		if (!string.IsNullOrEmpty(windir))
		{
			yield return Path.Combine(windir, "Fonts", "arial.ttf");
			yield return Path.Combine(windir, "Fonts", "segoeui.ttf");
		}

		yield return "/system/fonts/Roboto-Regular.ttf";
		yield return "/system/fonts/NotoSans-Regular.ttf";
		yield return "/System/Library/Fonts/Supplemental/Arial.ttf";
	}

	private static byte[]? GetLogoBytes()
	{
		var assembly = typeof(SqliteActAssemblyPrototypeService).Assembly;
		using var stream = assembly.GetManifestResourceStream(LogoResourceName);
		if (stream is null)
			return null;
		using var ms = new MemoryStream();
		stream.CopyTo(ms);
		return ms.ToArray();
	}

	public ChecklistDocumentExportFile RenderDraftsZip(IReadOnlyList<ActDraft> drafts)
	{
		if (drafts is null || drafts.Count == 0)
			throw new InvalidOperationException("Нет актов для выгрузки.");

		using var stream = new MemoryStream();
		using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
		{
			var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var index = 0;
			foreach (var draft in drafts)
			{
				index++;
				var doc = RenderDraftDoc(draft);
				var entryName = EnsureUniqueZipEntryName(doc.FileName, usedNames, index);
				var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
				using var entryStream = entry.Open();
				entryStream.Write(doc.Content, 0, doc.Content.Length);
			}
		}

		var zipName = $"acts_export_{DateTime.Now:yyyyMMdd_HHmm}.zip";
		return new ChecklistDocumentExportFile(zipName, "application/zip", stream.ToArray());
	}

	private static string EnsureUniqueZipEntryName(string fileName, HashSet<string> usedNames, int index)
	{
		var name = string.IsNullOrWhiteSpace(fileName) ? $"act_{index}.doc" : fileName.Trim();
		if (usedNames.Add(name))
			return name;

		var ext = Path.GetExtension(name);
		var stem = Path.GetFileNameWithoutExtension(name);
		var candidate = $"{stem}_{index}{ext}";
		var n = 2;
		while (!usedNames.Add(candidate))
		{
			candidate = $"{stem}_{index}_{n}{ext}";
			n++;
		}

		return candidate;
	}

	public ActAssemblyPreview BuildDemoPreview()
	{
		var started = new DateTimeOffset(2025, 3, 17, 9, 0, 0, TimeSpan.FromHours(3));
		var rows = new List<ChecklistManagementRow>
		{
			new(9001, started, "ООО «Мираторг-Курск»", "Мираторг Курск", "Винтовой компрессор", "G301", "ТО-3000", "completed"),
			new(9002, started.AddDays(1), "ООО «Мираторг-Курск»", "Мираторг Курск", "Холодильный осушитель", "G301", "ТО-1", "completed"),
			new(9003, started.AddDays(1), "ООО «Мираторг-Курск»", "Мираторг Курск", "Фильтры очистки", "G301", "Еженедельное", "completed"),
			new(9004, started.AddDays(2), "ООО «Мираторг-Курск»", "Мираторг Курск", "Конденсатоотводчики", "G301", "Еженедельное", "completed"),
			new(9005, started.AddDays(3), "ООО «Мираторг-Курск»", "Мираторг Курск", "Газоразделительный модуль", "G301", "Еженедельное", "completed")
		};

		var installation = new ActDraft
		{
			DraftKey = "demo-installation",
			Profile = ActBlankProfile.Installation,
			Title = "Акт технического обслуживания компонентов установки",
			Customer = "ООО «Мираторг-Курск»",
			WorkDates = "03/17 — 03/21/25",
			InstallationLabel = "G301",
			WorkKind = "техническое обслуживание",
			ObjectAddress = "Курск, промзона Демо-ТО, 1",
			OperatingHours = "36834",
			EquipmentStateDisplay = "Под нагрузкой",
			WorkLines = InstallationWorkCatalog
				.Select(item =>
				{
					var done = rows.Any(r => MatchesKeywords(r.EquipmentTypeName, item.Keywords));
					return new ActWorkLine(item.Label, done ? "V" : "—");
				})
				.ToList(),
			SourceChecklistIds = rows.Select(r => r.ChecklistId).ToList(),
			ExtraWorksText =
				"[Компрессор G301] Замена фильтров FE65-2P — 1 шт., FE65-2M — 1 шт.\n" +
				"[Осушитель G301] Изменение настройки CMD (слив конденсата) с CON на T/N.",
			RemarksText =
				"[Компрессор G301] Требуется: замена индикатора сервиса регулятора всасывания, замена электромагнитного клапана.\n" +
				"[Фильтры G301] Требуется: устранить негерметичности на трубопроводе (отмечены белым маркером).\n" +
				"[Конденсатоотводчики G301] Заменить блоки на конденсатоотв. BM32 — 3 шт.\n" +
				"[ГРМ G301] Заменить датчики контроля на газогенераторе PL210M."
		};

		var compressor = new ActDraft
		{
			DraftKey = "demo-unit-compressor",
			Profile = ActBlankProfile.Compressor,
			Title = "Акт технического обслуживания компрессора",
			Customer = "ООО «Мираторг-Курск»",
			WorkDates = "03/18 — 03/19/25",
			InstallationLabel = "G301",
			WorkKind = "ТО-3000",
			ObjectAddress = "Курск, промзона Демо-ТО, 1",
			EquipmentTypeName = "Винтовой компрессор",
			ModelName = "S 40-3",
			SerialNumber = "515 15 19",
			OperatingHours = "32592",
			EquipmentStateDisplay = "Под нагрузкой",
			WorkLines =
			[
				new ActWorkLine("Общий осмотр", "К"),
				new ActWorkLine("Воздушный фильтр", "З"),
				new ActWorkLine("Масляный фильтр", "З"),
				new ActWorkLine("Маслоотделитель", "З"),
				new ActWorkLine("Клапан мин. давления", "З"),
				new ActWorkLine("Охладитель", "Ч"),
				new ActWorkLine("Смазка подшипников ПЭД", "—")
			],
			SourceChecklistIds = [9001],
			ExtraWorksText =
				"Очистка от пыли узлов компрессора.\n" +
				"Замена элемента питания блока управления (CR 2032).\n" +
				"Долив масла: 8 л в резервуар и 1 л в масляный фильтр.",
			RemarksText =
				"Требуется: замена индикатора сервиса регулятора всасывания; замена электромагнитного клапана; " +
				"установка доп. сепаратора; промывка радиатора комбинированного охладителя."
		};

		var dryer = new ActDraft
		{
			DraftKey = "demo-unit-dryer",
			Profile = ActBlankProfile.Dryer,
			Title = "Акт технического обслуживания осушителя",
			Customer = "ООО «Мираторг-Курск»",
			WorkDates = "03/17 — 03/20/25",
			InstallationLabel = "G301",
			WorkKind = "ТО-1",
			ObjectAddress = "Курск, промзона Демо-ТО, 1",
			EquipmentTypeName = "Холодильный осушитель",
			ModelName = "DS 80-2",
			SerialNumber = "400119780003",
			OperatingHours = "840",
			EquipmentStateDisplay = "Осушитель в работе",
			WorkLines =
			[
				new ActWorkLine("Замена датчика давления включения вентилятора", "V"),
				new ActWorkLine("Замена датчика температуры точки росы", "V"),
				new ActWorkLine("Замена/доливка масла", "V"),
				new ActWorkLine("Замена фильтра хладагента", "V"),
				new ActWorkLine("Ремонт/замена контроллера", "V"),
				new ActWorkLine("Общий осмотр", "—")
			],
			SourceChecklistIds = [9002],
			ExtraWorksText = "Изменение настройки CMD (слив конденсата) с CON на T/N.",
			RemarksText = string.Empty
		};

		return new ActAssemblyPreview(rows, installation, [compressor, dryer]);
	}

	private static ActDraft BuildInstallationDraft(
		IReadOnlyList<ChecklistManagementRow> rows,
		IReadOnlyList<(ChecklistManagementRow Row, ChecklistDocumentExportModel Model)> models)
	{
		var first = models[0].Model.Header;
		var customer = rows.Select(r => r.OrganizationName).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
			?? first.OrganizationName;
		var unit = PickDominantInstallation(rows);
		var dates = FormatDateRange(rows);
		var workKind = rows.Select(r => r.MaintenanceTypeName).Distinct().Count() == 1
			? rows[0].MaintenanceTypeName
			: "техническое обслуживание";

		var workLines = InstallationWorkCatalog
			.Select(item =>
			{
				var done = rows.Any(r => MatchesKeywords(r.EquipmentTypeName, item.Keywords));
				return new ActWorkLine(item.Label, done ? "V" : "—");
			})
			.ToList();

		var (extra, remarks) = GlueNarrative(models);
		var hours = models
			.Select(m => FindDisplay(m.Model.Answers, "operating_hours", "hours", "runtime_hours", "fridge_hours"))
			.FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));
		var equipmentState = ResolveEquipmentStateDisplay(models);

		return new ActDraft
		{
			DraftKey = "installation",
			Profile = ActBlankProfile.Installation,
			Title = "Акт технического обслуживания компонентов установки",
			Customer = customer,
			WorkDates = dates,
			InstallationLabel = unit,
			WorkKind = workKind,
			ObjectAddress = FormatObjectAddress(first),
			OperatingHours = hours,
			EquipmentTypeName = null,
			EquipmentStateDisplay = equipmentState,
			WorkLines = workLines,
			SourceChecklistIds = rows.Select(r => r.ChecklistId).ToList(),
			ExtraWorksText = extra,
			RemarksText = remarks
		};
	}

	private static ActDraft BuildUnitDraft(ChecklistManagementRow row, ChecklistDocumentExportModel model)
	{
		var profile = DetectProfile(row.EquipmentTypeName);
		var title = profile switch
		{
			ActBlankProfile.Dryer => "Акт технического обслуживания осушителя",
			ActBlankProfile.Compressor => "Акт технического обслуживания компрессора",
			_ => $"Акт технического обслуживания ({row.EquipmentTypeName})"
		};

		var workLines = model.Answers
			.Where(IsLikelyWorkRow)
			.OrderBy(a => a.SortOrder)
			.Select(a =>
			{
				var value = string.IsNullOrWhiteSpace(a.ValueDisplay) ? a.ValueRaw : a.ValueDisplay;
				var mark = string.IsNullOrWhiteSpace(value) ? "—" : SummarizeMark(value, profile);
				return new ActWorkLine(a.QuestionText, mark);
			})
			.Take(18)
			.ToList();

		var extra = ExtractPrefixed(model.Answers, "extra_");
		var remarks = ExtractPrefixed(model.Answers, "remarks_")
			?? ExtractPrefixed(model.Answers, "comments");

		var modelName = FindDisplay(model.Answers, "comp_model", "model", "oht_model", "filter_model");
		var hours = FindDisplay(model.Answers, "operating_hours", "hours", "runtime_hours", "fridge_hours");
		var serial = FindDisplay(model.Answers, "comp_serial", "serial_number", "compressor_serial", "serial");
		var equipmentState = FindEquipmentStateDisplay(model.Answers);

		return new ActDraft
		{
			DraftKey = $"unit-{row.ChecklistId}",
			Profile = profile,
			Title = title,
			Customer = row.OrganizationName,
			WorkDates = FormatSingleDate(row.StartedAt),
			InstallationLabel = row.InstallationLabel,
			WorkKind = row.MaintenanceTypeName,
			ObjectAddress = FormatObjectAddress(model.Header),
			EquipmentTypeName = row.EquipmentTypeName,
			ModelName = modelName,
			SerialNumber = serial,
			OperatingHours = hours,
			EquipmentStateDisplay = equipmentState,
			WorkLines = workLines,
			SourceChecklistIds = [row.ChecklistId],
			ExtraWorksText = extra ?? string.Empty,
			RemarksText = remarks ?? string.Empty
		};
	}

	/// <summary>
	/// Для сводного акта: сначала состояние из компрессора, иначе первое непустое среди выбранных КЛ.
	/// </summary>
	private static string? ResolveEquipmentStateDisplay(
		IReadOnlyList<(ChecklistManagementRow Row, ChecklistDocumentExportModel Model)> models)
	{
		foreach (var (row, model) in models.OrderByDescending(m =>
			         DetectProfile(m.Row.EquipmentTypeName) == ActBlankProfile.Compressor))
		{
			var value = FindEquipmentStateDisplay(model.Answers);
			if (!string.IsNullOrWhiteSpace(value))
				return value;
		}

		return null;
	}

	private static string? FindEquipmentStateDisplay(IReadOnlyList<ChecklistDocumentAnswer> answers)
	{
		var byCode = FindDisplay(answers, "comp_state", "equipment_state", "state");
		if (!string.IsNullOrWhiteSpace(byCode))
			return byCode;

		foreach (var a in answers)
		{
			var q = a.QuestionText ?? string.Empty;
			if (!q.Contains("Состояние", StringComparison.OrdinalIgnoreCase))
				continue;
			if (!q.Contains("компресс", StringComparison.OrdinalIgnoreCase)
			    && !q.Contains("оборудован", StringComparison.OrdinalIgnoreCase)
			    && !q.Contains("осушит", StringComparison.OrdinalIgnoreCase))
				continue;

			var value = string.IsNullOrWhiteSpace(a.ValueDisplay) ? a.ValueRaw : a.ValueDisplay;
			if (!string.IsNullOrWhiteSpace(value))
				return value.Trim();
		}

		return null;
	}

	private static string FormatObjectAddress(ChecklistDocumentHeader header)
	{
		var actObject = FacilityAddressFormatter.FormatActObject(
			header.FacilityCity,
			header.FacilityStreet,
			header.FacilityBuilding,
			header.FacilityStructure,
			header.FacilityBlock);

		if (!string.IsNullOrWhiteSpace(actObject) && actObject != "—")
			return actObject;

		return string.IsNullOrWhiteSpace(header.FacilityName) ? "—" : header.FacilityName;
	}

	private static (string Extra, string Remarks) GlueNarrative(
		IReadOnlyList<(ChecklistManagementRow Row, ChecklistDocumentExportModel Model)> models)
	{
		var extras = new List<string>();
		var remarks = new List<string>();

		foreach (var (row, model) in models)
		{
			var label = ShortEquipmentLabel(row);
			var extra = ExtractPrefixed(model.Answers, "extra_");
			if (!string.IsNullOrWhiteSpace(extra))
				extras.Add($"[{label}] {extra.Trim()}");

			var rem = ExtractPrefixed(model.Answers, "remarks_")
				?? ExtractPrefixed(model.Answers, "comments");
			if (!string.IsNullOrWhiteSpace(rem))
				remarks.Add($"[{label}] {rem.Trim()}");
		}

		return (
			string.Join(Environment.NewLine, extras),
			string.Join(Environment.NewLine, remarks));
	}

	private static string ShortEquipmentLabel(ChecklistManagementRow row)
	{
		var eq = row.EquipmentTypeName;
		if (eq.Contains("компресс", StringComparison.OrdinalIgnoreCase))
			eq = "Компрессор";
		else if (eq.Contains("осушит", StringComparison.OrdinalIgnoreCase))
			eq = "Осушитель";
		return $"{eq} {row.InstallationLabel}".Trim();
	}

	private static string? ExtractPrefixed(IReadOnlyList<ChecklistDocumentAnswer> answers, string prefix)
	{
		foreach (var a in answers)
		{
			var code = a.FieldCode ?? string.Empty;
			var match = code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
				|| (prefix.Equals("comments", StringComparison.OrdinalIgnoreCase)
					&& code.Equals("comments", StringComparison.OrdinalIgnoreCase));
			if (!match)
				continue;
			var value = string.IsNullOrWhiteSpace(a.ValueDisplay) ? a.ValueRaw : a.ValueDisplay;
			if (!string.IsNullOrWhiteSpace(value))
				return value.Trim();
		}

		return null;
	}

	private static string? FindDisplay(IReadOnlyList<ChecklistDocumentAnswer> answers, params string[] codes)
	{
		foreach (var code in codes)
		{
			var a = answers.FirstOrDefault(x =>
				string.Equals(x.FieldCode, code, StringComparison.OrdinalIgnoreCase));
			if (a is null)
				continue;
			var value = string.IsNullOrWhiteSpace(a.ValueDisplay) ? a.ValueRaw : a.ValueDisplay;
			if (!string.IsNullOrWhiteSpace(value))
				return value.Trim();
		}

		return null;
	}

	private static bool IsLikelyWorkRow(ChecklistDocumentAnswer a)
	{
		var code = a.FieldCode ?? string.Empty;
		if (string.IsNullOrWhiteSpace(code))
			return false;
		if (code.StartsWith("extra_", StringComparison.OrdinalIgnoreCase))
			return false;
		if (code.StartsWith("remarks_", StringComparison.OrdinalIgnoreCase))
			return false;

		string[] skip =
		[
			"start_date", "start_time", "end_date", "end_time", "workers",
			"unit_number", "equipment_pick", "comp_model", "comp_manufacturer",
			"comp_type", "comp_serial", "serial_number", "comp_state",
			"operating_hours", "hours", "model"
		];
		return !skip.Contains(code, StringComparer.OrdinalIgnoreCase);
	}

	private static string SummarizeMark(string value, ActBlankProfile profile)
	{
		var v = value.Trim();
		if (profile == ActBlankProfile.Compressor)
		{
			if (v.StartsWith("К", StringComparison.OrdinalIgnoreCase)
				|| v.StartsWith("Ч", StringComparison.OrdinalIgnoreCase)
				|| v.StartsWith("З", StringComparison.OrdinalIgnoreCase)
				|| v.StartsWith("П", StringComparison.OrdinalIgnoreCase))
				return v[..1].ToUpperInvariant();
			if (v.Contains("замен", StringComparison.OrdinalIgnoreCase))
				return "З";
			if (v.Contains("чист", StringComparison.OrdinalIgnoreCase))
				return "Ч";
			if (string.Equals(v, "true", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(v, "да", StringComparison.OrdinalIgnoreCase))
				return "К";
			if (v is "-" or "—")
				return "—";
			return "К";
		}

		// Dryer / installation-like: галочка если есть ответ.
		if (v is "-" or "—")
			return "—";
		return "V";
	}

	private static ActBlankProfile DetectProfile(string? equipmentTypeName)
	{
		var et = equipmentTypeName ?? string.Empty;
		if (et.Contains("осушит", StringComparison.OrdinalIgnoreCase))
			return ActBlankProfile.Dryer;
		if (et.Contains("компресс", StringComparison.OrdinalIgnoreCase))
			return ActBlankProfile.Compressor;
		return ActBlankProfile.Installation;
	}

	private static bool MatchesKeywords(string equipmentTypeName, string[] keywords) =>
		keywords.Any(k => equipmentTypeName.Contains(k, StringComparison.OrdinalIgnoreCase));

	private static string PickDominantInstallation(IReadOnlyList<ChecklistManagementRow> rows)
	{
		return rows
			.GroupBy(r => r.InstallationLabel)
			.OrderByDescending(g => g.Count())
			.Select(g => g.Key)
			.FirstOrDefault() ?? "—";
	}

	private static string FormatDateRange(IReadOnlyList<ChecklistManagementRow> rows)
	{
		var dates = rows
			.Select(r => r.StartedAt?.ToLocalTime())
			.Where(d => d.HasValue)
			.Select(d => DateOnly.FromDateTime(d!.Value.DateTime))
			.Distinct()
			.OrderBy(d => d)
			.ToList();
		if (dates.Count == 0)
			return "—";
		if (dates.Count == 1)
			return MrsDateFormat.FormatDateShort(dates[0]);
		return $"{MrsDateFormat.FormatDateShort(dates[0])} — {MrsDateFormat.FormatDateShort(dates[^1])}";
	}

	private static string FormatSingleDate(DateTimeOffset? startedAt) =>
		MrsDateFormat.FormatDateShort(startedAt);

	private static string Sanitize(string? s)
	{
		if (string.IsNullOrWhiteSpace(s))
			return "unit";
		var chars = s.Trim().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray();
		return new string(chars);
	}

	private static string BuildSimpleHtml(ActDraft draft)
	{
		var logoUri = GetLogoDataUri();
		var subtitle = draft.Profile switch
		{
			ActBlankProfile.Dryer => "Технического обслуживания осушителя",
			ActBlankProfile.Compressor => "Технического обслуживания компрессора",
			_ => "Технического обслуживания компонентов установки"
		};
		var hours = string.IsNullOrWhiteSpace(draft.OperatingHours) ? "___________" : draft.OperatingHours!;
		var objectAddress = string.IsNullOrWhiteSpace(draft.ObjectAddress) ? "___________" : draft.ObjectAddress;
		var unit = string.IsNullOrWhiteSpace(draft.InstallationLabel) ? "___________" : draft.InstallationLabel;
		var dates = string.IsNullOrWhiteSpace(draft.WorkDates) ? "___________" : draft.WorkDates;
		var customer = string.IsNullOrWhiteSpace(draft.Customer) ? "___________" : draft.Customer;
		var workKind = string.IsNullOrWhiteSpace(draft.WorkKind) ? "___________" : draft.WorkKind;

		var sb = new StringBuilder();
		sb.AppendLine("<html xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:w=\"urn:schemas-microsoft-com:office:word\">");
		sb.AppendLine("<head><meta charset=\"utf-8\" />");
		sb.AppendLine("<!--[if gte mso 9]><xml>");
		sb.AppendLine("<w:WordDocument><w:View>Print</w:View><w:Zoom>100</w:Zoom>");
		sb.AppendLine("<w:DoNotOptimizeForBrowser/></w:WordDocument></xml><![endif]-->");
		sb.AppendLine("<style>");
		// Отступы только через @page: body+@page в Word суммируются и страница «уезжает».
		sb.AppendLine("@page Section1{size:210mm 297mm;margin:2cm 2cm 2cm 2.5cm;mso-page-orientation:portrait;}");
		sb.AppendLine("div.Section1{page:Section1;}");
		sb.AppendLine("html,body{margin:0;padding:0;}");
		sb.AppendLine("body{font-family:Calibri,Arial,sans-serif;font-size:11pt;}");
		sb.AppendLine("table{border-collapse:collapse;mso-table-lspace:0pt;mso-table-rspace:0pt;}");
		sb.AppendLine("td,th{word-wrap:break-word;overflow-wrap:break-word;}");
		sb.AppendLine(".header-top{width:100%;table-layout:fixed;margin-bottom:6pt;}");
		sb.AppendLine(".header-top td{border:none !important;padding:0;vertical-align:top;}");
		sb.AppendLine(".logo-cell{width:3.8cm;}");
		sb.AppendLine(".logo-cell img{width:3.4cm;height:3.4cm;display:block;}");
		sb.AppendLine(".contact-cell{width:auto;text-align:right;padding-right:0;}");
		sb.AppendLine(".contact-inner{width:auto;text-align:left;}");
		sb.AppendLine(".contact-vbar{width:0.6pt;background-color:").Append(BrandRed).Append(";font-size:1pt;line-height:1pt;mso-line-height-rule:exactly;}");
		sb.AppendLine(".contact-text{padding-left:0.28cm;vertical-align:top;width:6.2cm;}");
		sb.AppendLine(".contact-line{color:").Append(BrandRed).Append(";font-size:10pt;line-height:1.25;margin:0;padding:0;}");
		sb.AppendLine(".doc-title{text-align:center;font-weight:bold;font-size:14pt;margin:14pt 0 10pt 0;}");
		sb.AppendLine(".doc-title-sub{font-size:13pt;margin-top:4pt;}");
		sb.AppendLine(".meta-grid{width:100%;table-layout:fixed;}");
		sb.AppendLine(".meta-grid td{border:none !important;padding:4pt 6pt 6pt 0;vertical-align:bottom;width:50%;}");
		sb.AppendLine(".lbl{font-weight:normal;}");
		sb.AppendLine(".val-line{border-bottom:1pt solid #000;display:block;min-height:14pt;margin-top:2pt;padding-bottom:1pt;}");
		sb.AppendLine(".full-row{width:100%;table-layout:fixed;}");
		sb.AppendLine(".full-row td{border:none !important;padding:6pt 0;vertical-align:bottom;}");
		sb.AppendLine(".eq-title{font-weight:bold;margin:14pt 0 6pt 0;}");
		sb.AppendLine(".eq-table{width:100%;table-layout:fixed;}");
		sb.AppendLine(".eq-table td,.eq-table th{border:1px solid #000;padding:4pt 5pt;vertical-align:top;font-size:11pt;width:50%;}");
		sb.AppendLine(".works-title{font-weight:bold;margin:14pt 0 6pt 0;}");
		sb.AppendLine(".works-table{width:100%;table-layout:fixed;}");
		sb.AppendLine(".works-table td{border:1px solid #000;padding:4pt 5pt;vertical-align:middle;font-size:11pt;}");
		sb.AppendLine(".works-label{width:42%;}");
		sb.AppendLine(".works-mark{width:8%;text-align:center;}");
		sb.AppendLine(".bottom-section-title{font-weight:bold;text-transform:uppercase;margin:12pt 0 4pt 0;}");
		sb.AppendLine(".lined-table{width:100%;table-layout:fixed;}");
		sb.AppendLine(".lined-table td{border-bottom:1px solid #7A7A7A;height:16pt;vertical-align:bottom;padding:0 0 1pt 0;font-size:11pt;}");
		sb.AppendLine(".signature-grid{width:100%;table-layout:fixed;margin-top:16pt;}");
		sb.AppendLine(".signature-grid td{vertical-align:top;border:none !important;padding:0;}");
		sb.AppendLine(".signature-title{font-weight:bold;margin:0 0 12pt 0;line-height:1.2;}");
		sb.AppendLine(".signature-fields{width:100%;table-layout:fixed;}");
		sb.AppendLine(".signature-fields td{border:none !important;padding:2pt 0;font-size:11pt;}");
		sb.AppendLine(".signature-label{width:90pt;white-space:nowrap;padding-right:6pt !important;}");
		sb.AppendLine(".signature-line{border-bottom:1px solid #7A7A7A;min-height:14pt;display:block;}");
		sb.AppendLine(".footer-brand{margin-top:26pt;}");
		sb.AppendLine(".footer-rule{border-top:1.5pt solid ").Append(BrandRed).Append(";font-size:1pt;line-height:1pt;mso-line-height-rule:exactly;}");
		sb.AppendLine(".footer-meta{margin-top:8pt;width:100%;table-layout:fixed;}");
		sb.AppendLine(".footer-meta td{border:none !important;color:").Append(BrandRed).Append(";font-size:11pt;}");
		sb.AppendLine(".footer-meta .right{text-align:right;}");
		sb.AppendLine("</style></head><body>");
		sb.AppendLine("<div class=\"Section1\">");

		// Шапка Brand Schutz: логотип слева, контакты справа
		sb.AppendLine("<table class=\"header-top\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
		sb.AppendLine("<td class=\"logo-cell\">");
		if (!string.IsNullOrEmpty(logoUri))
			sb.Append("<img width=\"128\" height=\"128\" src=\"").Append(logoUri).Append("\" alt=\"Brand Schutz\" />");
		else
			sb.Append("<span style=\"color:").Append(BrandRed).Append(";font-weight:bold;font-size:14pt;\">Brand Schutz</span>");
		sb.AppendLine("</td>");
		sb.AppendLine("<td class=\"contact-cell\">");
		sb.AppendLine("<table class=\"contact-inner\" cellspacing=\"0\" cellpadding=\"0\" align=\"right\"><tr>");
		sb.AppendLine("<td class=\"contact-vbar\" style=\"width:1px;\">&nbsp;</td>");
		sb.AppendLine("<td class=\"contact-text\">");
		AppendContactLine(sb, "ООО «Бранд Шутц»");
		AppendContactLine(sb, "+7(495) 363-8916");
		AppendContactLine(sb, "117648, г. Москва, вн.тер.г.");
		AppendContactLine(sb, "Муниципальный Округ Чертаново");
		AppendContactLine(sb, "Северное, мкр. Северное Чертаново,");
		AppendContactLine(sb, "д. 4, к. 402, пом. 6/2Т");
		sb.AppendLine("</td></tr></table>");
		sb.AppendLine("</td></tr></table>");

		sb.AppendLine("<div class=\"doc-title\">");
		sb.AppendLine("АКТ _____ /_____<br/>");
		sb.Append("<span class=\"doc-title-sub\">").Append(Html(subtitle)).AppendLine("</span>");
		sb.AppendLine("</div>");

		// Шапка реквизитов
		sb.AppendLine("<table class=\"meta-grid\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
		sb.Append("<td><span class=\"lbl\">Заказчик:</span><span class=\"val-line\">").Append(Html(customer)).AppendLine("</span></td>");
		sb.Append("<td><span class=\"lbl\">Дата проведения работ:</span><span class=\"val-line\">").Append(Html(dates)).AppendLine("</span></td>");
		sb.AppendLine("</tr><tr>");
		sb.Append("<td colspan=\"2\"><span class=\"lbl\">Объект:</span><span class=\"val-line\">").Append(Html(objectAddress)).AppendLine("</span></td>");
		sb.AppendLine("</tr>");

		if (draft.Profile == ActBlankProfile.Installation)
		{
			sb.AppendLine("<tr>");
			sb.Append("<td><span class=\"lbl\">Часы эксплуатации:</span><span class=\"val-line\">").Append(Html(hours)).AppendLine("</span></td>");
			sb.Append("<td><span class=\"lbl\">Номер установки:</span><span class=\"val-line\">").Append(Html(unit)).AppendLine("</span></td>");
			sb.AppendLine("</tr>");
		}
		else
		{
			sb.AppendLine("<tr>");
			sb.Append("<td><span class=\"lbl\">Тип оборудования:</span><span class=\"val-line\">")
				.Append(Html(draft.EquipmentTypeName ?? "___________")).AppendLine("</span></td>");
			sb.Append("<td><span class=\"lbl\">Модель:</span><span class=\"val-line\">")
				.Append(Html(string.IsNullOrWhiteSpace(draft.ModelName) ? "___________" : draft.ModelName!)).AppendLine("</span></td>");
			sb.AppendLine("</tr><tr>");
			sb.Append("<td><span class=\"lbl\">Серийный номер:</span><span class=\"val-line\">")
				.Append(Html(string.IsNullOrWhiteSpace(draft.SerialNumber) ? "___________" : draft.SerialNumber!)).AppendLine("</span></td>");
			sb.Append("<td><span class=\"lbl\">Номер установки:</span><span class=\"val-line\">").Append(Html(unit)).AppendLine("</span></td>");
			sb.AppendLine("</tr><tr>");
			sb.Append("<td><span class=\"lbl\">Часы эксплуатации:</span><span class=\"val-line\">").Append(Html(hours)).AppendLine("</span></td>");
			sb.AppendLine("<td>&nbsp;</td>");
			sb.AppendLine("</tr>");
		}

		sb.AppendLine("</table>");
		sb.AppendLine("<table class=\"full-row\" cellspacing=\"0\" cellpadding=\"0\"><tr><td>");
		sb.Append("<span class=\"lbl\">Вид работ:</span><span class=\"val-line\">").Append(Html(workKind)).AppendLine("</span>");
		sb.AppendLine("</td></tr></table>");

		AppendEquipmentStateSection(sb, draft.Profile, ParseEquipmentState(draft.EquipmentStateDisplay, draft.Profile));

		// Перечень работ
		sb.AppendLine("<p class=\"works-title\">Перечень выполненных работ:</p>");
		sb.AppendLine("<table class=\"works-table\" cellspacing=\"0\" cellpadding=\"0\">");
		sb.AppendLine("<colgroup><col class=\"works-label\" /><col class=\"works-mark\" /><col class=\"works-label\" /><col class=\"works-mark\" /></colgroup>");
		var mid = (draft.WorkLines.Count + 1) / 2;
		var left = draft.WorkLines.Take(mid).ToList();
		var right = draft.WorkLines.Skip(mid).ToList();
		var rows = Math.Max(left.Count, right.Count);
		for (var i = 0; i < rows; i++)
		{
			sb.AppendLine("<tr>");
			AppendWorkCell(sb, i < left.Count ? left[i] : null);
			AppendWorkCell(sb, i < right.Count ? right[i] : null);
			sb.AppendLine("</tr>");
		}

		if (draft.Profile == ActBlankProfile.Compressor)
		{
			sb.AppendLine("<tr><td colspan=\"4\">К — контроль; Ч — чистка; З — замена; П — параметры; — — не выполн.</td></tr>");
		}

		sb.AppendLine("</table>");

		sb.AppendLine("<p class=\"bottom-section-title\">ДОПОЛНИТЕЛЬНЫЕ РАБОТЫ:</p>");
		AppendContentLines(sb, draft.ExtraWorksText);
		sb.AppendLine("<p class=\"bottom-section-title\">ЗАМЕЧАНИЯ И РЕКОМЕНДАЦИИ:</p>");
		AppendContentLines(sb, draft.RemarksText);

		sb.AppendLine("<table class=\"signature-grid\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
		sb.AppendLine("<td style=\"width:48%;padding-right:18pt;\">");
		sb.AppendLine("<p class=\"signature-title\">Представитель<br/>ИСПОЛНИТЕЛЯ<br/>Работу выполнил.</p>");
		AppendSignatureFields(sb);
		sb.AppendLine("</td><td style=\"width:4%;\">&nbsp;</td><td style=\"width:48%;padding-left:18pt;\">");
		sb.AppendLine("<p class=\"signature-title\">Представитель<br/>ЗАКАЗЧИКА:<br/>Выполнение работ подтверждаю.</p>");
		AppendSignatureFields(sb);
		sb.AppendLine("</td></tr></table>");

		sb.AppendLine("<div class=\"footer-brand\">");
		sb.AppendLine("<div class=\"footer-rule\">&nbsp;</div>");
		sb.AppendLine("<table class=\"footer-meta\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
		sb.AppendLine("<td>www.brandschutz.ru</td>");
		sb.AppendLine("<td class=\"right\">ИНН 7726589854</td>");
		sb.AppendLine("</tr></table>");
		sb.AppendLine("</div>");

		sb.AppendLine("</div></body></html>");
		return sb.ToString();
	}

	private static void AppendContactLine(StringBuilder sb, string text) =>
		sb.Append("<p class=\"contact-line\">").Append(Html(text)).AppendLine("</p>");

	private sealed record EquipmentStateFlags(bool Working, bool UnderLoad, bool Off, bool NotWorking);

	private static string Box(bool on) => on ? "☑" : "☐";

	private static EquipmentStateFlags ParseEquipmentState(string? display, ActBlankProfile profile)
	{
		// В БД одно поле (обычно comp_state) — копируем в прибытие и убытие.
		var s = string.IsNullOrWhiteSpace(display) ? string.Empty : display.Trim().ToLowerInvariant();
		if (string.IsNullOrEmpty(s))
			return new EquipmentStateFlags(false, false, false, false);

		var notWorking = s.Contains("не рабоч", StringComparison.OrdinalIgnoreCase)
			|| s.Contains("не работ", StringComparison.OrdinalIgnoreCase);
		if (notWorking)
			return new EquipmentStateFlags(false, false, false, true);

		var off = s.Contains("выключ", StringComparison.OrdinalIgnoreCase);
		var underLoad = profile == ActBlankProfile.Dryer
			? s.Contains("в работе", StringComparison.OrdinalIgnoreCase)
			  || (s.Contains("работ", StringComparison.OrdinalIgnoreCase) && !off)
			: s.Contains("нагруз", StringComparison.OrdinalIgnoreCase);

		var hasWorkingKeyword = s.Contains("рабочее", StringComparison.OrdinalIgnoreCase);

		if (hasWorkingKeyword && !underLoad && !off)
			return new EquipmentStateFlags(true, false, false, false);
		if (underLoad && !off)
			return new EquipmentStateFlags(true, true, false, false);
		if (off && !underLoad)
			return new EquipmentStateFlags(true, false, true, false);
		if (underLoad && off)
			return new EquipmentStateFlags(true, true, true, false);
		if (hasWorkingKeyword)
			return new EquipmentStateFlags(true, underLoad, off, false);

		return new EquipmentStateFlags(false, false, false, false);
	}

	private static void AppendEquipmentStateSection(StringBuilder sb, ActBlankProfile profile, EquipmentStateFlags st)
	{
		var (midLeft, midRight) = profile switch
		{
			ActBlankProfile.Dryer => ("Осушитель в работе", "Осушитель выключен"),
			ActBlankProfile.Installation => ("Установка под нагрузкой", "Установка выключена"),
			_ => ("Компрессор под нагрузкой", "Компрессор выключен")
		};

		sb.AppendLine("<p class=\"eq-title\">Состояние оборудования</p>");
		sb.AppendLine("<table class=\"eq-table\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
		sb.AppendLine("<th style=\"width:50%;text-align:center;\">на дату прибытия</th>");
		sb.AppendLine("<th style=\"width:50%;text-align:center;\">на дату убытия</th>");
		sb.AppendLine("</tr><tr>");
		for (var col = 0; col < 2; col++)
		{
			var d = col == 0 ? "прибытия" : "убытия";
			sb.AppendLine("<td style=\"vertical-align:top;\">");
			sb.Append(Box(st.Working)).Append(" Рабочее на дату ").Append(d).AppendLine("<br/>");
			sb.Append(Box(st.UnderLoad)).Append(' ').Append(Html(midLeft))
				.Append(" &nbsp; ").Append(Box(st.Off)).Append(' ').Append(Html(midRight)).AppendLine("<br/>");
			sb.Append(Box(st.NotWorking)).Append(" Не рабочее на дату ").Append(d).AppendLine();
			sb.AppendLine("</td>");
		}

		sb.AppendLine("</tr></table>");
	}

	private static void AppendContentLines(StringBuilder sb, string? text)
	{
		var lines = SplitContentLines(text);
		if (lines.Count == 0)
			return;

		sb.AppendLine("<table class=\"lined-table\" cellspacing=\"0\" cellpadding=\"0\">");
		foreach (var line in lines)
			sb.Append("<tr><td>").Append(Html(line)).AppendLine("</td></tr>");
		sb.AppendLine("</table>");
	}

	private static List<string> SplitContentLines(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return [];

		return text
			.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToList();
	}

	private static void AppendSignatureFields(StringBuilder sb)
	{
		sb.AppendLine("<table class=\"signature-fields\" cellspacing=\"0\" cellpadding=\"0\">");
		foreach (var label in new[] { "Должность:", "ФИО:", "Подпись:" })
		{
			sb.AppendLine("<tr>");
			sb.Append("<td class=\"signature-label\">").Append(Html(label)).AppendLine("</td>");
			sb.AppendLine("<td><span class=\"signature-line\">&nbsp;</span></td>");
			sb.AppendLine("</tr>");
		}

		sb.AppendLine("</table>");
	}

	private static void AppendWorkCell(StringBuilder sb, ActWorkLine? line)
	{
		if (line is null)
		{
			sb.AppendLine("<td class=\"works-label\">&nbsp;</td><td class=\"works-mark\">&nbsp;</td>");
			return;
		}

		sb.Append("<td class=\"works-label\">").Append(Html(line.Label)).AppendLine("</td>");
		sb.Append("<td class=\"works-mark\">").Append(Html(line.Mark)).AppendLine("</td>");
	}

	private static string GetLogoDataUri()
	{
		lock (LogoLock)
		{
			if (_logoDataUri is not null)
				return _logoDataUri;

			var assembly = typeof(SqliteActAssemblyPrototypeService).Assembly;
			using var stream = assembly.GetManifestResourceStream(LogoResourceName);
			if (stream is null)
			{
				_logoDataUri = string.Empty;
				return _logoDataUri;
			}

			using var ms = new MemoryStream();
			stream.CopyTo(ms);
			_logoDataUri = "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
			return _logoDataUri;
		}
	}

	private static string Html(string? s)
	{
		if (string.IsNullOrEmpty(s))
			return "&nbsp;";
		return System.Net.WebUtility.HtmlEncode(s).Replace("\n", "<br/>", StringComparison.Ordinal);
	}
}
