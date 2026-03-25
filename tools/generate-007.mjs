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

// INT-03 (ежегодное, ТО-3000/год)
let c3 = compressorBase(3, 5201);
item(c3++, 3, 13, "min_pressure_valve_3000", "Клапан минимального давления, ТО-3000/год", "Проверить клапан минимального давления и при необходимости привести в исправность, используя комплект изнашиваемых деталей.", ft.radio, false);
item(c3++, 3, 14, "safety_valve_3000", "Проверка предохранительного клапана, ТО-3000/год", null, ft.radio, false);
item(c3++, 3, 15, "oil_filter_3000", "Масляный фильтр, ТО-3000/год", "Замена после 3000 часов эксплуатации, но не позже, чем через год. При каждой замене масла.", ft.radio, false);
item(c3++, 3, 16, "separator_3000", "Маслоотделитель (сепаратор), ТО-3000/год", "Замена если разница между давлением в сети и давлением в системе превысит 0,8 бар.", ft.radio, false);
item(c3++, 3, 17, "air_filter_3000", "Фильтр всасывающий (воздушный), ТО-3000/год", "Замена при повреждениях, после двукратной чистки, через 3000 часов эксплуатации или ежегодно.", ft.radio, false);
item(c3++, 3, 18, "belts_3000", "Клиновые ремни, ТО-3000/год", "Проверка на наличие повреждений после 3000 часов эксплуатации, но не реже одного раза в год.", ft.radio, false);
item(c3++, 3, 19, "oil_thermostat_3000", "Регулятор масла (термостат), ТО-3000/год", "Проверить регулятор масла и при необходимости привести в исправность.", ft.radio, false);
item(c3++, 3, 20, "service_reset_3000", "Сброс интервала сервисного обслуживания, ТО-3000/год", null, ft.radio, false);
item(c3++, 3, 21, "extra_3000", "Дополнительные работы, ТО-3000/год", null, ft.textarea, false);
item(c3++, 3, 22, "remarks_3000", "Замечания и рекомендации, ТО-3000/год", null, ft.textarea, false);
item(c3++, 3, 23, "end_date", "Дата окончания", null, ft.date, false);
item(c3++, 3, 24, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
stdCompressorOpts(5201);
pushOpts(5213, ["Исправен", "Неисправен", "Замена деталей", "Не выполнялось"]);
pushOpts(5214, ["Исправен", "Неисправен", "Замена"]);
pushOpts(5215, ["Замена", "Не выполнено"]);
pushOpts(5216, ["Замена", "Не выполнено"]);
pushOpts(5217, ["Замена", "Чистка"]);
pushOpts(5218, ["Контроль", "Замена полная", "Замена частичная"]);
pushOpts(5219, ["Исправен", "Неисправен", "Замена деталей", "Не выполнено"]);
pushOpts(5220, ["Выполнено", "Не выполено"]);

// INT-04 (2 года, ТО-9000/2 года)
let c4 = compressorBase(4, 5301);
item(c4++, 4, 13, "min_pressure_valve_9000", "Клапан минимального давления, ТО-9000/2 года", "Проверить клапан минимального давления и при необходимости привести в исправность.", ft.radio, false);
item(c4++, 4, 14, "oil_filter_9000", "Масляный фильтр, ТО-9000/2 года", "Замена после 3000 часов эксплуатации, но не позже, чем через год.", ft.radio, false);
item(c4++, 4, 15, "oil_thermostat_9000", "Регулятор масла (термостат), ТО-9000/2 года", "Проверить регулятор масла и при необходимости привести в исправность.", ft.radio, false);
item(c4++, 4, 16, "separator_9000", "Маслоотделитель (сепаратор), ТО-9000/2 года", "Замена если разница между давлением в сети и давлением в системе превысит 0,8 бар.", ft.radio, false);
item(c4++, 4, 17, "air_filter_9000", "Фильтр всасывающий (воздушный), ТО-9000/2 года", "Замена при повреждениях, после двукратной чистки, через 3000 часов эксплуатации или ежегодно.", ft.radio, false);
item(c4++, 4, 18, "panel_filter_9000", "Фильтр приточного воздуха (панельный), ТО-9000/2 года", null, ft.radio, false);
item(c4++, 4, 19, "dynamic_reg_9000", "Динамический регулятор всасывания, ТО-9000/2 года", "Проверить регулятор всасывания и при необходимости заменить.", ft.radio, false);
item(c4++, 4, 20, "belts_9000", "Клиновые ремни, ТО-9000/2 года", "Заменить клиновые ремни", ft.radio, false);
item(c4++, 4, 21, "oil_change_9000", "Замена масла в резервуаре, ТО-9000/2 года", "При тяжёлых условиях эксплуатации сроки службы масла и фильтров сокращаются.", ft.radio, false);
item(c4++, 4, 22, "solenoid_9000", "Электромагнитный клапан (соленоидный), ТО-9000/2 года", "Проверить клапан и при необходимости заменить.", ft.radio, false);
item(c4++, 4, 23, "service_reset_9000", "Сброс интервала сервисного обслуживания, ТО-9000/2 года", null, ft.radio, false);
item(c4++, 4, 24, "extra_9000", "Дополнительные работы, ТО-9000/2 года", null, ft.textarea, false);
item(c4++, 4, 25, "remarks_9000", "Замечания и рекомендации, ТО-9000/2 года", null, ft.textarea, false);
item(c4++, 4, 26, "end_date", "Дата окончания", null, ft.date, false);
item(c4++, 4, 27, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
stdCompressorOpts(5301);
pushOpts(5313, ["Исправен", "Неисправен", "Замена деталей", "Не выполнялось"]);
pushOpts(5314, ["Замена", "Не выполнено"]);
pushOpts(5315, ["Исправен", "Неисправен", "Замена деталей", "Не выполнено"]);
pushOpts(5316, ["Замена", "Не выполнено"]);
pushOpts(5317, ["Замена", "Не выполнено"]);
pushOpts(5318, ["Замена", "Не выполнено"]);
pushOpts(5319, ["Исправен", "Неисправен", "Замена деталей", "Не выполнено"]);
pushOpts(5320, ["Заменить клиновые ремни", "Замена полная", "Замена частичная", "Не выполнено"]);
pushOpts(5321, ["Выполнено", "Не выполнено"]);
pushOpts(5322, ["Исправен", "Неисправен", "Замена", "Не выполнено"]);
pushOpts(5323, ["Выполнено", "Не выполено"]);

// INT-05 (1500)
let c5 = compressorBase(5, 5401);
item(c5++, 5, 13, "heat_exchanger_1500", "Теплообменник, ТО-1500", "Чистка маслоохладителя сжатым воздухом.", ft.radio, false);
item(c5++, 5, 14, "electro_section_1500", "Электросекция, ТО-1500", "Проверить затяжку электрических подключений. Чистка сжатым воздухом.", ft.radio, false);
item(c5++, 5, 15, "current_l1_1500", "Потребляемый ток под нагрузкой L1, ТО-1500", "Проверка величины потребляемого тока.", ft.text, false);
item(c5++, 5, 16, "current_l2_1500", "Потребляемый ток под нагрузкой L2, ТО-1500", "Проверка величины потребляемого тока.", ft.text, false);
item(c5++, 5, 17, "current_l3_1500", "Потребляемый ток под нагрузкой L3, ТО-1500", "Проверка величины потребляемого тока.", ft.text, false);
item(c5++, 5, 18, "extra_1500", "Дополнительные работы, ТО-1500", null, ft.textarea, false);
item(c5++, 5, 19, "remarks_1500", "Замечания и рекомендации, ТО-1500", null, ft.textarea, false);
item(c5++, 5, 20, "end_date", "Дата окончания", null, ft.date, false);
item(c5++, 5, 21, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
stdCompressorOpts(5401);
pushOpts(5413, ["Чистка", "Требует замены"]);
pushOpts(5414, ["Выполнено", "Не выполнено"]);

// INT-06 (3000) — как регламент 3000/год
let c6 = compressorBase(6, 5501);
item(c6++, 6, 13, "min_pressure_valve_3000", "Клапан минимального давления, ТО-3000/год", "Проверить клапан минимального давления и при необходимости привести в исправность, используя комплект изнашиваемых деталей.", ft.radio, false);
item(c6++, 6, 14, "safety_valve_3000", "Проверка предохранительного клапана, ТО-3000/год", null, ft.radio, false);
item(c6++, 6, 15, "oil_filter_3000", "Масляный фильтр, ТО-3000/год", "Замена после 3000 часов эксплуатации, но не позже, чем через год. При каждой замене масла.", ft.radio, false);
item(c6++, 6, 16, "separator_3000", "Маслоотделитель (сепаратор), ТО-3000/год", "Замена если разница между давлением в сети и давлением в системе превысит 0,8 бар.", ft.radio, false);
item(c6++, 6, 17, "air_filter_3000", "Фильтр всасывающий (воздушный), ТО-3000/год", "Замена при повреждениях, после двукратной чистки, через 3000 часов эксплуатации или ежегодно.", ft.radio, false);
item(c6++, 6, 18, "belts_3000", "Клиновые ремни, ТО-3000/год", "Проверка на наличие повреждений после 3000 часов эксплуатации, но не реже одного раза в год.", ft.radio, false);
item(c6++, 6, 19, "oil_thermostat_3000", "Регулятор масла (термостат), ТО-3000/год", "Проверить регулятор масла и при необходимости привести в исправность.", ft.radio, false);
item(c6++, 6, 20, "service_reset_3000", "Сброс интервала сервисного обслуживания, ТО-3000/год", null, ft.radio, false);
item(c6++, 6, 21, "extra_3000", "Дополнительные работы, ТО-3000/год", null, ft.textarea, false);
item(c6++, 6, 22, "remarks_3000", "Замечания и рекомендации, ТО-3000/год", null, ft.textarea, false);
item(c6++, 6, 23, "end_date", "Дата окончания", null, ft.date, false);
item(c6++, 6, 24, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
stdCompressorOpts(5501);
pushOpts(5513, ["Исправен", "Неисправен", "Замена деталей", "Не выполнялось"]);
pushOpts(5514, ["Исправен", "Неисправен", "Замена"]);
pushOpts(5515, ["Замена", "Не выполнено"]);
pushOpts(5516, ["Замена", "Не выполнено"]);
pushOpts(5517, ["Замена", "Чистка"]);
pushOpts(5518, ["Контроль", "Замена полная", "Замена частичная"]);
pushOpts(5519, ["Исправен", "Неисправен", "Замена деталей", "Не выполнено"]);
pushOpts(5520, ["Выполнено", "Не выполено"]);

// INT-07 (6000)
let c7 = compressorBase(7, 5601);
item(c7++, 7, 13, "min_pressure_valve_6000", "Клапан минимального давления, ТО-6000", "Проверить клапан минимального давления и при необходимости привести в исправность.", ft.radio, false);
item(c7++, 7, 14, "oil_filter_6000", "Масляный фильтр, ТО-6000", "Замена после 3000 часов эксплуатации, но не позже, чем через год.", ft.radio, false);
item(c7++, 7, 15, "separator_6000", "Маслоотделитель (сепаратор), ТО-6000", "Замена если разница между давлением в сети и давлением в системе превысит 0,8 бар.", ft.radio, false);
item(c7++, 7, 16, "air_filter_6000", "Фильтр всасывающий (воздушный), ТО-6000", "Замена при повреждениях, после двукратной чистки, через 3000 часов эксплуатации или ежегодно.", ft.radio, false);
item(c7++, 7, 17, "service_reset_6000", "Сброс интервала сервисного обслуживания, ТО-6000", null, ft.radio, false);
item(c7++, 7, 18, "extra_6000", "Дополнительные работы, ТО-6000", null, ft.textarea, false);
item(c7++, 7, 19, "remarks_6000", "Замечания и рекомендации, ТО-6000", null, ft.textarea, false);
item(c7++, 7, 20, "end_date", "Дата окончания", null, ft.date, false);
item(c7++, 7, 21, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
stdCompressorOpts(5601);
pushOpts(5613, ["Исправен", "Неисправен", "Замена деталей", "Не выполнялось"]);
pushOpts(5614, ["Замена", "Не выполнено"]);
pushOpts(5615, ["Замена", "Не выполнено"]);
pushOpts(5616, ["Замена", "Чистка"]);
pushOpts(5617, ["Выполнено", "Не выполено"]);

// INT-08 (9000)
let c8 = compressorBase(8, 5701);
item(c8++, 8, 13, "min_pressure_valve_9000", "Клапан минимального давления, ТО-9000/2 года", "Проверить клапан минимального давления и при необходимости привести в исправность.", ft.radio, false);
item(c8++, 8, 14, "oil_filter_9000", "Масляный фильтр, ТО-9000/2 года", "Замена после 3000 часов эксплуатации, но не позже, чем через год.", ft.radio, false);
item(c8++, 8, 15, "oil_thermostat_9000", "Регулятор масла (термостат), ТО-9000/2 года", "Проверить регулятор масла и при необходимости привести в исправность.", ft.radio, false);
item(c8++, 8, 16, "separator_9000", "Маслоотделитель (сепаратор), ТО-9000/2 года", "Замена если разница между давлением в сети и давлением в системе превысит 0,8 бар.", ft.radio, false);
item(c8++, 8, 17, "air_filter_9000", "Фильтр всасывающий (воздушный), ТО-9000/2 года", "Замена при повреждениях, после двукратной чистки, через 3000 часов эксплуатации или ежегодно.", ft.radio, false);
item(c8++, 8, 18, "panel_filter_9000", "Фильтр приточного воздуха (панельный), ТО-9000/2 года", null, ft.radio, false);
item(c8++, 8, 19, "dynamic_reg_9000", "Динамический регулятор всасывания, ТО-9000/2 года", "Проверить регулятор всасывания и при необходимости заменить.", ft.radio, false);
item(c8++, 8, 20, "belts_9000", "Клиновые ремни, ТО-9000/2 года", "Заменить клиновые ремни", ft.radio, false);
item(c8++, 8, 21, "oil_change_9000", "Замена масла в резервуаре, ТО-9000/2 года", "При тяжёлых условиях эксплуатации сроки службы масла и фильтров сокращаются.", ft.radio, false);
item(c8++, 8, 22, "solenoid_9000", "Электромагнитный клапан (соленоидный), ТО-9000/2 года", "Проверить клапан и при необходимости заменить.", ft.radio, false);
item(c8++, 8, 23, "service_reset_9000", "Сброс интервала сервисного обслуживания, ТО-9000/2 года", null, ft.radio, false);
item(c8++, 8, 24, "extra_9000", "Дополнительные работы, ТО-9000/2 года", null, ft.textarea, false);
item(c8++, 8, 25, "remarks_9000", "Замечания и рекомендации, ТО-9000/2 года", null, ft.textarea, false);
item(c8++, 8, 26, "end_date", "Дата окончания", null, ft.date, false);
item(c8++, 8, 27, "end_time", "Время окончания", "Указанное время и дата будет считаться окончанием работ", ft.time, false);
stdCompressorOpts(5701);
pushOpts(5713, ["Исправен", "Неисправен", "Замена деталей", "Не выполнялось"]);
pushOpts(5714, ["Замена", "Не выполнено"]);
pushOpts(5715, ["Исправен", "Неисправен", "Замена деталей", "Не выполнено"]);
pushOpts(5716, ["Замена", "Не выполнено"]);
pushOpts(5717, ["Замена", "Не выполнено"]);
pushOpts(5718, ["Замена", "Не выполнено"]);
pushOpts(5719, ["Исправен", "Неисправен", "Замена деталей", "Не выполнено"]);
pushOpts(5720, ["Замена полная", "Замена частичная", "Не выполнено"]);
pushOpts(5721, ["Выполнено", "Не выполнено"]);
pushOpts(5722, ["Исправен", "Неисправен", "Замена", "Не выполнено"]);
pushOpts(5723, ["Выполнено", "Не выполено"]);

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
