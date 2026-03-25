# Generates database/sqlite/007_mosarchive_full.sql (UTF-8)
$ErrorActionPreference = 'Stop'
$outPath = Join-Path $PSScriptRoot '..\database\sqlite\007_mosarchive_full.sql'
$sb = [System.Text.StringBuilder]::new()

function AppendSqlLine([string]$s) { [void]$sb.AppendLine($s) }

function SqlEscape([string]$s) {
    if ($null -eq $s) { return '' }
    return $s.Replace("'", "''")
}

AppendSqlLine '-- 007: intro_modal_text, Mosarchive templates (compressor + unified equipment).'
AppendSqlLine 'PRAGMA foreign_keys = ON;'
AppendSqlLine ''
AppendSqlLine 'ALTER TABLE checklist_templates ADD COLUMN intro_modal_text TEXT;'
AppendSqlLine ''

AppendSqlLine 'DELETE FROM checklist_response_multi_options;'
AppendSqlLine 'DELETE FROM checklist_responses;'
AppendSqlLine 'DELETE FROM checklist_template_item_options;'
AppendSqlLine 'DELETE FROM checklist_template_items;'
AppendSqlLine ''

$weeklySafety = @'
ВНИМАНИЕ! Перед всеми работами по техническому обслуживанию:
1. Отключить компрессор при помощи кнопки ВЫКЛ.
2. Привести в действие переключатель аварийного останова.
3. Разомкнуть устройство отключения от сети и обезопасить с помощью висячего замка от непреднамеренного повторного включения.
4. Разместить на устройстве управления предупреждающую табличку.
5. Проверить, действительно ли обесточены все детали установки.
6. Перед началом работы дать всем горячим элементам конструкции компрессора остыть до 50°C.
7. Отсоединить компрессор от сети сжатого воздуха. Для этого закрыть шаровой кран на выходе сжатого воздуха.
8. Удалить воздух из системы компрессора.
'@

$errorsIntro = 'При наличии ошибок и неисправностей следует соблюдать инструкции и рекомендации, указанные в руководстве по эксплуатации компрессора.'
$to1500 = 'Внимание! Обслуживание выполняется с ТО-1500.'
$annualIntro = "$errorsIntro`n$to1500"

AppendSqlLine "UPDATE checklist_templates SET intro_modal_text = NULL, safety_modal_text = '$(SqlEscape $weeklySafety)', red_button_enabled = 1, top_plate_text = 'Мосархив – Сахарово. Винтовой компрессор — еженедельное ТО' WHERE id = 1;"

foreach ($tid in 3,4,6,7,8) {
    AppendSqlLine "UPDATE checklist_templates SET intro_modal_text = '$(SqlEscape $annualIntro)', safety_modal_text = '$(SqlEscape $annualIntro)', red_button_enabled = 1 WHERE id = $tid;"
}
AppendSqlLine "UPDATE checklist_templates SET intro_modal_text = NULL, safety_modal_text = '$(SqlEscape $errorsIntro)', red_button_enabled = 1, top_plate_text = 'Винтовой компрессор — ежемесячное ТО' WHERE id = 2;"
AppendSqlLine "UPDATE checklist_templates SET intro_modal_text = '$(SqlEscape $errorsIntro)', safety_modal_text = '$(SqlEscape $errorsIntro)', red_button_enabled = 1, top_plate_text = 'ТО-1500 компрессора' WHERE id = 5;"
AppendSqlLine ''

AppendSqlLine "INSERT OR IGNORE INTO users (id, user_role_id, first_name, last_name, login, password_hash, is_active) VALUES"
AppendSqlLine "    (2, 1, 'Пётр', 'Петров', 'petrov', '`$2a`$11`$OfflinePlaceholderHashNotForAuth', 1),"
AppendSqlLine "    (3, 1, 'Сергей', 'Сидоров', 'sidorov', '`$2a`$11`$OfflinePlaceholderHashNotForAuth', 1),"
AppendSqlLine "    (4, 1, 'Анна', 'Козлова', 'kozlova', '`$2a`$11`$OfflinePlaceholderHashNotForAuth', 1),"
AppendSqlLine "    (5, 1, 'Иван', 'Иванов', 'ivanov', '`$2a`$11`$OfflinePlaceholderHashNotForAuth', 1);"
AppendSqlLine ''

$ftDate = 3; $ftTime = 4; $ftText = 1; $ftTextArea = 2; $ftNumber = 6
$ftRadio = 8; $ftCheckbox = 9; $ftDropdown = 10; $ftDropMulti = 11

function Emit-Item($id, $tid, $sort, $code, $q, $hint, $ft, $req, $valRule) {
    $h = SqlEscape $hint
    $c = if ($code) { "'$(SqlEscape $code)'" } else { 'NULL' }
    $vr = if ($valRule) { "'$(SqlEscape $valRule)'" } else { 'NULL' }
    $hintSql = if ($hint) { "'$h'" } else { 'NULL' }
    "INSERT INTO checklist_template_items (id, checklist_template_id, sort_order, field_code, question_text, hint_text, field_type_id, is_required, validation_rule_code) VALUES ($id, $tid, $sort, $c, '$(SqlEscape $q)', $hintSql, $ft, $req, $vr);"
}

function Emit-Opt($id, $itemId, $sort, $label) {
    "INSERT INTO checklist_template_item_options (id, checklist_template_item_id, sort_order, option_label) VALUES ($id, $itemId, $sort, '$(SqlEscape $label)');"
}

function Compressor-BaseItems($templateId, [int]$idStart) {
    $rows = [System.Collections.Generic.List[string]]::new()
    $n = $idStart
    $rows.Add((Emit-Item $n $templateId 1 'start_date' 'Дата начала' 'Строго дд.мм.гггг (поле календаря)' $ftDate 1 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 2 'start_time' 'Время начала' 'Строго чч:мм' $ftTime 1 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 3 'workers' 'Лица, производившие работы' 'Можно выбрать несколько' $ftDropMulti 1 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 4 'unit_number' 'Номер установки' $null $ftDropdown 1 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 5 'equipment_pick' 'Оборудование' $null $ftDropdown 1 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 6 'comp_model' 'Модель компрессора' $null $ftDropdown 0 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 7 'comp_type' 'Тип компрессора' $null $ftDropdown 0 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 8 'comp_state' 'Состояние компрессора' 'Выберите состояние оборудования до начала работ' $ftCheckbox 0 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 9 'operating_hours' 'Часы эксплуатации компрессора' 'Можно указать часы и минуты' $ftText 0 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 10 'pressure_network' 'Давление в сети Pn (bar)' $null $ftText 0 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 11 'pressure_system' 'Давление в системе Ps (bar)' $null $ftText 0 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 12 'final_temp' 'Конечная температура сжатия' 'Проверить заданное значение: 70...100°C' $ftNumber 0 'integer_range_70_100')); $n++
    [pscustomobject]@{ Rows = $rows; NextId = $n; WorkersId = ($idStart + 2); UnitId = ($idStart + 3); EquipId = ($idStart + 4); StateId = ($idStart + 7) }
}

$optId = 60001
$optLines = [System.Collections.Generic.List[string]]::new()

function Push-Opt($itemId, $labels) {
    $so = 1
    foreach ($lb in $labels) {
        $optLines.Add((Emit-Opt $script:optId $itemId $so $lb))
        $script:optId++
        $so++
    }
}

function Push-WorkersFor($workersItemId) {
    Push-Opt $workersItemId @('Демо Инженер', 'Петров Пётр Петрович', 'Сидоров Сергей Сидорович', 'Козлова Анна', 'Иванов Иван Иванович')
}

function Add-CompressorStdOpts($idStart) {
    Push-WorkersFor ($idStart + 2)
    Push-Opt ($idStart + 3) @('G301', '301', 'G302', '302')
    Push-Opt ($idStart + 4) @('Винтовой компрессор', 'Электродвигатель компрессора', 'Осушитель холодильного типа', 'Циклонный сепаратор', 'Фильтры очистки', 'Угольный адсорбер', 'Конденсатоотводчики', 'Водомасляный сепаратор', 'Ресиверы', 'Газоразделительный модуль', 'Центральный шкаф управления', 'Шкаф управления зоной защиты', 'Датчики, контроллеры и модули')
    Push-Opt ($idStart + 5) @('Atlas Copco GA', 'Ingersoll Rand R', 'Другое')
    Push-Opt ($idStart + 6) @('Стационарный', 'Мобильный', 'Другое')
    Push-Opt ($idStart + 7) @('Рабочее', 'Под нагрузкой', 'Выключен', 'Не рабочее')
}

function Push-Radio2($itemId, $a, $b) { Push-Opt $itemId @($a, $b) }
function Push-Radio3($itemId, $a, $b, $c) { Push-Opt $itemId @($a, $b, $c) }
function Push-Radio4($itemId, $a, $b, $c, $d) { Push-Opt $itemId @($a, $b, $c, $d) }

# --- Items ---
$b1 = Compressor-BaseItems 1 5001
foreach ($r in $b1.Rows) { AppendSqlLine $r }
$cur = $b1.NextId
AppendSqlLine (Emit-Item $cur 1 13 'display_eval_weekly' 'Оценка отображаемых на дисплее данных, ЕЖН' 'Введите код ошибки или иные сервисные сообщения' $ftText 0 $null); $cur++
AppendSqlLine (Emit-Item $cur 1 14 'leak_check_weekly' 'Проверка наличия негерметичностей, ЕЖН' 'Визуальная проверка на наличие негерметичности' $ftRadio 0 $null); $leakW = $cur; $cur++
AppendSqlLine (Emit-Item $cur 1 15 'diff_pressure_weekly' 'Текущий дифференциал давления, ЕЖН' 'Проверить разницу между давлением в сети и системой, заданное значение 0 – 1,5 бар' $ftRadio 0 $null); $diffW = $cur; $cur++
AppendSqlLine (Emit-Item $cur 1 16 'filter_panel_weekly' 'Фильтр приточного воздуха (панельный), ЕЖН' 'Визуальная проверка, при необходимости замена' $ftRadio 0 $null); $filtW = $cur; $cur++
AppendSqlLine (Emit-Item $cur 1 17 'extra_weekly' 'Дополнительные работы, ЕЖН ТО' $null $ftTextArea 0 $null); $cur++
AppendSqlLine (Emit-Item $cur 1 18 'remarks_weekly' 'Замечания и рекомендации, ЕЖН ТО' $null $ftTextArea 0 $null); $cur++
AppendSqlLine (Emit-Item $cur 1 19 'end_date' 'Дата окончания' $null $ftDate 0 $null); $cur++
AppendSqlLine (Emit-Item $cur 1 20 'end_time' 'Время окончания' 'Указанное время и дата будет считаться окончанием работ' $ftTime 0 $null); $cur++

Add-CompressorStdOpts 5001
Push-Radio2 $leakW 'Есть' 'Отсутствует'
Push-Radio2 $diffW 'Норма' 'Отклонение'
Push-Radio3 $filtW 'Норма' 'Замена' 'Чистка'

$b2 = Compressor-BaseItems 2 5101
foreach ($r in $b2.Rows) { AppendSqlLine $r }
$cur2 = $b2.NextId
AppendSqlLine (Emit-Item $cur2 2 13 'emergency_switch_monthly' 'Переключатель аварийного останова, ЕЖМ' 'Проверить функционирование переключателя аварийного останова' $ftRadio 0 $null); $esw = $cur2; $cur2++
AppendSqlLine (Emit-Item $cur2 2 14 'work_pressure_monthly' 'Проверка рабочего давления, ЕЖМ' 'Проверить и при необходимости подрегулировать рабочее давление' $ftRadio 0 $null); $wp = $cur2; $cur2++
AppendSqlLine (Emit-Item $cur2 2 15 'suction_filter_monthly' 'Фильтр всасывающий (воздушный), ЕЖМ' 'Очистить от загрязнений и при необходимости заменить' $ftRadio 0 $null); $sf = $cur2; $cur2++
AppendSqlLine (Emit-Item $cur2 2 16 'oil_level_monthly' 'Уровень масла в резервуаре, ЕЖМ' 'Проверить уровень масла и при необходимости долить' $ftRadio 0 $null); $oil = $cur2; $cur2++
AppendSqlLine (Emit-Item $cur2 2 17 'temp_ped' 'Температура корпуса ПЭД' 'Измерить пирометром температуру корпуса приводного электродвигателя' $ftText 0 $null); $cur2++
AppendSqlLine (Emit-Item $cur2 2 18 'temp_edo' 'Температура корпуса ЭДО' 'Измерить пирометром температуру корпуса электродвигателя охладителя' $ftText 0 $null); $cur2++
AppendSqlLine (Emit-Item $cur2 2 19 'extra_monthly' 'Дополнительные работы, ЕЖМ ТО' $null $ftTextArea 0 $null); $cur2++
AppendSqlLine (Emit-Item $cur2 2 20 'remarks_monthly' 'Замечания и рекомендации, ЕЖМ ТО' $null $ftTextArea 0 $null); $cur2++
AppendSqlLine (Emit-Item $cur2 2 21 'end_date' 'Дата окончания' $null $ftDate 0 $null); $cur2++
AppendSqlLine (Emit-Item $cur2 2 22 'end_time' 'Время окончания' 'Указанное время и дата будет считаться окончанием работ' $ftTime 0 $null); $cur2++

Add-CompressorStdOpts 5101
Push-Radio2 $esw 'Рабочий' 'Не рабочий'
Push-Radio2 $wp 'Норма' 'Отклонение'
Push-Radio2 $sf 'Чистка' 'Замена'
Push-Radio2 $oil 'Норма' 'Долито'

for ($ti = 3; $ti -le 8; $ti++) {
    $start = 5200 + ($ti - 3) * 100 + 1
    $bb = Compressor-BaseItems $ti $start
    foreach ($r in $bb.Rows) { AppendSqlLine $r }
    $cn = $bb.NextId
    AppendSqlLine (Emit-Item $cn $ti 13 'regulation_notes' 'Пункты контрольного листа по выбранному виду ТО' 'Полный перечень полей ТО-3000/6000/9000 и т.д. будет добавлен в следующей итерации БД' $ftTextArea 0 $null); $cn++
    AppendSqlLine (Emit-Item $cn $ti 14 'extra_typed' 'Дополнительные работы' $null $ftTextArea 0 $null); $cn++
    AppendSqlLine (Emit-Item $cn $ti 15 'remarks_typed' 'Замечания и рекомендации' $null $ftTextArea 0 $null); $cn++
    AppendSqlLine (Emit-Item $cn $ti 16 'end_date' 'Дата окончания' $null $ftDate 0 $null); $cn++
    AppendSqlLine (Emit-Item $cn $ti 17 'end_time' 'Время окончания' 'Указанное время и дата будет считаться окончанием работ' $ftTime 0 $null); $cn++
    Add-CompressorStdOpts $start
}

$motorIntro = 'ТО - 1400. При наличии ошибок и неисправностей следует соблюдать инструкции и рекомендации, указанные в руководстве по эксплуатации.'
AppendSqlLine "UPDATE checklist_templates SET red_button_enabled = 1, intro_modal_text = '$(SqlEscape $motorIntro)', safety_modal_text = '$(SqlEscape $motorIntro)' WHERE id = 9;"

AppendSqlLine (Emit-Item 8001 9 1 'start_date' 'Дата начала' $null $ftDate 1 $null)
AppendSqlLine (Emit-Item 8002 9 2 'start_time' 'Время начала' $null $ftTime 1 $null)
AppendSqlLine (Emit-Item 8003 9 3 'workers' 'Лица, производившие работы' 'Можно выбрать несколько' $ftDropMulti 1 $null)
AppendSqlLine (Emit-Item 8004 9 4 'unit_number' 'Номер установки' $null $ftDropdown 1 $null)
AppendSqlLine (Emit-Item 8005 9 5 'motor_model' 'Модель/тип ПЭД' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8006 9 6 'motor_hours_note' 'Часы эксплуатации компрессора при ТО ПЭД' 'Указать часы эксплуатации компрессора при смазке подшипников двигателя' $ftText 0 $null)
AppendSqlLine (Emit-Item 8007 9 7 'bearing_grease' 'Смазка подшипников ПЭД, ТО-1400' 'Смазать подшипники приводного двигателя (в случае двигателей без устройства дополнительной смазки). Смазка производится при работающем двигателе. См. данные на табличке приводного двигателя.' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8008 9 8 'service_reset_motor' 'Сброс интервала сервисного обслуживания ПЭД, ТО-1400' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8009 9 9 'extra_motor' 'Дополнительные работы, ТО ПЭД' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8010 9 10 'remarks_motor' 'Замечания и рекомендации, ТО ПЭД' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8011 9 11 'end_date' 'Дата окончания' $null $ftDate 0 $null)
AppendSqlLine (Emit-Item 8012 9 12 'end_time' 'Время окончания' 'Указанное время и дата будет считаться окончанием работ' $ftTime 0 $null)
Push-WorkersFor 8003
Push-Opt 8004 @('G301', '301', 'G302', '302')
Push-Radio2 8007 'Выполнено' 'Не выполнено'
Push-Radio2 8008 'Выполнено' 'Не выполнено'

AppendSqlLine "UPDATE checklist_templates SET red_button_enabled = 1, intro_modal_text = '$(SqlEscape $errorsIntro)', safety_modal_text = '$(SqlEscape $errorsIntro)' WHERE id = 10;"

AppendSqlLine (Emit-Item 8101 10 1 'start_date' 'Дата начала' $null $ftDate 1 $null)
AppendSqlLine (Emit-Item 8102 10 2 'start_time' 'Время начала' $null $ftTime 1 $null)
AppendSqlLine (Emit-Item 8103 10 3 'workers' 'Лица, производившие работы' $null $ftDropMulti 1 $null)
AppendSqlLine (Emit-Item 8104 10 4 'unit_number' 'Номер установки' $null $ftDropdown 1 $null)
AppendSqlLine (Emit-Item 8105 10 5 'oht_model' 'Модель/тип ОХТ' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8106 10 6 'oht_hours' 'Общее число рабочих часов осушителя' 'Параметр А2/А3. Рабочие часы = А3×1000+А2' $ftText 0 $null)
AppendSqlLine (Emit-Item 8107 10 7 'fridge_hours' 'Общее число рабочих часов холодильного компрессора' 'Параметр А4/А5. Рабочие часы = А5×1000+А4' $ftText 0 $null)
AppendSqlLine (Emit-Item 8108 10 8 'compressor_out_temp' 'Температура на выходе компрессора ОХТ (линия нагнетания)' 'Параметр b8' $ftText 0 $null)
AppendSqlLine (Emit-Item 8109 10 9 'led_state_oht' 'Проверка состояния индикации (светодиодов) ОХТ, ЕЖН' 'Проверка: горит индикатор POWER ON, индикаторов панели управления' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8110 10 10 'display_oht' 'Оценка отображаемых на дисплее данных ОХТ, ЕЖН' 'Введите код ошибки или иные сервисные сообщения' $ftText 0 $null)
AppendSqlLine (Emit-Item 8111 10 11 'condensate_device_oht' 'Проверка устройства слива конденсата ОХТ, ЕЖН' 'Для активации кратко нажать кнопку диагностики конденсатоотводчика' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8112 10 12 'fins_clean_oht' 'Чистка рёбер конденсатора ОХТ, 4 мес' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8113 10 13 'current_l1_oht' 'Потребляемый ток под нагрузкой L1 ОХТ, 4 мес' 'Проверка величины потребляемого тока' $ftText 0 $null)
AppendSqlLine (Emit-Item 8114 10 14 'current_l2_oht' 'Потребляемый ток под нагрузкой L2 ОХТ, 4 мес' 'Проверка величины потребляемого тока' $ftText 0 $null)
AppendSqlLine (Emit-Item 8115 10 15 'current_l3_oht' 'Потребляемый ток под нагрузкой L3 ОХТ, 4 мес' 'Проверка величины потребляемого тока' $ftText 0 $null)
AppendSqlLine (Emit-Item 8116 10 16 'oht_safety_gate' 'Перед дальнейшими пунктами' 'ВНИМАНИЕ! Прежде чем приступить к выполнению любой операции ТО проверьте: отсутствие давления в пневматическом контуре; осушитель отключен от электрической сети.' $ftText 0 $null)
AppendSqlLine (Emit-Item 8117 10 17 'leak_refrigerant' 'Проверка системы на утечку хладагента ОХТ, ЕЖГ' 'Визуальная проверка соединений и магистралей' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8118 10 18 'temp_sensor_oht' 'Проверка датчика температуры ОХТ, ЕЖГ' 'Проверить датчики температуры. Заменить их, если необходимо' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8119 10 19 'kits_oht' 'Установка комплектов для ТО ОХТ, 3 года' $null $ftCheckbox 0 $null)
AppendSqlLine (Emit-Item 8120 10 20 'extra_oht' 'Дополнительные работы, ТО ОХТ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8121 10 21 'remarks_oht' 'Замечания и рекомендации, ТО ОХТ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8122 10 22 'end_date' 'Дата окончания' $null $ftDate 0 $null)
AppendSqlLine (Emit-Item 8123 10 23 'end_time' 'Время окончания' 'Указанное время и дата будет считаться окончанием работ' $ftTime 0 $null)
Push-WorkersFor 8103
Push-Opt 8104 @('G301', '301', 'G302', '302')
Push-Radio3 8109 'Исправна' 'Не работает' 'Выключена'
Push-Radio3 8111 'Исправно' 'Неисправно' 'Не выполнено'
Push-Radio2 8112 'Выполнено' 'Не выполнено'
Push-Radio2 8117 'Есть' 'Отсутствует'
Push-Radio3 8118 'Контроль' 'Замена' 'Не выполнялось'
Push-Opt 8119 @('комплекты для компрессора', 'комплекты для вентилятора', 'комплекты для клапана горячего газа', 'комплекты для испарителя')

AppendSqlLine (Emit-Item 8201 11 1 'start_date' 'Дата начала' $null $ftDate 1 $null)
AppendSqlLine (Emit-Item 8202 11 2 'start_time' 'Время начала' $null $ftTime 1 $null)
AppendSqlLine (Emit-Item 8203 11 3 'workers' 'Лица, производившие работы' $null $ftDropMulti 1 $null)
AppendSqlLine (Emit-Item 8204 11 4 'unit_number' 'Номер установки' $null $ftDropdown 1 $null)
AppendSqlLine (Emit-Item 8205 11 5 'cyclone_model' 'Модель/тип ЦС' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8206 11 6 'cyclone_state' 'Проверка состояния ЦС' 'Визуальный контроль состояния конденсатоотводчика' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8207 11 7 'extra_cyclone' 'Дополнительные работы, ЦС' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8208 11 8 'remarks_cyclone' 'Замечания и рекомендации, ЦС' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8209 11 9 'end_date' 'Дата окончания' $null $ftDate 0 $null)
AppendSqlLine (Emit-Item 8210 11 10 'end_time' 'Время окончания' 'Указанное время и дата будет считаться окончанием работ' $ftTime 0 $null)
AppendSqlLine "UPDATE checklist_templates SET intro_modal_text = '$(SqlEscape $errorsIntro)', safety_modal_text = '$(SqlEscape $errorsIntro)', red_button_enabled = 1 WHERE id = 11;"
Push-WorkersFor 8203
Push-Opt 8204 @('G301', '301', 'G302', '302')
Push-Radio4 8206 'Исправен' 'Авария' 'Чистка' 'Не выполнялось'

AppendSqlLine (Emit-Item 8301 12 1 'start_date' 'Дата начала' $null $ftDate 1 $null)
AppendSqlLine (Emit-Item 8302 12 2 'start_time' 'Время начала' $null $ftTime 1 $null)
AppendSqlLine (Emit-Item 8303 12 3 'workers' 'Лица, производившие работы' $null $ftDropMulti 1 $null)
AppendSqlLine (Emit-Item 8304 12 4 'unit_number' 'Номер установки' $null $ftDropdown 1 $null)
AppendSqlLine (Emit-Item 8305 12 5 'filter_model' 'Модель/тип фильтра' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8306 12 6 'filter_place' 'Место установки фильтра' 'Выберите место установки в сети' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8307 12 7 'filter_element' 'Проверка фильтрующего элемента, ЕЖН' 'Проверьте индикацию на дифференциальном манометре (опционально)' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8308 12 8 'extra_filters' 'Дополнительные работы по фильтрам' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8309 12 9 'remarks_filters' 'Замечания и рекомендации по фильтрам' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8310 12 10 'end_date' 'Дата окончания' $null $ftDate 0 $null)
AppendSqlLine (Emit-Item 8311 12 11 'end_time' 'Время окончания' 'Указанное время и дата будет считаться окончанием работ' $ftTime 0 $null)
AppendSqlLine "UPDATE checklist_templates SET intro_modal_text = 'Замена фильтрующих элементов не реже 1 раза в год.', safety_modal_text = 'Замена фильтрующих элементов не реже 1 раза в год.', red_button_enabled = 1 WHERE id = 12;"
Push-WorkersFor 8303
Push-Opt 8304 @('G301', '301', 'G302', '302')
Push-Radio2 8306 'Сжатый воздух' 'Азот'
Push-Radio3 8307 'Норма' 'Отклонение' 'Замена'

$adsIntro = "Адсорберы на основе активированного угля. $errorsIntro"
AppendSqlLine "UPDATE checklist_templates SET intro_modal_text = '$(SqlEscape $adsIntro)', safety_modal_text = '$(SqlEscape $adsIntro)', red_button_enabled = 1 WHERE id = 13;"

AppendSqlLine (Emit-Item 8401 13 1 'start_date' 'Дата начала' $null $ftDate 1 $null)
AppendSqlLine (Emit-Item 8402 13 2 'start_time' 'Время начала' $null $ftTime 1 $null)
AppendSqlLine (Emit-Item 8403 13 3 'workers' 'Лица, производившие работы' $null $ftDropMulti 1 $null)
AppendSqlLine (Emit-Item 8404 13 4 'unit_number' 'Номер установки' $null $ftDropdown 1 $null)
AppendSqlLine (Emit-Item 8405 13 5 'ads_model' 'Модель/тип адсорбера' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8406 13 6 'ads_damage' 'Проверка на наличие повреждений АДС, ЕЖН' 'Визуальный осмотр корпуса и соединений' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8407 13 7 'ads_pressure_gauge' 'Проверка уровня давления на манометре АДС, ЕЖН' 'Визуальная проверка уровня давления на манометре' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8408 13 8 'ads_tube' 'Индикаторная трубка с механизмом АДС, ЕЖН' 'Замена индикаторной трубки производится после изменения цвета' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8409 13 9 'ads_oil_residual' 'Проверка остаточного содержания масла в АДС, ЕЖМ' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8410 13 10 'ads_carbon_year' 'Замена активированного угля в АДС, ГОД' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8411 13 11 'extra_ads' 'Дополнительные работы, АДС' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8412 13 12 'remarks_ads' 'Замечания и рекомендации, АДС' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8413 13 13 'end_date' 'Дата окончания' $null $ftDate 0 $null)
AppendSqlLine (Emit-Item 8414 13 14 'end_time' 'Время окончания' 'Указанное время и дата будет считаться окончанием работ' $ftTime 0 $null)
Push-WorkersFor 8403
Push-Opt 8404 @('G301', '301', 'G302', '302')
Push-Radio3 8406 'Норма' 'Отклонение' 'Не выполнялось'
Push-Radio2 8407 'Норма' 'Отклонение'
Push-Radio2 8408 'Контроль' 'Замена'
Push-Radio3 8409 'Норма' 'Отклонение' 'Не выполнялось'
Push-Radio2 8410 'Выполнено' 'Не выполнено'

# --- Cond 14, WMS 15, Receiver 16, GRM 17, CAB 18-19, DKM 20: compact ---
function Unified-Head($tid, $id0) {
    AppendSqlLine (Emit-Item ($id0+0) $tid 1 'start_date' 'Дата начала' $null $ftDate 1 $null)
    AppendSqlLine (Emit-Item ($id0+1) $tid 2 'start_time' 'Время начала' $null $ftTime 1 $null)
    AppendSqlLine (Emit-Item ($id0+2) $tid 3 'workers' 'Лица, производившие работы' $null $ftDropMulti 1 $null)
    AppendSqlLine (Emit-Item ($id0+3) $tid 4 'unit_number' 'Номер установки' $null $ftDropdown 1 $null)
    Push-WorkersFor ($id0+2)
    Push-Opt ($id0+3) @('G301', '301', 'G302', '302')
}

AppendSqlLine "UPDATE checklist_templates SET intro_modal_text = '$(SqlEscape $errorsIntro)', safety_modal_text = '$(SqlEscape $errorsIntro)', red_button_enabled = 1 WHERE id IN (14,15,17,18,19,20);"
AppendSqlLine "UPDATE checklist_templates SET intro_modal_text = NULL, safety_modal_text = 'Еженедельное техническое обслуживание.', red_button_enabled = 1 WHERE id = 16;"

Unified-Head 14 8501
AppendSqlLine (Emit-Item 8505 14 5 'cond_model' 'Модель/тип КО' 'Если в сети одинаковые модели, укажите серийный номер' $ftText 1 $null)
AppendSqlLine (Emit-Item 8506 14 6 'cond_led' 'Проверка состояния индикации (светодиодов) КО, ЕЖН' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8507 14 7 'cond_drain' 'Проверка работы и отвода конденсата КО, ЕЖН' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8508 14 8 'cond_clean' 'Чистка корпуса и клапана КО, ЕЖГ' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8509 14 9 'cond_wear' 'Замена изнашивающихся деталей КО, ЕЖГ' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8510 14 10 'extra_cond' 'Дополнительные работы, КО' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8511 14 11 'remarks_cond' 'Замечания и рекомендации, КО' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8512 14 12 'end_date' 'Дата окончания' $null $ftDate 0 $null)
AppendSqlLine (Emit-Item 8513 14 13 'end_time' 'Время окончания' 'Указанное время и дата будет считаться окончанием работ' $ftTime 0 $null)
Push-Radio3 8506 'Исправна' 'Неисправна' 'Не выполнено'
Push-Radio3 8507 'Исправна' 'Неисправна' 'Не выполнено'
Push-Radio2 8508 'Выполнено' 'Не выполнено'
Push-Radio2 8509 'Выполнено' 'Не выполнено'

Unified-Head 15 8601
AppendSqlLine (Emit-Item 8605 15 5 'wms_model' 'Модель/тип ВМС' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8606 15 6 'wms_indicators' 'Проверка состояния индикаторов ВМС, ЕЖМ' 'Визуальный контроль за индикаторами водомасляного сепаратора' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8607 15 7 'wms_filters' 'Замена фильтров по индикаторам, ВМС' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8608 15 8 'extra_wms' 'Дополнительные работы, ВМС' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8609 15 9 'remarks_wms' 'Замечания и рекомендации, ВМС' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8610 15 10 'end_date' 'Дата окончания' $null $ftDate 0 $null)
AppendSqlLine (Emit-Item 8611 15 11 'end_time' 'Время окончания' 'Указанное время и дата будет считаться окончанием работ' $ftTime 0 $null)
Push-Radio2 8606 'Норма' 'Отклонение'
Push-Radio2 8607 'Выполнено' 'Не выполнено'

Unified-Head 16 8701
AppendSqlLine (Emit-Item 8705 16 5 'recv_model' 'Модель/тип ресивера' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8706 16 6 'recv_place' 'Место установки ресивера' 'Выберите место установки в сети' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8707 16 7 'recv_drain' 'Проверка на содержание частиц или жидкости (вода/масло)' 'Ненадолго приоткройте на дне ресивера ручной клапан сброса конденсата' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8708 16 8 'recv_leak' 'Проверка наличия негерметичностей' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8709 16 9 'recv_pressure' 'Проверка уровня давления на манометре' 'Визуальная проверка уровня давления на манометре' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8710 16 10 'extra_recv' 'Дополнительные работы по ресиверу' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8711 16 11 'remarks_recv' 'Замечания и рекомендации по ресиверу' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8712 16 12 'end_date' 'Дата окончания' $null $ftDate 0 $null)
AppendSqlLine (Emit-Item 8713 16 13 'end_time' 'Время окончания' 'Указанное время и дата будет считаться окончанием работ' $ftTime 0 $null)
Push-Radio2 8706 'Сжатый воздух' 'Азот'
Push-Radio2 8707 'Выполнено' 'Не выполнялось'
Push-Radio3 8708 'Есть' 'Отсутствуют' 'Не выполнялось'
Push-Radio2 8709 'Норма' 'Отклонение'

Unified-Head 17 8801
AppendSqlLine (Emit-Item 8805 17 5 'grm_model' 'Модель/тип ГРМ' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8806 17 6 'grm_main_switch' 'Проверьте положение главного выключателя на модуле, ЕЖН' 'Выключатель должен находиться в положении ON' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8807 17 7 'grm_states' 'Проверка заданных состояний и предельных значений ГРМ, ЕЖН' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8808 17 8 'grm_pressure' 'Проверка уровня давления на манометре ГРМ, ЕЖН' 'Визуальная проверка уровня давления на манометре' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8809 17 9 'grm_replace_3y' 'Замена оборудования ГРМ, каждые 3 года' 'Отметьте какое оборудование было заменено' $ftCheckbox 0 $null)
AppendSqlLine (Emit-Item 8810 17 10 'grm_replace_5y' 'Замена оборудования ГРМ, каждые 5 лет' 'Отметьте какое оборудование было заменено' $ftCheckbox 0 $null)
AppendSqlLine (Emit-Item 8811 17 11 'extra_grm' 'Дополнительные работы, ГРМ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8812 17 12 'remarks_grm' 'Замечания и рекомендации, ГРМ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8813 17 13 'end_date' 'Дата окончания' $null $ftDate 0 $null)
AppendSqlLine (Emit-Item 8814 17 14 'end_time' 'Время окончания' 'Указанное время и дата будет считаться окончанием работ' $ftTime 0 $null)
Push-Radio2 8806 'Включен' 'Отключен'
Push-Radio2 8807 'Норма' 'Отклонение'
Push-Radio2 8808 'Норма' 'Отклонение'
Push-Opt 8809 @('Манометр «Вход сжатого воздуха»', 'Манометр «Накопительная емкость CMS1»', 'Манометр «Накопительная емкость CMS2»', 'Интерфейс управления распределением сжатого воздуха', 'Датчик кислорода')
Push-Opt 8810 @('Шланги сжатого воздуха и управляющего воздуха (комплект)', 'Клапан управления 1', 'Клапан управления 2', 'Реле давления «Вход сжатого воздуха»', 'Редуктор давления «Управляющий воздух»', 'Редуктор давления «Датчик кислорода»')

Unified-Head 18 8901
AppendSqlLine (Emit-Item 8905 18 5 'cshu_number' 'Номер ЦШУ' 'Укажите номер шкафа/щита на табличке' $ftText 1 $null)
AppendSqlLine (Emit-Item 8906 18 6 'cshu_inspect' 'Осмотр ЦШУ, ЕЖН' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8907 18 7 'cshu_battery_model' 'Модель/тип АКБ ЦШУ' $null $ftText 0 $null)
AppendSqlLine (Emit-Item 8908 18 8 'cshu_battery_state' 'Проверка состояния АКБ ЦШУ, ЕЖН' 'Проверить визуально состояние АКБ. Замена не реже 1 раза в 2 года' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8909 18 9 'extra_cshu' 'Дополнительные работы, ЦШУ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8910 18 10 'remarks_cshu' 'Замечания и рекомендации, ЦШУ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8911 18 11 'end_date' 'Дата окончания' $null $ftDate 0 $null)
AppendSqlLine (Emit-Item 8912 18 12 'end_time' 'Время окончания' 'Указанное время и дата будет считаться окончанием работ' $ftTime 0 $null)
Push-Radio2 8906 'Осмотрено' 'Не выполнялось'
Push-Radio4 8908 'Норма' 'Отклонение' 'Замена' 'Не выполнялось'

Unified-Head 19 9001
AppendSqlLine (Emit-Item 9005 19 5 'shuzz_number' 'Номер ШУЗЗ' 'Укажите номер шкафа/щита на табличке' $ftText 1 $null)
AppendSqlLine (Emit-Item 9006 19 6 'shuzz_inspect' 'Осмотр ШУЗЗ, ЕЖН' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 9007 19 7 'shuzz_battery_model' 'Модель/тип АКБ ШУЗЗ' $null $ftText 0 $null)
AppendSqlLine (Emit-Item 9008 19 8 'shuzz_battery_state' 'Проверка состояния АКБ ШУЗЗ, ЕЖН' 'Проверить визуально состояние АКБ. Замена не реже 1 раза в 2 года' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 9009 19 9 'extra_shuzz' 'Дополнительные работы, ШУЗЗ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 9010 19 10 'remarks_shuzz' 'Замечания и рекомендации, ШУЗЗ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 9011 19 11 'end_date' 'Дата окончания' $null $ftDate 0 $null)
AppendSqlLine (Emit-Item 9012 19 12 'end_time' 'Время окончания' 'Указанное время и дата будет считаться окончанием работ' $ftTime 0 $null)
Push-Radio2 9006 'Осмотрено' 'Не выполнялось'
Push-Radio4 9008 'Норма' 'Отклонение' 'Замена' 'Не выполнялось'

$dcmIntro = 'Устройства имеют систему самодиагностики и постоянно проверяются в повседневной эксплуатации. Проверка устройств производится в случае сообщений о неисправности. Замена компонентов производится согласно регламенту.'
AppendSqlLine "UPDATE checklist_templates SET intro_modal_text = '$(SqlEscape $dcmIntro)', safety_modal_text = '$(SqlEscape $dcmIntro)', red_button_enabled = 1 WHERE id = 20;"

Unified-Head 20 9101
AppendSqlLine (Emit-Item 9105 20 5 'dcm_model' 'Модель/тип устройства' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 9106 20 6 'dcm_fault_check' 'Проверка устройства по сообщению о неисправности' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 9107 20 7 'extra_dcm' 'Дополнительные работы, ДКМ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 9108 20 8 'remarks_dcm' 'Замечания и рекомендации, ДКМ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 9109 20 9 'end_date' 'Дата окончания' $null $ftDate 0 $null)
AppendSqlLine (Emit-Item 9110 20 10 'end_time' 'Время окончания' 'Указанное время и дата будет считаться окончанием работ' $ftTime 0 $null)
Push-Radio4 9106 'Норма' 'Отклонение' 'Замена' 'Не выполнено'

AppendSqlLine ''
AppendSqlLine '-- Варианты ответов'
foreach ($ol in $optLines) { AppendSqlLine $ol }

[System.IO.File]::WriteAllText($outPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $outPath"
