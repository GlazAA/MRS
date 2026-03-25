import { writeFileSync } from "fs";
import { join, dirname } from "path";
import { fileURLToPath } from "url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const outPath = join(__dirname, "../database/sqlite/007_mosarchive_full.sql");

const esc = (s) => (s ?? "").replace(/'/g, "''");

const lines = [];
const add = (s) => lines.push(s);

add("-- 007: intro_modal_text, Mosarchive templates");
add("PRAGMA foreign_keys = ON;");
add("");
add("ALTER TABLE checklist_templates ADD COLUMN intro_modal_text TEXT;");
add("");
add("DELETE FROM checklist_response_multi_options;");
add("DELETE FROM checklist_responses;");
add("DELETE FROM checklist_template_item_options;");
add("DELETE FROM checklist_template_items;");
add("");

const weeklySafety = `ВНИМАНИЕ! Перед всеми работами по техническому обслуживанию:
1. Отключить компрессор при помощи кнопки ВЫКЛ.
2. Привести в действие переключатель аварийного останова.
3. Разомкнуть устройство отключения от сети и обезопасить с помощью висячего замка от непреднамеренного повторного включения.
4. Разместить на устройстве управления предупреждающую табличку.
5. Проверить, действительно ли обесточены все детали установки.
6. Перед началом работы дать всем горячим элементам конструкции компрессора остыть до 50°C.
7. Отсоединить компрессор от сети сжатого воздуха. Для этого закрыть шаровой кран на выходе сжатого воздуха.
8. Удалить воздух из системы компрессора.`;

const errorsIntro =
  "При наличии ошибок и неисправностей следует соблюдать инструкции и рекомендации, указанные в руководстве по эксплуатации компрессора.";
const to1500 = "Внимание! Обслуживание выполняется с ТО-1500.";
const annualIntro = `${errorsIntro}\n${to1500}`;

add(
  `UPDATE checklist_templates SET intro_modal_text = NULL, safety_modal_text = '${esc(weeklySafety)}', red_button_enabled = 1, top_plate_text = 'Мосархив – Сахарово. Винтовой компрессор — еженедельное ТО' WHERE id = 1;`
);
for (const tid of [3, 4, 6, 7, 8]) {
  add(
    `UPDATE checklist_templates SET intro_modal_text = '${esc(annualIntro)}', safety_modal_text = '${esc(annualIntro)}', red_button_enabled = 1 WHERE id = ${tid};`
  );
}
add(
  `UPDATE checklist_templates SET intro_modal_text = NULL, safety_modal_text = '${esc(errorsIntro)}', red_button_enabled = 1, top_plate_text = 'Винтовой компрессор — ежемесячное ТО' WHERE id = 2;`
);
add(
  `UPDATE checklist_templates SET intro_modal_text = '${esc(errorsIntro)}', safety_modal_text = '${esc(errorsIntro)}', red_button_enabled = 1, top_plate_text = 'ТО-1500 компрессора' WHERE id = 5;`
);
add("");

add(`INSERT OR IGNORE INTO users (id, user_role_id, first_name, last_name, login, password_hash, is_active) VALUES
    (2, 1, 'Пётр', 'Петров', 'petrov', '$2a$11$OfflinePlaceholderHashNotForAuth', 1),
    (3, 1, 'Сергей', 'Сидоров', 'sidorov', '$2a$11$OfflinePlaceholderHashNotForAuth', 1),
    (4, 1, 'Анна', 'Козлова', 'kozlova', '$2a$11$OfflinePlaceholderHashNotForAuth', 1),
    (5, 1, 'Иван', 'Иванов', 'ivanov', '$2a$11$OfflinePlaceholderHashNotForAuth', 1);`);
add("");

const ft = {
  date: 3,
  time: 4,
  text: 1,
  textarea: 2,
  number: 6,
  radio: 8,
  checkbox: 9,
  dropdown: 10,
  dropdown_multiple: 11,
};

function item(id, tid, sort, code, q, hint, ftype, req, valRule) {
  const c = code ? `'${esc(code)}'` : "NULL";
  const h = hint ? `'${esc(hint)}'` : "NULL";
  const vr = valRule ? `'${esc(valRule)}'` : "NULL";
  add(
    `INSERT INTO checklist_template_items (id, checklist_template_id, sort_order, field_code, question_text, hint_text, field_type_id, is_required, validation_rule_code) VALUES (${id}, ${tid}, ${sort}, ${c}, '${esc(q)}', ${h}, ${ftype}, ${req ? 1 : 0}, ${vr});`
  );
}

const optLines = [];
let optId = 60001;
function pushOpt(itemId, sort, label) {
  optLines.push(
    `INSERT INTO checklist_template_item_options (id, checklist_template_item_id, sort_order, option_label) VALUES (${optId++}, ${itemId}, ${sort}, '${esc(label)}');`
  );
}
function pushOpts(itemId, labels) {
  labels.forEach((lb, i) => pushOpt(itemId, i + 1, lb));
}

function compressorBase(tid, idStart) {
  let n = idStart;
  item(n++, tid, 1, "start_date", "Дата начала", "Строго дд.мм.гггг (поле календаря)", ft.date, true);
  item(n++, tid, 2, "start_time", "Время начала", "Строго чч:мм", ft.time, true);
  item(n++, tid, 3, "workers", "Лица, производившие работы", "Можно выбрать несколько", ft.dropdown_multiple, true);
  item(n++, tid, 4, "unit_number", "Номер установки", null, ft.dropdown, true);
  item(n++, tid, 5, "equipment_pick", "Оборудование", null, ft.dropdown, true);
  item(n++, tid, 6, "comp_model", "Модель компрессора", null, ft.dropdown, false);
  item(n++, tid, 7, "comp_type", "Тип компрессора", null, ft.dropdown, false);
  item(n++, tid, 8, "comp_state", "Состояние компрессора", "Выберите состояние оборудования до начала работ", ft.checkbox, false);
  item(n++, tid, 9, "operating_hours", "Часы эксплуатации компрессора", "Можно указать часы и минуты", ft.text, false);
  item(n++, tid, 10, "pressure_network", "Давление в сети Pn (bar)", null, ft.text, false);
  item(n++, tid, 11, "pressure_system", "Давление в системе Ps (bar)", null, ft.text, false);
  item(n++, tid, 12, "final_temp", "Конечная температура сжатия", "Проверить заданное значение: 70...100°C", ft.number, false, "integer_range_70_100");
  return n;
}

function stdCompressorOpts(idStart) {
  pushOpts(idStart + 2, [
    "Демо Инженер",
    "Петров Пётр Петрович",
    "Сидоров Сергей Сидорович",
    "Козлова Анна",
    "Иванов Иван Иванович",
  ]);
  pushOpts(idStart + 3, ["G301", "301", "G302", "302"]);
  pushOpts(idStart + 4, [
    "Винтовой компрессор",
    "Электродвигатель компрессора",
    "Осушитель холодильного типа",
    "Циклонный сепаратор",
    "Фильтры очистки",
    "Угольный адсорбер",
    "Конденсатоотводчики",
    "Водомасляный сепаратор",
    "Ресиверы",
    "Газоразделительный модуль",
    "Центральный шкаф управления",
    "Шкаф управления зоной защиты",
    "Датчики, контроллеры и модули",
  ]);
  pushOpts(idStart + 5, ["Atlas Copco GA", "Ingersoll Rand R", "Другое"]);
  pushOpts(idStart + 6, ["Стационарный", "Мобильный", "Другое"]);
  pushOpts(idStart + 7, ["Рабочее", "Под нагрузкой", "Выключен", "Не рабочее"]);
}

let cur = compressorBase(1, 5001);
item(cur++, 1, 13, "display_eval_weekly", "Оценка отображаемых на дисплее данных, ЕЖН", "Введите код ошибки или иные сервисные сообщения", ft.text, false);
item(cur++, 1, 14, "leak_check_weekly", "Проверка наличия негерметичностей, ЕЖН", "Визуальная проверка на наличие негерметичности", ft.radio, false);
const leakW = cur - 1;
item(cur++, 1, 15, "diff_pressure_weekly", "Текущий дифференциал давления, ЕЖН", "Проверить разницу между давлением в сети и системой, заданное значение 0 – 1,5 бар", ft.radio, false);
const diffW = cur - 1;
item(cur++, 1, 16, "filter_panel_weekly", "Фильтр приточного воздуха (панельный), ЕЖН", "Визуальная проверка, при необходимости замена", ft.radio, false);
const filtW = cur - 1;
item(cur++, 1, 17, "extra_weekly", "Дополнительные работы, ЕЖН ТО", null, ft.textarea, false);
item(cur++, 1, 18, "remarks_weekly", "Замечания и рекомендации, ЕЖН ТО", null, ft.textarea, false);
item(cur++, 1, 19, "end_date", "Дата окончания", null, ft.date, false);
item(cur++, 1, 20, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
stdCompressorOpts(5001);
pushOpts(leakW, ["Есть", "Отсутствует"]);
pushOpts(diffW, ["Норма", "Отклонение"]);
pushOpts(filtW, ["Норма", "Замена", "Чистка"]);

let cur2 = compressorBase(2, 5101);
item(cur2++, 2, 13, "emergency_switch_monthly", "Переключатель аварийного останова, ЕЖМ", "Проверить функционирование переключателя аварийного останова", ft.radio, false);
const esw = cur2 - 1;
item(cur2++, 2, 14, "work_pressure_monthly", "Проверка рабочего давления, ЕЖМ", "Проверить и при необходимости подрегулировать рабочее давление", ft.radio, false);
const wp = cur2 - 1;
item(cur2++, 2, 15, "suction_filter_monthly", "Фильтр всасывающий (воздушный), ЕЖМ", "Очистить от загрязнений и при необходимости заменить", ft.radio, false);
const sf = cur2 - 1;
item(cur2++, 2, 16, "oil_level_monthly", "Уровень масла в резервуаре, ЕЖМ", "Проверить уровень масла и при необходимости долить", ft.radio, false);
const oil = cur2 - 1;
item(cur2++, 2, 17, "temp_ped", "Температура корпуса ПЭД", "Измерить пирометром температуру корпуса приводного электродвигателя", ft.text, false);
item(cur2++, 2, 18, "temp_edo", "Температура корпуса ЭДО", "Измерить пирометром температуру корпуса электродвигателя охладителя", ft.text, false);
item(cur2++, 2, 19, "extra_monthly", "Дополнительные работы, ЕЖМ ТО", null, ft.textarea, false);
item(cur2++, 2, 20, "remarks_monthly", "Замечания и рекомендации, ЕЖМ ТО", null, ft.textarea, false);
item(cur2++, 2, 21, "end_date", "Дата окончания", null, ft.date, false);
item(cur2++, 2, 22, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
stdCompressorOpts(5101);
pushOpts(esw, ["Рабочий", "Не рабочий"]);
pushOpts(wp, ["Норма", "Отклонение"]);
pushOpts(sf, ["Чистка", "Замена"]);
pushOpts(oil, ["Норма", "Долито"]);

for (let ti = 3; ti <= 8; ti++) {
  const start = 5200 + (ti - 3) * 100 + 1;
  let cn = compressorBase(ti, start);
  item(cn++, ti, 13, "regulation_notes", "Пункты контрольного листа по выбранному виду ТО", "Полный перечень полей ТО-3000/6000/9000 будет добавлен в следующей итерации БД", ft.textarea, false);
  item(cn++, ti, 14, "extra_typed", "Дополнительные работы", null, ft.textarea, false);
  item(cn++, ti, 15, "remarks_typed", "Замечания и рекомендации", null, ft.textarea, false);
  item(cn++, ti, 16, "end_date", "Дата окончания", null, ft.date, false);
  item(cn++, ti, 17, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
  stdCompressorOpts(start);
}

const motorIntro =
  "ТО - 1400. При наличии ошибок и неисправностей следует соблюдать инструкции и рекомендации, указанные в руководстве по эксплуатации.";
add(
  `UPDATE checklist_templates SET red_button_enabled = 1, intro_modal_text = '${esc(motorIntro)}', safety_modal_text = '${esc(motorIntro)}' WHERE id = 9;`
);
item(8001, 9, 1, "start_date", "Дата начала", null, ft.date, true);
item(8002, 9, 2, "start_time", "Время начала", null, ft.time, true);
item(8003, 9, 3, "workers", "Лица, производившие работы", "Можно выбрать несколько", ft.dropdown_multiple, true);
item(8004, 9, 4, "unit_number", "Номер установки", null, ft.dropdown, true);
item(8005, 9, 5, "motor_model", "Модель/тип ПЭД", null, ft.text, true);
item(8006, 9, 6, "motor_hours_note", "Часы эксплуатации компрессора при ТО ПЭД", "Указать часы эксплуатации компрессора при смазке подшипников двигателя", ft.text, false);
item(8007, 9, 7, "bearing_grease", "Смазка подшипников ПЭД, ТО-1400", "Смазать подшипники приводного двигателя…", ft.radio, false);
item(8008, 9, 8, "service_reset_motor", "Сброс интервала сервисного обслуживания ПЭД, ТО-1400", null, ft.radio, false);
item(8009, 9, 9, "extra_motor", "Дополнительные работы, ТО ПЭД", null, ft.textarea, false);
item(8010, 9, 10, "remarks_motor", "Замечания и рекомендации, ТО ПЭД", null, ft.textarea, false);
item(8011, 9, 11, "end_date", "Дата окончания", null, ft.date, false);
item(8012, 9, 12, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
pushOpts(8003, ["Демо Инженер", "Петров Пётр Петрович", "Сидоров Сергей Сидорович", "Козлова Анна", "Иванов Иван Иванович"]);
pushOpts(8004, ["G301", "301", "G302", "302"]);
pushOpts(8007, ["Выполнено", "Не выполнено"]);
pushOpts(8008, ["Выполнено", "Не выполнено"]);

add(
  `UPDATE checklist_templates SET red_button_enabled = 1, intro_modal_text = '${esc(errorsIntro)}', safety_modal_text = '${esc(errorsIntro)}' WHERE id = 10;`
);
item(8101, 10, 1, "start_date", "Дата начала", null, ft.date, true);
item(8102, 10, 2, "start_time", "Время начала", null, ft.time, true);
item(8103, 10, 3, "workers", "Лица, производившие работы", null, ft.dropdown_multiple, true);
item(8104, 10, 4, "unit_number", "Номер установки", null, ft.dropdown, true);
item(8105, 10, 5, "oht_model", "Модель/тип ОХТ", null, ft.text, true);
item(8106, 10, 6, "oht_hours", "Общее число рабочих часов осушителя", "Параметр А2/А3. Рабочие часы = А3×1000+А2", ft.text, false);
item(8107, 10, 7, "fridge_hours", "Общее число рабочих часов холодильного компрессора", "Параметр А4/А5. Рабочие часы = А5×1000+А4", ft.text, false);
item(8108, 10, 8, "compressor_out_temp", "Температура на выходе компрессора ОХТ (линия нагнетания)", "Параметр b8", ft.text, false);
item(8109, 10, 9, "led_state_oht", "Проверка состояния индикации (светодиодов) ОХТ, ЕЖН", "Проверка: горит индикатор POWER ON…", ft.radio, false);
item(8110, 10, 10, "display_oht", "Оценка отображаемых на дисплее данных ОХТ, ЕЖН", "Введите код ошибки или иные сервисные сообщения", ft.text, false);
item(8111, 10, 11, "condensate_device_oht", "Проверка устройства слива конденсата ОХТ, ЕЖН", "Для активации кратко нажать кнопку диагностики конденсатоотводчика", ft.radio, false);
item(8112, 10, 12, "fins_clean_oht", "Чистка рёбер конденсатора ОХТ, 4 мес", null, ft.radio, false);
item(8113, 10, 13, "current_l1_oht", "Потребляемый ток под нагрузкой L1 ОХТ, 4 мес", "Проверка величины потребляемого тока", ft.text, false);
item(8114, 10, 14, "current_l2_oht", "Потребляемый ток под нагрузкой L2 ОХТ, 4 мес", "Проверка величины потребляемого тока", ft.text, false);
item(8115, 10, 15, "current_l3_oht", "Потребляемый ток под нагрузкой L3 ОХТ, 4 мес", "Проверка величины потребляемого тока", ft.text, false);
item(8116, 10, 16, "oht_safety_gate", "Перед дальнейшими пунктами", "ВНИМАНИЕ! Прежде чем приступить к выполнению любой операции ТО проверьте: отсутствие давления в пневматическом контуре; осушитель отключен от электрической сети.", ft.text, false);
item(8117, 10, 17, "leak_refrigerant", "Проверка системы на утечку хладагента ОХТ, ЕЖГ", "Визуальная проверка соединений и магистралей", ft.radio, false);
item(8118, 10, 18, "temp_sensor_oht", "Проверка датчика температуры ОХТ, ЕЖГ", "Проверить датчики температуры. Заменить их, если необходимо", ft.radio, false);
item(8119, 10, 19, "kits_oht", "Установка комплектов для ТО ОХТ, 3 года", null, ft.checkbox, false);
item(8120, 10, 20, "extra_oht", "Дополнительные работы, ТО ОХТ", null, ft.textarea, false);
item(8121, 10, 21, "remarks_oht", "Замечания и рекомендации, ТО ОХТ", null, ft.textarea, false);
item(8122, 10, 22, "end_date", "Дата окончания", null, ft.date, false);
item(8123, 10, 23, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
pushOpts(8103, ["Демо Инженер", "Петров Пётр Петрович", "Сидоров Сергей Сидорович", "Козлова Анна", "Иванов Иван Иванович"]);
pushOpts(8104, ["G301", "301", "G302", "302"]);
pushOpts(8109, ["Исправна", "Не работает", "Выключена"]);
pushOpts(8111, ["Исправно", "Неисправно", "Не выполнено"]);
pushOpts(8112, ["Выполнено", "Не выполнено"]);
pushOpts(8117, ["Есть", "Отсутствует"]);
pushOpts(8118, ["Контроль", "Замена", "Не выполнялось"]);
pushOpts(8119, [
  "комплекты для компрессора",
  "комплекты для вентилятора",
  "комплекты для клапана горячего газа",
  "комплекты для испарителя",
]);

function unifiedHead(tid, id0) {
  item(id0, tid, 1, "start_date", "Дата начала", null, ft.date, true);
  item(id0 + 1, tid, 2, "start_time", "Время начала", null, ft.time, true);
  item(id0 + 2, tid, 3, "workers", "Лица, производившие работы", null, ft.dropdown_multiple, true);
  item(id0 + 3, tid, 4, "unit_number", "Номер установки", null, ft.dropdown, true);
  pushOpts(id0 + 2, ["Демо Инженер", "Петров Пётр Петрович", "Сидоров Сергей Сидорович", "Козлова Анна", "Иванов Иван Иванович"]);
  pushOpts(id0 + 3, ["G301", "301", "G302", "302"]);
}

add(
  `UPDATE checklist_templates SET intro_modal_text = '${esc(errorsIntro)}', safety_modal_text = '${esc(errorsIntro)}', red_button_enabled = 1 WHERE id = 11;`
);
unifiedHead(11, 8201);
item(8205, 11, 5, "cyclone_model", "Модель/тип ЦС", null, ft.text, true);
item(8206, 11, 6, "cyclone_state", "Проверка состояния ЦС", "Визуальный контроль состояния конденсатоотводчика", ft.radio, false);
item(8207, 11, 7, "extra_cyclone", "Дополнительные работы, ЦС", null, ft.textarea, false);
item(8208, 11, 8, "remarks_cyclone", "Замечания и рекомендации, ЦС", null, ft.textarea, false);
item(8209, 11, 9, "end_date", "Дата окончания", null, ft.date, false);
item(8210, 11, 10, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
pushOpts(8206, ["Исправен", "Авария", "Чистка", "Не выполнялось"]);

add(
  `UPDATE checklist_templates SET intro_modal_text = 'Замена фильтрующих элементов не реже 1 раза в год.', safety_modal_text = 'Замена фильтрующих элементов не реже 1 раза в год.', red_button_enabled = 1 WHERE id = 12;`
);
unifiedHead(12, 8301);
item(8305, 12, 5, "filter_model", "Модель/тип фильтра", null, ft.text, true);
item(8306, 12, 6, "filter_place", "Место установки фильтра", "Выберите место установки в сети", ft.radio, false);
item(8307, 12, 7, "filter_element", "Проверка фильтрующего элемента, ЕЖН", "Проверьте индикацию на дифференциальном манометре (опционально)", ft.radio, false);
item(8308, 12, 8, "extra_filters", "Дополнительные работы по фильтрам", null, ft.textarea, false);
item(8309, 12, 9, "remarks_filters", "Замечания и рекомендации по фильтрам", null, ft.textarea, false);
item(8310, 12, 10, "end_date", "Дата окончания", null, ft.date, false);
item(8311, 12, 11, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
pushOpts(8306, ["Сжатый воздух", "Азот"]);
pushOpts(8307, ["Норма", "Отклонение", "Замена"]);

const adsIntro = `Адсорберы на основе активированного угля. ${errorsIntro}`;
add(
  `UPDATE checklist_templates SET intro_modal_text = '${esc(adsIntro)}', safety_modal_text = '${esc(adsIntro)}', red_button_enabled = 1 WHERE id = 13;`
);
unifiedHead(13, 8401);
item(8405, 13, 5, "ads_model", "Модель/тип адсорбера", null, ft.text, true);
item(8406, 13, 6, "ads_damage", "Проверка на наличие повреждений АДС, ЕЖН", "Визуальный осмотр корпуса и соединений", ft.radio, false);
item(8407, 13, 7, "ads_pressure_gauge", "Проверка уровня давления на манометре АДС, ЕЖН", "Визуальная проверка уровня давления на манометре", ft.radio, false);
item(8408, 13, 8, "ads_tube", "Индикаторная трубка с механизмом АДС, ЕЖН", "Замена индикаторной трубки производится после изменения цвета", ft.radio, false);
item(8409, 13, 9, "ads_oil_residual", "Проверка остаточного содержания масла в АДС, ЕЖМ", null, ft.radio, false);
item(8410, 13, 10, "ads_carbon_year", "Замена активированного угля в АДС, ГОД", null, ft.radio, false);
item(8411, 13, 11, "extra_ads", "Дополнительные работы, АДС", null, ft.textarea, false);
item(8412, 13, 12, "remarks_ads", "Замечания и рекомендации, АДС", null, ft.textarea, false);
item(8413, 13, 13, "end_date", "Дата окончания", null, ft.date, false);
item(8414, 13, 14, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
pushOpts(8406, ["Норма", "Отклонение", "Не выполнялось"]);
pushOpts(8407, ["Норма", "Отклонение"]);
pushOpts(8408, ["Контроль", "Замена"]);
pushOpts(8409, ["Норма", "Отклонение", "Не выполнялось"]);
pushOpts(8410, ["Выполнено", "Не выполнено"]);

add(
  `UPDATE checklist_templates SET intro_modal_text = '${esc(errorsIntro)}', safety_modal_text = '${esc(errorsIntro)}', red_button_enabled = 1 WHERE id IN (14,15,17,18,19,20);`
);
add(
  `UPDATE checklist_templates SET intro_modal_text = NULL, safety_modal_text = 'Еженедельное техническое обслуживание.', red_button_enabled = 1 WHERE id = 16;`
);

unifiedHead(14, 8501);
item(8505, 14, 5, "cond_model", "Модель/тип КО", "Если в сети одинаковые модели, укажите серийный номер", ft.text, true);
item(8506, 14, 6, "cond_led", "Проверка состояния индикации (светодиодов) КО, ЕЖН", null, ft.radio, false);
item(8507, 14, 7, "cond_drain", "Проверка работы и отвода конденсата КО, ЕЖН", null, ft.radio, false);
item(8508, 14, 8, "cond_clean", "Чистка корпуса и клапана КО, ЕЖГ", null, ft.radio, false);
item(8509, 14, 9, "cond_wear", "Замена изнашивающихся деталей КО, ЕЖГ", null, ft.radio, false);
item(8510, 14, 10, "extra_cond", "Дополнительные работы, КО", null, ft.textarea, false);
item(8511, 14, 11, "remarks_cond", "Замечания и рекомендации, КО", null, ft.textarea, false);
item(8512, 14, 12, "end_date", "Дата окончания", null, ft.date, false);
item(8513, 14, 13, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
pushOpts(8506, ["Исправна", "Неисправна", "Не выполнено"]);
pushOpts(8507, ["Исправна", "Неисправна", "Не выполнено"]);
pushOpts(8508, ["Выполнено", "Не выполнено"]);
pushOpts(8509, ["Выполнено", "Не выполнено"]);

unifiedHead(15, 8601);
item(8605, 15, 5, "wms_model", "Модель/тип ВМС", null, ft.text, true);
item(8606, 15, 6, "wms_indicators", "Проверка состояния индикаторов ВМС, ЕЖМ", "Визуальный контроль за индикаторами водомасляного сепаратора", ft.radio, false);
item(8607, 15, 7, "wms_filters", "Замена фильтров по индикаторам, ВМС", null, ft.radio, false);
item(8608, 15, 8, "extra_wms", "Дополнительные работы, ВМС", null, ft.textarea, false);
item(8609, 15, 9, "remarks_wms", "Замечания и рекомендации, ВМС", null, ft.textarea, false);
item(8610, 15, 10, "end_date", "Дата окончания", null, ft.date, false);
item(8611, 15, 11, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
pushOpts(8606, ["Норма", "Отклонение"]);
pushOpts(8607, ["Выполнено", "Не выполнено"]);

unifiedHead(16, 8701);
item(8705, 16, 5, "recv_model", "Модель/тип ресивера", null, ft.text, true);
item(8706, 16, 6, "recv_place", "Место установки ресивера", "Выберите место установки в сети", ft.radio, false);
item(8707, 16, 7, "recv_drain", "Проверка на содержание частиц или жидкости (вода/масло)", "Ненадолго приоткройте на дне ресивера ручной клапан сброса конденсата", ft.radio, false);
item(8708, 16, 8, "recv_leak", "Проверка наличия негерметичностей", null, ft.radio, false);
item(8709, 16, 9, "recv_pressure", "Проверка уровня давления на манометре", "Визуальная проверка уровня давления на манометре", ft.radio, false);
item(8710, 16, 10, "extra_recv", "Дополнительные работы по ресиверу", null, ft.textarea, false);
item(8711, 16, 11, "remarks_recv", "Замечания и рекомендации по ресиверу", null, ft.textarea, false);
item(8712, 16, 12, "end_date", "Дата окончания", null, ft.date, false);
item(8713, 16, 13, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
pushOpts(8706, ["Сжатый воздух", "Азот"]);
pushOpts(8707, ["Выполнено", "Не выполнялось"]);
pushOpts(8708, ["Есть", "Отсутствуют", "Не выполнялось"]);
pushOpts(8709, ["Норма", "Отклонение"]);

unifiedHead(17, 8801);
item(8805, 17, 5, "grm_model", "Модель/тип ГРМ", null, ft.text, true);
item(8806, 17, 6, "grm_main_switch", "Проверьте положение главного выключателя на модуле, ЕЖН", "Выключатель должен находиться в положении ON", ft.radio, false);
item(8807, 17, 7, "grm_states", "Проверка заданных состояний и предельных значений ГРМ, ЕЖН", null, ft.radio, false);
item(8808, 17, 8, "grm_pressure", "Проверка уровня давления на манометре ГРМ, ЕЖН", "Визуальная проверка уровня давления на манометре", ft.radio, false);
item(8809, 17, 9, "grm_replace_3y", "Замена оборудования ГРМ, каждые 3 года", "Отметьте какое оборудование было заменено", ft.checkbox, false);
item(8810, 17, 10, "grm_replace_5y", "Замена оборудования ГРМ, каждые 5 лет", "Отметьте какое оборудование было заменено", ft.checkbox, false);
item(8811, 17, 11, "extra_grm", "Дополнительные работы, ГРМ", null, ft.textarea, false);
item(8812, 17, 12, "remarks_grm", "Замечания и рекомендации, ГРМ", null, ft.textarea, false);
item(8813, 17, 13, "end_date", "Дата окончания", null, ft.date, false);
item(8814, 17, 14, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
pushOpts(8806, ["Включен", "Отключен"]);
pushOpts(8807, ["Норма", "Отклонение"]);
pushOpts(8808, ["Норма", "Отклонение"]);
pushOpts(8809, [
  'Манометр "Вход сжатого воздуха"',
  'Манометр "Накопительная емкость CMS1"',
  'Манометр "Накопительная емкость CMS2"',
  "Интерфейс управления распределением сжатого воздуха",
  "Датчик кислорода",
]);
pushOpts(8810, [
  "Шланги сжатого воздуха и управляющего воздуха (комплект)",
  "Клапан управления 1",
  "Клапан управления 2",
  'Реле давления "Вход сжатого воздуха"',
  'Редуктор давления "Управляющий воздух"',
  'Редуктор давления "Датчик кислорода"',
]);

unifiedHead(18, 8901);
item(8905, 18, 5, "cshu_number", "Номер ЦШУ", "Укажите номер шкафа/щита на табличке", ft.text, true);
item(8906, 18, 6, "cshu_inspect", "Осмотр ЦШУ, ЕЖН", null, ft.radio, false);
item(8907, 18, 7, "cshu_battery_model", "Модель/тип АКБ ЦШУ", null, ft.text, false);
item(8908, 18, 8, "cshu_battery_state", "Проверка состояния АКБ ЦШУ, ЕЖН", "Проверить визуально состояние АКБ. Замена не реже 1 раза в 2 года", ft.radio, false);
item(8909, 18, 9, "extra_cshu", "Дополнительные работы, ЦШУ", null, ft.textarea, false);
item(8910, 18, 10, "remarks_cshu", "Замечания и рекомендации, ЦШУ", null, ft.textarea, false);
item(8911, 18, 11, "end_date", "Дата окончания", null, ft.date, false);
item(8912, 18, 12, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
pushOpts(8906, ["Осмотрено", "Не выполнялось"]);
pushOpts(8908, ["Норма", "Отклонение", "Замена", "Не выполнялось"]);

unifiedHead(19, 9001);
item(9005, 19, 5, "shuzz_number", "Номер ШУЗЗ", "Укажите номер шкафа/щита на табличке", ft.text, true);
item(9006, 19, 6, "shuzz_inspect", "Осмотр ШУЗЗ, ЕЖН", null, ft.radio, false);
item(9007, 19, 7, "shuzz_battery_model", "Модель/тип АКБ ШУЗЗ", null, ft.text, false);
item(9008, 19, 8, "shuzz_battery_state", "Проверка состояния АКБ ШУЗЗ, ЕЖН", "Проверить визуально состояние АКБ. Замена не реже 1 раза в 2 года", ft.radio, false);
item(9009, 19, 9, "extra_shuzz", "Дополнительные работы, ШУЗЗ", null, ft.textarea, false);
item(9010, 19, 10, "remarks_shuzz", "Замечания и рекомендации, ШУЗЗ", null, ft.textarea, false);
item(9011, 19, 11, "end_date", "Дата окончания", null, ft.date, false);
item(9012, 19, 12, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
pushOpts(9006, ["Осмотрено", "Не выполнялось"]);
pushOpts(9008, ["Норма", "Отклонение", "Замена", "Не выполнялось"]);

const dcmIntro =
  "Устройства имеют систему самодиагностики и постоянно проверяются в повседневной эксплуатации. Проверка устройств производится в случае сообщений о неисправности. Замена компонентов производится согласно регламенту.";
add(
  `UPDATE checklist_templates SET intro_modal_text = '${esc(dcmIntro)}', safety_modal_text = '${esc(dcmIntro)}', red_button_enabled = 1 WHERE id = 20;`
);
unifiedHead(20, 9101);
item(9105, 20, 5, "dcm_model", "Модель/тип устройства", null, ft.text, true);
item(9106, 20, 6, "dcm_fault_check", "Проверка устройства по сообщению о неисправности", null, ft.radio, false);
item(9107, 20, 7, "extra_dcm", "Дополнительные работы, ДКМ", null, ft.textarea, false);
item(9108, 20, 8, "remarks_dcm", "Замечания и рекомендации, ДКМ", null, ft.textarea, false);
item(9109, 20, 9, "end_date", "Дата окончания", null, ft.date, false);
item(9110, 20, 10, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
pushOpts(9106, ["Норма", "Отклонение", "Замена", "Не выполнено"]);

add("");
add("-- Варианты ответов");
optLines.forEach((l) => add(l));

writeFileSync(outPath, lines.join("\n") + "\n", "utf8");
console.log("Wrote", outPath);
