# Структура проекта MRS (подробно)

## 1. Слои решения и зоны ответственности

### 1.1 `src/MRS.Application`
- Здесь находятся интерфейсы сценариев и модели данных.
- В этом слое нет SQL и UI-кода.
- Основные папки:
  - `Checklists` — контракты для создания, редактирования, управления и экспорта листов.
  - `Facilities` — контракты для иерархии заказчик -> объект -> система -> установка.
  - `Storage` — контракты пути/инициализации локальной БД.

### 1.2 `src/MRS.Infrastructure`
- Реализация интерфейсов `MRS.Application` на SQLite.
- Все SQL-запросы и преобразования данных в модели.
- Важные сервисы:
  - `SqliteChecklistFlowService`
  - `SqliteChecklistSaveService`
  - `SqliteChecklistEditService`
  - `SqliteChecklistManagementService`
  - `SqliteChecklistDocumentExportService`

### 1.3 `src/MRS.Maui`
- UI-слой приложения на MAUI Blazor.
- Здесь находятся страницы, маршруты, меню, кнопки и вызовы сервисов.
- DI-конфигурация: `MauiProgram.cs`.

### 1.4 `database/sqlite`
- Схема таблиц, seed, демо-данные, шаблоны.
- Базовая схема: `001_schema.sql`.

### 1.5 `tests/MRS.Infrastructure.Tests`
- Тесты инфраструктурного слоя (инициализация БД, SQL-сервисы).

---

## 2. Маршрутизация и навигация по страницам (полная нумерация)

Маршруты объявлены в Razor через `@page`.

### 0. Главная страница
- Файл: `src/MRS.Maui/Components/Pages/Home.razor`
- URL: `/`
- Кнопки:
  - **0.1** `Контрольный лист` -> `/checklists`
  - **0.2** `Управление листами` -> `/management`
  - **0.3** `Управление объектами` -> заглушка (`javascript:void(0)`)
  - **0.4** `SQL-окно` -> заглушка (`javascript:void(0)`)

### 1. Контрольные листы (выбор контекста)
- Файл: `src/MRS.Maui/Components/Pages/Checklists.razor`
- URL: `/checklists`
- Действия:
  - **1.1** выбор заказчика (селект)
  - **1.2** выбор объекта (селект)
  - **1.3** выбор системы (селект)
  - **1.4** кнопка `Типы оборудований` -> `/checklists/equipment-types?organizationId=...&facilityId=...&systemId=...`
  - **1.5** кнопка назад `←` в тулбаре -> `/`
  - **1.6** информационная таблица истории созданных листов (только просмотр)

### 1.4. Типы оборудования
- Файл: `src/MRS.Maui/Components/Pages/EquipmentTypes.razor`
- URL: `/checklists/equipment-types`
- Действия:
  - **1.4.1** кнопка назад `←` -> `/checklists`
  - **1.4.2** выбор конкретного типа оборудования (кнопка-элемент списка) -> `/checklists/maintenance?organizationId=...&facilityId=...&systemId=...&equipmentTypeId=...`

### 1.4.2. Вид ТО (шаблон)
- Файл: `src/MRS.Maui/Components/Pages/ChecklistMaintenance.razor`
- URL: `/checklists/maintenance`
- Действия:
  - **1.4.2.1** кнопка назад `←` -> `/checklists/equipment-types?...`
  - **1.4.2.2** выбор вида ТО (fork) -> `/checklists/create?organizationId=...&facilityId=...&systemId=...&equipmentTypeId=...&maintenanceTypeId=...`

### 1.4.2.2. Создание контрольного листа
- Файл: `src/MRS.Maui/Components/Pages/ChecklistCreate.razor`
- URL: `/checklists/create`
- Действия:
  - **1.4.2.2.1** кнопка назад `←` -> `/checklists/maintenance?...`
  - **1.4.2.2.2** выбор установки:
    - существующая установка;
    - `Новая установка` (ввод номера).
  - **1.4.2.2.3** заполнение динамических полей формы по `field_type`.
  - **1.4.2.2.4** `Сохранить локально` -> `draft/local`.
  - **1.4.2.2.5** `Сохранить в БД` -> `completed/pending_upload`.
  - **1.4.2.2.6** после сохранения -> возврат на `/checklists?...` с сохранением контекста.
  - **1.4.2.2.7** модальные окна: вводная информация и безопасность.

### 2. Управление листами
- Файл: `src/MRS.Maui/Components/Pages/Management.razor`
- URL: `/management`
- Действия:
  - **2.1** кнопка назад `←` -> `/`
  - **2.2** `Работа с контрольным листом` -> `/checklists/work`
  - **2.3** `Создать шаблон ТО` -> заглушка (`javascript:void(0)`)

### 2.2. Работа с контрольными листами
- Файл: `src/MRS.Maui/Components/Pages/ChecklistWorkList.razor`
- URL: `/checklists/work`
- Действия:
  - **2.2.1** кнопка назад `←` -> `/management`
  - **2.2.2** фильтр `Объект`
  - **2.2.3** фильтр `Оборудование`
  - **2.2.4** фильтр `№ установки`
  - **2.2.5** чекбокс выбора листа для пакетной выгрузки
  - **2.2.6** клик по карточке листа -> `/checklists/work/edit?checklistId=...`
  - **2.2.7** кнопка `↓` (экспорт) -> формирует ZIP, внутри отдельный `.doc` для каждого выбранного листа (или всех отфильтрованных, если ничего не отмечено)

### 2.2.6. Редактирование контрольного листа
- Файл: `src/MRS.Maui/Components/Pages/ChecklistWorkEdit.razor`
- URL: `/checklists/work/edit`
- Действия:
  - **2.2.6.1** кнопка назад `←` -> `/checklists/work`
  - **2.2.6.2** просмотр данных листа и динамических полей
  - **2.2.6.3** `Внести изменения` -> включает режим редактирования
  - **2.2.6.4** `Сохранить` -> модалка подтверждения и `ValidateAsync`/`ApplyAsync`
  - **2.2.6.5** модалка частичного сохранения (если часть полей недоступна)
  - **2.2.6.6** `Отправить администратору` — пока отключена

### 3. Общее меню (на большинстве страниц)
- Файл: `src/MRS.Maui/Components/Shared/MrsTopMenu.razor`
- Действия:
  - **3.1** `Контрольный лист` -> `/checklists`
  - **3.2** `Управление листами` -> `/management`
  - **3.3** `Управление объектами` -> `/facilities` (маршрут должен быть реализован отдельно)
  - **3.4** `SQL-окно` -> `/sql` (маршрут должен быть реализован отдельно)

---

## 3. Карта файлов по выгрузке контрольных листов

- Экран списка/фильтров/экспорта:
  - `src/MRS.Maui/Components/Pages/ChecklistWorkList.razor`
- Экран редактирования:
  - `src/MRS.Maui/Components/Pages/ChecklistWorkEdit.razor`
- Переход из управления:
  - `src/MRS.Maui/Components/Pages/Management.razor`
- Источник данных списка:
  - `src/MRS.Infrastructure/Sqlite/SqliteChecklistManagementService.cs`
- Модель строки списка:
  - `src/MRS.Application/Checklists/ChecklistManagementRow.cs`
- JS-скачивание:
  - `src/MRS.Maui/wwwroot/js/mrs-download.js`
- Сервис экспорта документов:
  - `src/MRS.Application/Checklists/IChecklistDocumentExportService.cs`
  - `src/MRS.Application/Checklists/ChecklistDocumentExportModel.cs`
  - `src/MRS.Infrastructure/Sqlite/SqliteChecklistDocumentExportService.cs`

---

## 4. Как работает текущая выгрузка DOC + ZIP

### 4.1 На странице `ChecklistWorkList`
- Пользователь отмечает чекбоксами нужные листы.
- Если не выбрано ничего, берутся все листы после фильтрации.
- Вызывается `ExportZipAsync(checklistIds)`.

### 4.2 В сервисе `SqliteChecklistDocumentExportService`
- Для каждого `checklistId`:
  1. Загружается шапка документа (организация/объект/установка/вид ТО/даты/статус).
  2. Определяется шаблон листа (`checklist_template_id` или fallback-резолв).
  3. Загружаются ответы и нормализуются значения для отображения.
  4. Генерируется `.doc` (HTML-формат, открывается Word).
- Все `.doc` складываются в один ZIP.

### 4.3 Скачивание файла
- UI получает байты ZIP.
- JS-функция `mrsDownloadBase64File` сохраняет архив локально на устройство.

---

## 5. DI (где подключаются сервисы)

Файл: `src/MRS.Maui/MauiProgram.cs`

Ключевые регистрации:
- `IChecklistManagementService -> SqliteChecklistManagementService`
- `IChecklistEditService -> SqliteChecklistEditService`
- `IChecklistDocumentExportService -> SqliteChecklistDocumentExportService`

---

## 6. Что смотреть, если нужно изменить поведение

- Изменить логику выбора/фильтрации списка:
  - `ChecklistWorkList.razor`
- Изменить состав полей в документе:
  - `SqliteChecklistDocumentExportService.cs` (`LoadHeaderAsync`, `LoadAnswersAsync`, `BuildWordHtml`)
- Изменить формат/имя файлов:
  - `SqliteChecklistDocumentExportService.cs` (`ExportDocAsync`, `ExportZipAsync`)
- Изменить способ скачивания:
  - `wwwroot/js/mrs-download.js`
