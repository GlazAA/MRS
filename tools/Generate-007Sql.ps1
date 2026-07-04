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
Р’РќРРњРђРќРР•! РџРµСЂРµРґ РІСЃРµРјРё СЂР°Р±РѕС‚Р°РјРё РїРѕ С‚РµС…РЅРёС‡РµСЃРєРѕРјСѓ РѕР±СЃР»СѓР¶РёРІР°РЅРёСЋ:
1. РћС‚РєР»СЋС‡РёС‚СЊ РєРѕРјРїСЂРµСЃСЃРѕСЂ РїСЂРё РїРѕРјРѕС‰Рё РєРЅРѕРїРєРё Р’Р«РљР›.
2. РџСЂРёРІРµСЃС‚Рё РІ РґРµР№СЃС‚РІРёРµ РїРµСЂРµРєР»СЋС‡Р°С‚РµР»СЊ Р°РІР°СЂРёР№РЅРѕРіРѕ РѕСЃС‚Р°РЅРѕРІР°.
3. Р Р°Р·РѕРјРєРЅСѓС‚СЊ СѓСЃС‚СЂРѕР№СЃС‚РІРѕ РѕС‚РєР»СЋС‡РµРЅРёСЏ РѕС‚ СЃРµС‚Рё Рё РѕР±РµР·РѕРїР°СЃРёС‚СЊ СЃ РїРѕРјРѕС‰СЊСЋ РІРёСЃСЏС‡РµРіРѕ Р·Р°РјРєР° РѕС‚ РЅРµРїСЂРµРґРЅР°РјРµСЂРµРЅРЅРѕРіРѕ РїРѕРІС‚РѕСЂРЅРѕРіРѕ РІРєР»СЋС‡РµРЅРёСЏ.
4. Р Р°Р·РјРµСЃС‚РёС‚СЊ РЅР° СѓСЃС‚СЂРѕР№СЃС‚РІРµ СѓРїСЂР°РІР»РµРЅРёСЏ РїСЂРµРґСѓРїСЂРµР¶РґР°СЋС‰СѓСЋ С‚Р°Р±Р»РёС‡РєСѓ.
5. РџСЂРѕРІРµСЂРёС‚СЊ, РґРµР№СЃС‚РІРёС‚РµР»СЊРЅРѕ Р»Рё РѕР±РµСЃС‚РѕС‡РµРЅС‹ РІСЃРµ РґРµС‚Р°Р»Рё СѓСЃС‚Р°РЅРѕРІРєРё.
6. РџРµСЂРµРґ РЅР°С‡Р°Р»РѕРј СЂР°Р±РѕС‚С‹ РґР°С‚СЊ РІСЃРµРј РіРѕСЂСЏС‡РёРј СЌР»РµРјРµРЅС‚Р°Рј РєРѕРЅСЃС‚СЂСѓРєС†РёРё РєРѕРјРїСЂРµСЃСЃРѕСЂР° РѕСЃС‚С‹С‚СЊ РґРѕ 50В°C.
7. РћС‚СЃРѕРµРґРёРЅРёС‚СЊ РєРѕРјРїСЂРµСЃСЃРѕСЂ РѕС‚ СЃРµС‚Рё СЃР¶Р°С‚РѕРіРѕ РІРѕР·РґСѓС…Р°.
Р”Р»СЏ СЌС‚РѕРіРѕ Р·Р°РєСЂС‹С‚СЊ С€Р°СЂРѕРІРѕР№ РєСЂР°РЅ РЅР° РІС‹С…РѕРґРµ СЃР¶Р°С‚РѕРіРѕ РІРѕР·РґСѓС…Р°.
9. РЈРґР°Р»РёС‚СЊ РІРѕР·РґСѓС… РёР· СЃРёСЃС‚РµРјС‹ РєРѕРјРїСЂРµСЃСЃРѕСЂР°.
'@

$errorsIntro = 'РџСЂРё РЅР°Р»РёС‡РёРё РѕС€РёР±РѕРє Рё РЅРµРёСЃРїСЂР°РІРЅРѕСЃС‚РµР№ СЃР»РµРґСѓРµС‚ СЃРѕР±Р»СЋРґР°С‚СЊ РёРЅСЃС‚СЂСѓРєС†РёРё Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё, СѓРєР°Р·Р°РЅРЅС‹Рµ РІ СЂСѓРєРѕРІРѕРґСЃС‚РІРµ РїРѕ СЌРєСЃРїР»СѓР°С‚Р°С†РёРё.'
$to1500 = 'Р’РЅРёРјР°РЅРёРµ! РћР±СЃР»СѓР¶РёРІР°РЅРёРµ РІС‹РїРѕР»РЅСЏРµС‚СЃСЏ СЃ РўРћ-1500.'
$annualIntro = "$errorsIntro`n$to1500"

function Patch-Template([int]$tid, [string]$topPlate, [string]$safety = $weeklySafety) {
    AppendSqlLine "UPDATE checklist_templates SET intro_modal_text = NULL, top_plate_text = '$(SqlEscape $topPlate)', safety_modal_text = '$(SqlEscape $safety)', red_button_enabled = 1 WHERE id = $tid;"
}

Patch-Template 1 $errorsIntro
foreach ($tid in 3,4,6,7,8) { Patch-Template $tid $annualIntro }
Patch-Template 2 $errorsIntro
Patch-Template 5 $errorsIntro
AppendSqlLine ''

AppendSqlLine "INSERT OR IGNORE INTO users (id, user_role_id, first_name, last_name, login, password_hash, is_active) VALUES"
AppendSqlLine "    (2, 1, 'РџС‘С‚СЂ', 'РџРµС‚СЂРѕРІ', 'petrov', '`$2a`$11`$OfflinePlaceholderHashNotForAuth', 1),"
AppendSqlLine "    (3, 1, 'РЎРµСЂРіРµР№', 'РЎРёРґРѕСЂРѕРІ', 'sidorov', '`$2a`$11`$OfflinePlaceholderHashNotForAuth', 1),"
AppendSqlLine "    (4, 1, 'РђРЅРЅР°', 'РљРѕР·Р»РѕРІР°', 'kozlova', '`$2a`$11`$OfflinePlaceholderHashNotForAuth', 1),"
AppendSqlLine "    (5, 1, 'РРІР°РЅ', 'РРІР°РЅРѕРІ', 'ivanov', '`$2a`$11`$OfflinePlaceholderHashNotForAuth', 1);"
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
    $rows.Add((Emit-Item $n $templateId 1 'start_date' 'Р”Р°С‚Р° РЅР°С‡Р°Р»Р°' 'РЎС‚СЂРѕРіРѕ РґРґ.РјРј.РіРіРіРі (РїРѕР»Рµ РєР°Р»РµРЅРґР°СЂСЏ)' $ftDate 1 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 2 'start_time' 'Р’СЂРµРјСЏ РЅР°С‡Р°Р»Р°' 'РЎС‚СЂРѕРіРѕ С‡С‡:РјРј' $ftTime 1 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 3 'workers' 'Р›РёС†Р°, РїСЂРѕРёР·РІРѕРґРёРІС€РёРµ СЂР°Р±РѕС‚С‹' 'РњРѕР¶РЅРѕ РІС‹Р±СЂР°С‚СЊ РЅРµСЃРєРѕР»СЊРєРѕ' $ftDropMulti 1 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 4 'unit_number' 'РќРѕРјРµСЂ СѓСЃС‚Р°РЅРѕРІРєРё' $null $ftDropdown 1 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 5 'equipment_pick' 'РћР±РѕСЂСѓРґРѕРІР°РЅРёРµ' $null $ftDropdown 1 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 6 'comp_model' 'РњРѕРґРµР»СЊ РєРѕРјРїСЂРµСЃСЃРѕСЂР°' $null $ftDropdown 0 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 7 'comp_type' 'РўРёРї РєРѕРјРїСЂРµСЃСЃРѕСЂР°' $null $ftDropdown 0 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 8 'comp_state' 'РЎРѕСЃС‚РѕСЏРЅРёРµ РєРѕРјРїСЂРµСЃСЃРѕСЂР°' 'Р’С‹Р±РµСЂРёС‚Рµ СЃРѕСЃС‚РѕСЏРЅРёРµ РѕР±РѕСЂСѓРґРѕРІР°РЅРёСЏ РґРѕ РЅР°С‡Р°Р»Р° СЂР°Р±РѕС‚' $ftCheckbox 0 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 9 'operating_hours' 'Р§Р°СЃС‹ СЌРєСЃРїР»СѓР°С‚Р°С†РёРё РєРѕРјРїСЂРµСЃСЃРѕСЂР°' 'РњРѕР¶РЅРѕ СѓРєР°Р·Р°С‚СЊ С‡Р°СЃС‹ Рё РјРёРЅСѓС‚С‹' $ftText 0 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 10 'pressure_network' 'Р”Р°РІР»РµРЅРёРµ РІ СЃРµС‚Рё Pn (bar)' $null $ftText 0 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 11 'pressure_system' 'Р”Р°РІР»РµРЅРёРµ РІ СЃРёСЃС‚РµРјРµ Ps (bar)' $null $ftText 0 $null)); $n++
    $rows.Add((Emit-Item $n $templateId 12 'final_temp' 'РљРѕРЅРµС‡РЅР°СЏ С‚РµРјРїРµСЂР°С‚СѓСЂР° СЃР¶Р°С‚РёСЏ' 'РџСЂРѕРІРµСЂРёС‚СЊ Р·Р°РґР°РЅРЅРѕРµ Р·РЅР°С‡РµРЅРёРµ: 70...100В°C' $ftNumber 0 'integer_range_70_100')); $n++
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
    Push-Opt $workersItemId @('Р”РµРјРѕ РРЅР¶РµРЅРµСЂ', 'РџРµС‚СЂРѕРІ РџС‘С‚СЂ РџРµС‚СЂРѕРІРёС‡', 'РЎРёРґРѕСЂРѕРІ РЎРµСЂРіРµР№ РЎРёРґРѕСЂРѕРІРёС‡', 'РљРѕР·Р»РѕРІР° РђРЅРЅР°', 'РРІР°РЅРѕРІ РРІР°РЅ РРІР°РЅРѕРІРёС‡')
}

function Add-CompressorStdOpts($idStart) {
    Push-WorkersFor ($idStart + 2)
    Push-Opt ($idStart + 3) @('G301', '301', 'G302', '302')
    Push-Opt ($idStart + 4) @('Р’РёРЅС‚РѕРІРѕР№ РєРѕРјРїСЂРµСЃСЃРѕСЂ', 'Р­Р»РµРєС‚СЂРѕРґРІРёРіР°С‚РµР»СЊ РєРѕРјРїСЂРµСЃСЃРѕСЂР°', 'РћСЃСѓС€РёС‚РµР»СЊ С…РѕР»РѕРґРёР»СЊРЅРѕРіРѕ С‚РёРїР°', 'Р¦РёРєР»РѕРЅРЅС‹Р№ СЃРµРїР°СЂР°С‚РѕСЂ', 'Р¤РёР»СЊС‚СЂС‹ РѕС‡РёСЃС‚РєРё', 'РЈРіРѕР»СЊРЅС‹Р№ Р°РґСЃРѕСЂР±РµСЂ', 'РљРѕРЅРґРµРЅСЃР°С‚РѕРѕС‚РІРѕРґС‡РёРєРё', 'Р’РѕРґРѕРјР°СЃР»СЏРЅС‹Р№ СЃРµРїР°СЂР°С‚РѕСЂ', 'Р РµСЃРёРІРµСЂС‹', 'Р“Р°Р·РѕСЂР°Р·РґРµР»РёС‚РµР»СЊРЅС‹Р№ РјРѕРґСѓР»СЊ', 'Р¦РµРЅС‚СЂР°Р»СЊРЅС‹Р№ С€РєР°С„ СѓРїСЂР°РІР»РµРЅРёСЏ', 'РЁРєР°С„ СѓРїСЂР°РІР»РµРЅРёСЏ Р·РѕРЅРѕР№ Р·Р°С‰РёС‚С‹', 'Р”Р°С‚С‡РёРєРё, РєРѕРЅС‚СЂРѕР»Р»РµСЂС‹ Рё РјРѕРґСѓР»Рё')
    Push-Opt ($idStart + 5) @('Atlas Copco GA', 'Ingersoll Rand R', 'Р”СЂСѓРіРѕРµ')
    Push-Opt ($idStart + 6) @('РЎС‚Р°С†РёРѕРЅР°СЂРЅС‹Р№', 'РњРѕР±РёР»СЊРЅС‹Р№', 'Р”СЂСѓРіРѕРµ')
    Push-Opt ($idStart + 7) @('Р Р°Р±РѕС‡РµРµ', 'РџРѕРґ РЅР°РіСЂСѓР·РєРѕР№', 'Р’С‹РєР»СЋС‡РµРЅ', 'РќРµ СЂР°Р±РѕС‡РµРµ')
}

function Push-Radio2($itemId, $a, $b) { Push-Opt $itemId @($a, $b) }
function Push-Radio3($itemId, $a, $b, $c) { Push-Opt $itemId @($a, $b, $c) }
function Push-Radio4($itemId, $a, $b, $c, $d) { Push-Opt $itemId @($a, $b, $c, $d) }

# --- Items ---
$b1 = Compressor-BaseItems 1 5001
foreach ($r in $b1.Rows) { AppendSqlLine $r }
$cur = $b1.NextId
AppendSqlLine (Emit-Item $cur 1 13 'display_eval_weekly' 'РћС†РµРЅРєР° РѕС‚РѕР±СЂР°Р¶Р°РµРјС‹С… РЅР° РґРёСЃРїР»РµРµ РґР°РЅРЅС‹С…, Р•Р–Рќ' 'Р’РІРµРґРёС‚Рµ РєРѕРґ РѕС€РёР±РєРё РёР»Рё РёРЅС‹Рµ СЃРµСЂРІРёСЃРЅС‹Рµ СЃРѕРѕР±С‰РµРЅРёСЏ' $ftText 0 $null); $cur++
AppendSqlLine (Emit-Item $cur 1 14 'leak_check_weekly' 'РџСЂРѕРІРµСЂРєР° РЅР°Р»РёС‡РёСЏ РЅРµРіРµСЂРјРµС‚РёС‡РЅРѕСЃС‚РµР№, Р•Р–Рќ' 'Р’РёР·СѓР°Р»СЊРЅР°СЏ РїСЂРѕРІРµСЂРєР° РЅР° РЅР°Р»РёС‡РёРµ РЅРµРіРµСЂРјРµС‚РёС‡РЅРѕСЃС‚Рё' $ftRadio 0 $null); $leakW = $cur; $cur++
AppendSqlLine (Emit-Item $cur 1 15 'diff_pressure_weekly' 'РўРµРєСѓС‰РёР№ РґРёС„С„РµСЂРµРЅС†РёР°Р» РґР°РІР»РµРЅРёСЏ, Р•Р–Рќ' 'РџСЂРѕРІРµСЂРёС‚СЊ СЂР°Р·РЅРёС†Сѓ РјРµР¶РґСѓ РґР°РІР»РµРЅРёРµРј РІ СЃРµС‚Рё Рё СЃРёСЃС‚РµРјРѕР№, Р·Р°РґР°РЅРЅРѕРµ Р·РЅР°С‡РµРЅРёРµ 0 вЂ“ 1,5 Р±Р°СЂ' $ftRadio 0 $null); $diffW = $cur; $cur++
AppendSqlLine (Emit-Item $cur 1 16 'filter_panel_weekly' 'Р¤РёР»СЊС‚СЂ РїСЂРёС‚РѕС‡РЅРѕРіРѕ РІРѕР·РґСѓС…Р° (РїР°РЅРµР»СЊРЅС‹Р№), Р•Р–Рќ' 'Р’РёР·СѓР°Р»СЊРЅР°СЏ РїСЂРѕРІРµСЂРєР°, РїСЂРё РЅРµРѕР±С…РѕРґРёРјРѕСЃС‚Рё Р·Р°РјРµРЅР°' $ftRadio 0 $null); $filtW = $cur; $cur++
AppendSqlLine (Emit-Item $cur 1 17 'extra_weekly' 'Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ СЂР°Р±РѕС‚С‹, Р•Р–Рќ РўРћ' $null $ftTextArea 0 $null); $cur++
AppendSqlLine (Emit-Item $cur 1 18 'remarks_weekly' 'Р—Р°РјРµС‡Р°РЅРёСЏ Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё, Р•Р–Рќ РўРћ' $null $ftTextArea 0 $null); $cur++
AppendSqlLine (Emit-Item $cur 1 19 'end_date' 'Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ' $null $ftDate 0 $null); $cur++

Add-CompressorStdOpts 5001
Push-Radio2 $leakW 'Р•СЃС‚СЊ' 'РћС‚СЃСѓС‚СЃС‚РІСѓРµС‚'
Push-Radio2 $diffW 'РќРѕСЂРјР°' 'РћС‚РєР»РѕРЅРµРЅРёРµ'
Push-Radio3 $filtW 'РќРѕСЂРјР°' 'Р—Р°РјРµРЅР°' 'Р§РёСЃС‚РєР°'

$b2 = Compressor-BaseItems 2 5101
foreach ($r in $b2.Rows) { AppendSqlLine $r }
$cur2 = $b2.NextId
AppendSqlLine (Emit-Item $cur2 2 13 'emergency_switch_monthly' 'РџРµСЂРµРєР»СЋС‡Р°С‚РµР»СЊ Р°РІР°СЂРёР№РЅРѕРіРѕ РѕСЃС‚Р°РЅРѕРІР°, Р•Р–Рњ' 'РџСЂРѕРІРµСЂРёС‚СЊ С„СѓРЅРєС†РёРѕРЅРёСЂРѕРІР°РЅРёРµ РїРµСЂРµРєР»СЋС‡Р°С‚РµР»СЏ Р°РІР°СЂРёР№РЅРѕРіРѕ РѕСЃС‚Р°РЅРѕРІР°' $ftRadio 0 $null); $esw = $cur2; $cur2++
AppendSqlLine (Emit-Item $cur2 2 14 'work_pressure_monthly' 'РџСЂРѕРІРµСЂРєР° СЂР°Р±РѕС‡РµРіРѕ РґР°РІР»РµРЅРёСЏ, Р•Р–Рњ' 'РџСЂРѕРІРµСЂРёС‚СЊ Рё РїСЂРё РЅРµРѕР±С…РѕРґРёРјРѕСЃС‚Рё РїРѕРґСЂРµРіСѓР»РёСЂРѕРІР°С‚СЊ СЂР°Р±РѕС‡РµРµ РґР°РІР»РµРЅРёРµ' $ftRadio 0 $null); $wp = $cur2; $cur2++
AppendSqlLine (Emit-Item $cur2 2 15 'suction_filter_monthly' 'Р¤РёР»СЊС‚СЂ РІСЃР°СЃС‹РІР°СЋС‰РёР№ (РІРѕР·РґСѓС€РЅС‹Р№), Р•Р–Рњ' 'РћС‡РёСЃС‚РёС‚СЊ РѕС‚ Р·Р°РіСЂСЏР·РЅРµРЅРёР№ Рё РїСЂРё РЅРµРѕР±С…РѕРґРёРјРѕСЃС‚Рё Р·Р°РјРµРЅРёС‚СЊ' $ftRadio 0 $null); $sf = $cur2; $cur2++
AppendSqlLine (Emit-Item $cur2 2 16 'oil_level_monthly' 'РЈСЂРѕРІРµРЅСЊ РјР°СЃР»Р° РІ СЂРµР·РµСЂРІСѓР°СЂРµ, Р•Р–Рњ' 'РџСЂРѕРІРµСЂРёС‚СЊ СѓСЂРѕРІРµРЅСЊ РјР°СЃР»Р° Рё РїСЂРё РЅРµРѕР±С…РѕРґРёРјРѕСЃС‚Рё РґРѕР»РёС‚СЊ' $ftRadio 0 $null); $oil = $cur2; $cur2++
AppendSqlLine (Emit-Item $cur2 2 17 'temp_ped' 'РўРµРјРїРµСЂР°С‚СѓСЂР° РєРѕСЂРїСѓСЃР° РџР­Р”' 'РР·РјРµСЂРёС‚СЊ РїРёСЂРѕРјРµС‚СЂРѕРј С‚РµРјРїРµСЂР°С‚СѓСЂСѓ РєРѕСЂРїСѓСЃР° РїСЂРёРІРѕРґРЅРѕРіРѕ СЌР»РµРєС‚СЂРѕРґРІРёРіР°С‚РµР»СЏ' $ftText 0 $null); $cur2++
AppendSqlLine (Emit-Item $cur2 2 18 'temp_edo' 'РўРµРјРїРµСЂР°С‚СѓСЂР° РєРѕСЂРїСѓСЃР° Р­Р”Рћ' 'РР·РјРµСЂРёС‚СЊ РїРёСЂРѕРјРµС‚СЂРѕРј С‚РµРјРїРµСЂР°С‚СѓСЂСѓ РєРѕСЂРїСѓСЃР° СЌР»РµРєС‚СЂРѕРґРІРёРіР°С‚РµР»СЏ РѕС…Р»Р°РґРёС‚РµР»СЏ' $ftText 0 $null); $cur2++
AppendSqlLine (Emit-Item $cur2 2 19 'extra_monthly' 'Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ СЂР°Р±РѕС‚С‹, Р•Р–Рњ РўРћ' $null $ftTextArea 0 $null); $cur2++
AppendSqlLine (Emit-Item $cur2 2 20 'remarks_monthly' 'Р—Р°РјРµС‡Р°РЅРёСЏ Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё, Р•Р–Рњ РўРћ' $null $ftTextArea 0 $null); $cur2++
AppendSqlLine (Emit-Item $cur2 2 21 'end_date' 'Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ' $null $ftDate 0 $null); $cur2++

Add-CompressorStdOpts 5101
Push-Radio2 $esw 'Р Р°Р±РѕС‡РёР№' 'РќРµ СЂР°Р±РѕС‡РёР№'
Push-Radio2 $wp 'РќРѕСЂРјР°' 'РћС‚РєР»РѕРЅРµРЅРёРµ'
Push-Radio2 $sf 'Р§РёСЃС‚РєР°' 'Р—Р°РјРµРЅР°'
Push-Radio2 $oil 'РќРѕСЂРјР°' 'Р”РѕР»РёС‚Рѕ'

for ($ti = 3; $ti -le 8; $ti++) {
    $start = 5200 + ($ti - 3) * 100 + 1
    $bb = Compressor-BaseItems $ti $start
    foreach ($r in $bb.Rows) { AppendSqlLine $r }
    $cn = $bb.NextId
    AppendSqlLine (Emit-Item $cn $ti 13 'regulation_notes' 'РџСѓРЅРєС‚С‹ РєРѕРЅС‚СЂРѕР»СЊРЅРѕРіРѕ Р»РёСЃС‚Р° РїРѕ РІС‹Р±СЂР°РЅРЅРѕРјСѓ РІРёРґСѓ РўРћ' 'РџРѕР»РЅС‹Р№ РїРµСЂРµС‡РµРЅСЊ РїРѕР»РµР№ РўРћ-3000/6000/9000 Рё С‚.Рґ. Р±СѓРґРµС‚ РґРѕР±Р°РІР»РµРЅ РІ СЃР»РµРґСѓСЋС‰РµР№ РёС‚РµСЂР°С†РёРё Р‘Р”' $ftTextArea 0 $null); $cn++
    AppendSqlLine (Emit-Item $cn $ti 14 'extra_typed' 'Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ СЂР°Р±РѕС‚С‹' $null $ftTextArea 0 $null); $cn++
    AppendSqlLine (Emit-Item $cn $ti 15 'remarks_typed' 'Р—Р°РјРµС‡Р°РЅРёСЏ Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё' $null $ftTextArea 0 $null); $cn++
    AppendSqlLine (Emit-Item $cn $ti 16 'end_date' 'Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ' $null $ftDate 0 $null); $cn++
    Add-CompressorStdOpts $start
}

$motorIntro = 'РўРћ - 1400. РџСЂРё РЅР°Р»РёС‡РёРё РѕС€РёР±РѕРє Рё РЅРµРёСЃРїСЂР°РІРЅРѕСЃС‚РµР№ СЃР»РµРґСѓРµС‚ СЃРѕР±Р»СЋРґР°С‚СЊ РёРЅСЃС‚СЂСѓРєС†РёРё Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё, СѓРєР°Р·Р°РЅРЅС‹Рµ РІ СЂСѓРєРѕРІРѕРґСЃС‚РІРµ РїРѕ СЌРєСЃРїР»СѓР°С‚Р°С†РёРё.'
Patch-Template 9 $motorIntro

AppendSqlLine (Emit-Item 8001 9 1 'start_date' 'Р”Р°С‚Р° РЅР°С‡Р°Р»Р°' $null $ftDate 1 $null)
AppendSqlLine (Emit-Item 8002 9 2 'start_time' 'Р’СЂРµРјСЏ РЅР°С‡Р°Р»Р°' $null $ftTime 1 $null)
AppendSqlLine (Emit-Item 8003 9 3 'workers' 'Р›РёС†Р°, РїСЂРѕРёР·РІРѕРґРёРІС€РёРµ СЂР°Р±РѕС‚С‹' 'РњРѕР¶РЅРѕ РІС‹Р±СЂР°С‚СЊ РЅРµСЃРєРѕР»СЊРєРѕ' $ftDropMulti 1 $null)
AppendSqlLine (Emit-Item 8004 9 4 'unit_number' 'РќРѕРјРµСЂ СѓСЃС‚Р°РЅРѕРІРєРё' $null $ftDropdown 1 $null)
AppendSqlLine (Emit-Item 8005 9 5 'motor_model' 'РњРѕРґРµР»СЊ/С‚РёРї РџР­Р”' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8006 9 6 'motor_hours_note' 'Р§Р°СЃС‹ СЌРєСЃРїР»СѓР°С‚Р°С†РёРё РєРѕРјРїСЂРµСЃСЃРѕСЂР° РїСЂРё РўРћ РџР­Р”' 'РЈРєР°Р·Р°С‚СЊ С‡Р°СЃС‹ СЌРєСЃРїР»СѓР°С‚Р°С†РёРё РєРѕРјРїСЂРµСЃСЃРѕСЂР° РїСЂРё СЃРјР°Р·РєРµ РїРѕРґС€РёРїРЅРёРєРѕРІ РґРІРёРіР°С‚РµР»СЏ' $ftText 0 $null)
AppendSqlLine (Emit-Item 8007 9 7 'bearing_grease' 'РЎРјР°Р·РєР° РїРѕРґС€РёРїРЅРёРєРѕРІ РџР­Р”, РўРћ-1400' 'РЎРјР°Р·Р°С‚СЊ РїРѕРґС€РёРїРЅРёРєРё РїСЂРёРІРѕРґРЅРѕРіРѕ РґРІРёРіР°С‚РµР»СЏ (РІ СЃР»СѓС‡Р°Рµ РґРІРёРіР°С‚РµР»РµР№ Р±РµР· СѓСЃС‚СЂРѕР№СЃС‚РІР° РґРѕРїРѕР»РЅРёС‚РµР»СЊРЅРѕР№ СЃРјР°Р·РєРё). РЎРјР°Р·РєР° РїСЂРѕРёР·РІРѕРґРёС‚СЃСЏ РїСЂРё СЂР°Р±РѕС‚Р°СЋС‰РµРј РґРІРёРіР°С‚РµР»Рµ. РЎРј. РґР°РЅРЅС‹Рµ РЅР° С‚Р°Р±Р»РёС‡РєРµ РїСЂРёРІРѕРґРЅРѕРіРѕ РґРІРёРіР°С‚РµР»СЏ.' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8008 9 8 'service_reset_motor' 'РЎР±СЂРѕСЃ РёРЅС‚РµСЂРІР°Р»Р° СЃРµСЂРІРёСЃРЅРѕРіРѕ РѕР±СЃР»СѓР¶РёРІР°РЅРёСЏ РџР­Р”, РўРћ-1400' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8009 9 9 'extra_motor' 'Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ СЂР°Р±РѕС‚С‹, РўРћ РџР­Р”' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8010 9 10 'remarks_motor' 'Р—Р°РјРµС‡Р°РЅРёСЏ Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё, РўРћ РџР­Р”' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8011 9 11 'end_date' 'Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ' $null $ftDate 0 $null)
Push-WorkersFor 8003
Push-Opt 8004 @('G301', '301', 'G302', '302')
Push-Radio2 8007 'Р’С‹РїРѕР»РЅРµРЅРѕ' 'РќРµ РІС‹РїРѕР»РЅРµРЅРѕ'
Push-Radio2 8008 'Р’С‹РїРѕР»РЅРµРЅРѕ' 'РќРµ РІС‹РїРѕР»РЅРµРЅРѕ'

Patch-Template 10 $errorsIntro

AppendSqlLine (Emit-Item 8101 10 1 'start_date' 'Р”Р°С‚Р° РЅР°С‡Р°Р»Р°' $null $ftDate 1 $null)
AppendSqlLine (Emit-Item 8102 10 2 'start_time' 'Р’СЂРµРјСЏ РЅР°С‡Р°Р»Р°' $null $ftTime 1 $null)
AppendSqlLine (Emit-Item 8103 10 3 'workers' 'Р›РёС†Р°, РїСЂРѕРёР·РІРѕРґРёРІС€РёРµ СЂР°Р±РѕС‚С‹' $null $ftDropMulti 1 $null)
AppendSqlLine (Emit-Item 8104 10 4 'unit_number' 'РќРѕРјРµСЂ СѓСЃС‚Р°РЅРѕРІРєРё' $null $ftDropdown 1 $null)
AppendSqlLine (Emit-Item 8105 10 5 'oht_model' 'РњРѕРґРµР»СЊ/С‚РёРї РћРҐРў' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8106 10 6 'oht_hours' 'РћР±С‰РµРµ С‡РёСЃР»Рѕ СЂР°Р±РѕС‡РёС… С‡Р°СЃРѕРІ РѕСЃСѓС€РёС‚РµР»СЏ' 'РџР°СЂР°РјРµС‚СЂ Рђ2/Рђ3. Р Р°Р±РѕС‡РёРµ С‡Р°СЃС‹ = Рђ3Г—1000+Рђ2' $ftText 0 $null)
AppendSqlLine (Emit-Item 8107 10 7 'fridge_hours' 'РћР±С‰РµРµ С‡РёСЃР»Рѕ СЂР°Р±РѕС‡РёС… С‡Р°СЃРѕРІ С…РѕР»РѕРґРёР»СЊРЅРѕРіРѕ РєРѕРјРїСЂРµСЃСЃРѕСЂР°' 'РџР°СЂР°РјРµС‚СЂ Рђ4/Рђ5. Р Р°Р±РѕС‡РёРµ С‡Р°СЃС‹ = Рђ5Г—1000+Рђ4' $ftText 0 $null)
AppendSqlLine (Emit-Item 8108 10 8 'compressor_out_temp' 'РўРµРјРїРµСЂР°С‚СѓСЂР° РЅР° РІС‹С…РѕРґРµ РєРѕРјРїСЂРµСЃСЃРѕСЂР° РћРҐРў (Р»РёРЅРёСЏ РЅР°РіРЅРµС‚Р°РЅРёСЏ)' 'РџР°СЂР°РјРµС‚СЂ b8' $ftText 0 $null)
AppendSqlLine (Emit-Item 8109 10 9 'led_state_oht' 'РџСЂРѕРІРµСЂРєР° СЃРѕСЃС‚РѕСЏРЅРёСЏ РёРЅРґРёРєР°С†РёРё (СЃРІРµС‚РѕРґРёРѕРґРѕРІ) РћРҐРў, Р•Р–Рќ' 'РџСЂРѕРІРµСЂРєР°: РіРѕСЂРёС‚ РёРЅРґРёРєР°С‚РѕСЂ POWER ON, РёРЅРґРёРєР°С‚РѕСЂРѕРІ РїР°РЅРµР»Рё СѓРїСЂР°РІР»РµРЅРёСЏ' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8110 10 10 'display_oht' 'РћС†РµРЅРєР° РѕС‚РѕР±СЂР°Р¶Р°РµРјС‹С… РЅР° РґРёСЃРїР»РµРµ РґР°РЅРЅС‹С… РћРҐРў, Р•Р–Рќ' 'Р’РІРµРґРёС‚Рµ РєРѕРґ РѕС€РёР±РєРё РёР»Рё РёРЅС‹Рµ СЃРµСЂРІРёСЃРЅС‹Рµ СЃРѕРѕР±С‰РµРЅРёСЏ' $ftText 0 $null)
AppendSqlLine (Emit-Item 8111 10 11 'condensate_device_oht' 'РџСЂРѕРІРµСЂРєР° СѓСЃС‚СЂРѕР№СЃС‚РІР° СЃР»РёРІР° РєРѕРЅРґРµРЅСЃР°С‚Р° РћРҐРў, Р•Р–Рќ' 'Р”Р»СЏ Р°РєС‚РёРІР°С†РёРё РєСЂР°С‚РєРѕ РЅР°Р¶Р°С‚СЊ РєРЅРѕРїРєСѓ РґРёР°РіРЅРѕСЃС‚РёРєРё РєРѕРЅРґРµРЅСЃР°С‚РѕРѕС‚РІРѕРґС‡РёРєР°' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8112 10 12 'fins_clean_oht' 'Р§РёСЃС‚РєР° СЂС‘Р±РµСЂ РєРѕРЅРґРµРЅСЃР°С‚РѕСЂР° РћРҐРў, 4 РјРµСЃ' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8113 10 13 'current_l1_oht' 'РџРѕС‚СЂРµР±Р»СЏРµРјС‹Р№ С‚РѕРє РїРѕРґ РЅР°РіСЂСѓР·РєРѕР№ L1 РћРҐРў, 4 РјРµСЃ' 'РџСЂРѕРІРµСЂРєР° РІРµР»РёС‡РёРЅС‹ РїРѕС‚СЂРµР±Р»СЏРµРјРѕРіРѕ С‚РѕРєР°' $ftText 0 $null)
AppendSqlLine (Emit-Item 8114 10 14 'current_l2_oht' 'РџРѕС‚СЂРµР±Р»СЏРµРјС‹Р№ С‚РѕРє РїРѕРґ РЅР°РіСЂСѓР·РєРѕР№ L2 РћРҐРў, 4 РјРµСЃ' 'РџСЂРѕРІРµСЂРєР° РІРµР»РёС‡РёРЅС‹ РїРѕС‚СЂРµР±Р»СЏРµРјРѕРіРѕ С‚РѕРєР°' $ftText 0 $null)
AppendSqlLine (Emit-Item 8115 10 15 'current_l3_oht' 'РџРѕС‚СЂРµР±Р»СЏРµРјС‹Р№ С‚РѕРє РїРѕРґ РЅР°РіСЂСѓР·РєРѕР№ L3 РћРҐРў, 4 РјРµСЃ' 'РџСЂРѕРІРµСЂРєР° РІРµР»РёС‡РёРЅС‹ РїРѕС‚СЂРµР±Р»СЏРµРјРѕРіРѕ С‚РѕРєР°' $ftText 0 $null)
AppendSqlLine (Emit-Item 8116 10 16 'oht_safety_gate' 'РџРµСЂРµРґ РґР°Р»СЊРЅРµР№С€РёРјРё РїСѓРЅРєС‚Р°РјРё' 'Р’РќРРњРђРќРР•! РџСЂРµР¶РґРµ С‡РµРј РїСЂРёСЃС‚СѓРїРёС‚СЊ Рє РІС‹РїРѕР»РЅРµРЅРёСЋ Р»СЋР±РѕР№ РѕРїРµСЂР°С†РёРё РўРћ РїСЂРѕРІРµСЂСЊС‚Рµ: РѕС‚СЃСѓС‚СЃС‚РІРёРµ РґР°РІР»РµРЅРёСЏ РІ РїРЅРµРІРјР°С‚РёС‡РµСЃРєРѕРј РєРѕРЅС‚СѓСЂРµ; РѕСЃСѓС€РёС‚РµР»СЊ РѕС‚РєР»СЋС‡РµРЅ РѕС‚ СЌР»РµРєС‚СЂРёС‡РµСЃРєРѕР№ СЃРµС‚Рё.' $ftText 0 $null)
AppendSqlLine (Emit-Item 8117 10 17 'leak_refrigerant' 'РџСЂРѕРІРµСЂРєР° СЃРёСЃС‚РµРјС‹ РЅР° СѓС‚РµС‡РєСѓ С…Р»Р°РґР°РіРµРЅС‚Р° РћРҐРў, Р•Р–Р“' 'Р’РёР·СѓР°Р»СЊРЅР°СЏ РїСЂРѕРІРµСЂРєР° СЃРѕРµРґРёРЅРµРЅРёР№ Рё РјР°РіРёСЃС‚СЂР°Р»РµР№' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8118 10 18 'temp_sensor_oht' 'РџСЂРѕРІРµСЂРєР° РґР°С‚С‡РёРєР° С‚РµРјРїРµСЂР°С‚СѓСЂС‹ РћРҐРў, Р•Р–Р“' 'РџСЂРѕРІРµСЂРёС‚СЊ РґР°С‚С‡РёРєРё С‚РµРјРїРµСЂР°С‚СѓСЂС‹. Р—Р°РјРµРЅРёС‚СЊ РёС…, РµСЃР»Рё РЅРµРѕР±С…РѕРґРёРјРѕ' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8119 10 19 'kits_oht' 'РЈСЃС‚Р°РЅРѕРІРєР° РєРѕРјРїР»РµРєС‚РѕРІ РґР»СЏ РўРћ РћРҐРў, 3 РіРѕРґР°' $null $ftCheckbox 0 $null)
AppendSqlLine (Emit-Item 8120 10 20 'extra_oht' 'Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ СЂР°Р±РѕС‚С‹, РўРћ РћРҐРў' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8121 10 21 'remarks_oht' 'Р—Р°РјРµС‡Р°РЅРёСЏ Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё, РўРћ РћРҐРў' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8122 10 22 'end_date' 'Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ' $null $ftDate 0 $null)
Push-WorkersFor 8103
Push-Opt 8104 @('G301', '301', 'G302', '302')
Push-Radio3 8109 'РСЃРїСЂР°РІРЅР°' 'РќРµ СЂР°Р±РѕС‚Р°РµС‚' 'Р’С‹РєР»СЋС‡РµРЅР°'
Push-Radio3 8111 'РСЃРїСЂР°РІРЅРѕ' 'РќРµРёСЃРїСЂР°РІРЅРѕ' 'РќРµ РІС‹РїРѕР»РЅРµРЅРѕ'
Push-Radio2 8112 'Р’С‹РїРѕР»РЅРµРЅРѕ' 'РќРµ РІС‹РїРѕР»РЅРµРЅРѕ'
Push-Radio2 8117 'Р•СЃС‚СЊ' 'РћС‚СЃСѓС‚СЃС‚РІСѓРµС‚'
Push-Radio3 8118 'РљРѕРЅС‚СЂРѕР»СЊ' 'Р—Р°РјРµРЅР°' 'РќРµ РІС‹РїРѕР»РЅСЏР»РѕСЃСЊ'
Push-Opt 8119 @('РєРѕРјРїР»РµРєС‚С‹ РґР»СЏ РєРѕРјРїСЂРµСЃСЃРѕСЂР°', 'РєРѕРјРїР»РµРєС‚С‹ РґР»СЏ РІРµРЅС‚РёР»СЏС‚РѕСЂР°', 'РєРѕРјРїР»РµРєС‚С‹ РґР»СЏ РєР»Р°РїР°РЅР° РіРѕСЂСЏС‡РµРіРѕ РіР°Р·Р°', 'РєРѕРјРїР»РµРєС‚С‹ РґР»СЏ РёСЃРїР°СЂРёС‚РµР»СЏ')

AppendSqlLine (Emit-Item 8201 11 1 'start_date' 'Р”Р°С‚Р° РЅР°С‡Р°Р»Р°' $null $ftDate 1 $null)
AppendSqlLine (Emit-Item 8202 11 2 'start_time' 'Р’СЂРµРјСЏ РЅР°С‡Р°Р»Р°' $null $ftTime 1 $null)
AppendSqlLine (Emit-Item 8203 11 3 'workers' 'Р›РёС†Р°, РїСЂРѕРёР·РІРѕРґРёРІС€РёРµ СЂР°Р±РѕС‚С‹' $null $ftDropMulti 1 $null)
AppendSqlLine (Emit-Item 8204 11 4 'unit_number' 'РќРѕРјРµСЂ СѓСЃС‚Р°РЅРѕРІРєРё' $null $ftDropdown 1 $null)
AppendSqlLine (Emit-Item 8205 11 5 'cyclone_model' 'РњРѕРґРµР»СЊ/С‚РёРї Р¦РЎ' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8206 11 6 'cyclone_state' 'РџСЂРѕРІРµСЂРєР° СЃРѕСЃС‚РѕСЏРЅРёСЏ Р¦РЎ' 'Р’РёР·СѓР°Р»СЊРЅС‹Р№ РєРѕРЅС‚СЂРѕР»СЊ СЃРѕСЃС‚РѕСЏРЅРёСЏ РєРѕРЅРґРµРЅСЃР°С‚РѕРѕС‚РІРѕРґС‡РёРєР°' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8207 11 7 'extra_cyclone' 'Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ СЂР°Р±РѕС‚С‹, Р¦РЎ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8208 11 8 'remarks_cyclone' 'Р—Р°РјРµС‡Р°РЅРёСЏ Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё, Р¦РЎ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8209 11 9 'end_date' 'Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ' $null $ftDate 0 $null)
Patch-Template 11 $errorsIntro
Push-WorkersFor 8203
Push-Opt 8204 @('G301', '301', 'G302', '302')
Push-Radio4 8206 'РСЃРїСЂР°РІРµРЅ' 'РђРІР°СЂРёСЏ' 'Р§РёСЃС‚РєР°' 'РќРµ РІС‹РїРѕР»РЅСЏР»РѕСЃСЊ'

AppendSqlLine (Emit-Item 8301 12 1 'start_date' 'Р”Р°С‚Р° РЅР°С‡Р°Р»Р°' $null $ftDate 1 $null)
AppendSqlLine (Emit-Item 8302 12 2 'start_time' 'Р’СЂРµРјСЏ РЅР°С‡Р°Р»Р°' $null $ftTime 1 $null)
AppendSqlLine (Emit-Item 8303 12 3 'workers' 'Р›РёС†Р°, РїСЂРѕРёР·РІРѕРґРёРІС€РёРµ СЂР°Р±РѕС‚С‹' $null $ftDropMulti 1 $null)
AppendSqlLine (Emit-Item 8304 12 4 'unit_number' 'РќРѕРјРµСЂ СѓСЃС‚Р°РЅРѕРІРєРё' $null $ftDropdown 1 $null)
AppendSqlLine (Emit-Item 8305 12 5 'filter_model' 'РњРѕРґРµР»СЊ/С‚РёРї С„РёР»СЊС‚СЂР°' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8306 12 6 'filter_place' 'РњРµСЃС‚Рѕ СѓСЃС‚Р°РЅРѕРІРєРё С„РёР»СЊС‚СЂР°' 'Р’С‹Р±РµСЂРёС‚Рµ РјРµСЃС‚Рѕ СѓСЃС‚Р°РЅРѕРІРєРё РІ СЃРµС‚Рё' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8307 12 7 'filter_element' 'РџСЂРѕРІРµСЂРєР° С„РёР»СЊС‚СЂСѓСЋС‰РµРіРѕ СЌР»РµРјРµРЅС‚Р°, Р•Р–Рќ' 'РџСЂРѕРІРµСЂСЊС‚Рµ РёРЅРґРёРєР°С†РёСЋ РЅР° РґРёС„С„РµСЂРµРЅС†РёР°Р»СЊРЅРѕРј РјР°РЅРѕРјРµС‚СЂРµ (РѕРїС†РёРѕРЅР°Р»СЊРЅРѕ)' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8308 12 8 'extra_filters' 'Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ СЂР°Р±РѕС‚С‹ РїРѕ С„РёР»СЊС‚СЂР°Рј' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8309 12 9 'remarks_filters' 'Р—Р°РјРµС‡Р°РЅРёСЏ Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё РїРѕ С„РёР»СЊС‚СЂР°Рј' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8310 12 10 'end_date' 'Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ' $null $ftDate 0 $null)
Patch-Template 12 'Р—Р°РјРµРЅР° С„РёР»СЊС‚СЂСѓСЋС‰РёС… СЌР»РµРјРµРЅС‚РѕРІ РЅРµ СЂРµР¶Рµ 1 СЂР°Р·Р° РІ РіРѕРґ.'
Push-WorkersFor 8303
Push-Opt 8304 @('G301', '301', 'G302', '302')
Push-Radio2 8306 'РЎР¶Р°С‚С‹Р№ РІРѕР·РґСѓС…' 'РђР·РѕС‚'
Push-Radio3 8307 'РќРѕСЂРјР°' 'РћС‚РєР»РѕРЅРµРЅРёРµ' 'Р—Р°РјРµРЅР°'

$adsIntro = "РђРґСЃРѕСЂР±РµСЂС‹ РЅР° РѕСЃРЅРѕРІРµ Р°РєС‚РёРІРёСЂРѕРІР°РЅРЅРѕРіРѕ СѓРіР»СЏ. $errorsIntro"
Patch-Template 13 $adsIntro

AppendSqlLine (Emit-Item 8401 13 1 'start_date' 'Р”Р°С‚Р° РЅР°С‡Р°Р»Р°' $null $ftDate 1 $null)
AppendSqlLine (Emit-Item 8402 13 2 'start_time' 'Р’СЂРµРјСЏ РЅР°С‡Р°Р»Р°' $null $ftTime 1 $null)
AppendSqlLine (Emit-Item 8403 13 3 'workers' 'Р›РёС†Р°, РїСЂРѕРёР·РІРѕРґРёРІС€РёРµ СЂР°Р±РѕС‚С‹' $null $ftDropMulti 1 $null)
AppendSqlLine (Emit-Item 8404 13 4 'unit_number' 'РќРѕРјРµСЂ СѓСЃС‚Р°РЅРѕРІРєРё' $null $ftDropdown 1 $null)
AppendSqlLine (Emit-Item 8405 13 5 'ads_model' 'РњРѕРґРµР»СЊ/С‚РёРї Р°РґСЃРѕСЂР±РµСЂР°' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8406 13 6 'ads_damage' 'РџСЂРѕРІРµСЂРєР° РЅР° РЅР°Р»РёС‡РёРµ РїРѕРІСЂРµР¶РґРµРЅРёР№ РђР”РЎ, Р•Р–Рќ' 'Р’РёР·СѓР°Р»СЊРЅС‹Р№ РѕСЃРјРѕС‚СЂ РєРѕСЂРїСѓСЃР° Рё СЃРѕРµРґРёРЅРµРЅРёР№' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8407 13 7 'ads_pressure_gauge' 'РџСЂРѕРІРµСЂРєР° СѓСЂРѕРІРЅСЏ РґР°РІР»РµРЅРёСЏ РЅР° РјР°РЅРѕРјРµС‚СЂРµ РђР”РЎ, Р•Р–Рќ' 'Р’РёР·СѓР°Р»СЊРЅР°СЏ РїСЂРѕРІРµСЂРєР° СѓСЂРѕРІРЅСЏ РґР°РІР»РµРЅРёСЏ РЅР° РјР°РЅРѕРјРµС‚СЂРµ' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8408 13 8 'ads_tube' 'РРЅРґРёРєР°С‚РѕСЂРЅР°СЏ С‚СЂСѓР±РєР° СЃ РјРµС…Р°РЅРёР·РјРѕРј РђР”РЎ, Р•Р–Рќ' 'Р—Р°РјРµРЅР° РёРЅРґРёРєР°С‚РѕСЂРЅРѕР№ С‚СЂСѓР±РєРё РїСЂРѕРёР·РІРѕРґРёС‚СЃСЏ РїРѕСЃР»Рµ РёР·РјРµРЅРµРЅРёСЏ С†РІРµС‚Р°' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8409 13 9 'ads_oil_residual' 'РџСЂРѕРІРµСЂРєР° РѕСЃС‚Р°С‚РѕС‡РЅРѕРіРѕ СЃРѕРґРµСЂР¶Р°РЅРёСЏ РјР°СЃР»Р° РІ РђР”РЎ, Р•Р–Рњ' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8410 13 10 'ads_carbon_year' 'Р—Р°РјРµРЅР° Р°РєС‚РёРІРёСЂРѕРІР°РЅРЅРѕРіРѕ СѓРіР»СЏ РІ РђР”РЎ, Р“РћР”' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8411 13 11 'extra_ads' 'Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ СЂР°Р±РѕС‚С‹, РђР”РЎ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8412 13 12 'remarks_ads' 'Р—Р°РјРµС‡Р°РЅРёСЏ Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё, РђР”РЎ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8413 13 13 'end_date' 'Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ' $null $ftDate 0 $null)
Push-WorkersFor 8403
Push-Opt 8404 @('G301', '301', 'G302', '302')
Push-Radio3 8406 'РќРѕСЂРјР°' 'РћС‚РєР»РѕРЅРµРЅРёРµ' 'РќРµ РІС‹РїРѕР»РЅСЏР»РѕСЃСЊ'
Push-Radio2 8407 'РќРѕСЂРјР°' 'РћС‚РєР»РѕРЅРµРЅРёРµ'
Push-Radio2 8408 'РљРѕРЅС‚СЂРѕР»СЊ' 'Р—Р°РјРµРЅР°'
Push-Radio3 8409 'РќРѕСЂРјР°' 'РћС‚РєР»РѕРЅРµРЅРёРµ' 'РќРµ РІС‹РїРѕР»РЅСЏР»РѕСЃСЊ'
Push-Radio2 8410 'Р’С‹РїРѕР»РЅРµРЅРѕ' 'РќРµ РІС‹РїРѕР»РЅРµРЅРѕ'

# --- Cond 14, WMS 15, Receiver 16, GRM 17, CAB 18-19, DKM 20: compact ---
function Unified-Head($tid, $id0) {
    AppendSqlLine (Emit-Item ($id0+0) $tid 1 'start_date' 'Р”Р°С‚Р° РЅР°С‡Р°Р»Р°' $null $ftDate 1 $null)
    AppendSqlLine (Emit-Item ($id0+1) $tid 2 'start_time' 'Р’СЂРµРјСЏ РЅР°С‡Р°Р»Р°' $null $ftTime 1 $null)
    AppendSqlLine (Emit-Item ($id0+2) $tid 3 'workers' 'Р›РёС†Р°, РїСЂРѕРёР·РІРѕРґРёРІС€РёРµ СЂР°Р±РѕС‚С‹' $null $ftDropMulti 1 $null)
    AppendSqlLine (Emit-Item ($id0+3) $tid 4 'unit_number' 'РќРѕРјРµСЂ СѓСЃС‚Р°РЅРѕРІРєРё' $null $ftDropdown 1 $null)
    Push-WorkersFor ($id0+2)
    Push-Opt ($id0+3) @('G301', '301', 'G302', '302')
}

foreach ($tid in 14,15,17,18,19) { Patch-Template $tid $errorsIntro }
Patch-Template 16 'Р•Р¶РµРЅРµРґРµР»СЊРЅРѕРµ С‚РµС…РЅРёС‡РµСЃРєРѕРµ РѕР±СЃР»СѓР¶РёРІР°РЅРёРµ.'

Unified-Head 14 8501
AppendSqlLine (Emit-Item 8505 14 5 'cond_model' 'РњРѕРґРµР»СЊ/С‚РёРї РљРћ' 'Р•СЃР»Рё РІ СЃРµС‚Рё РѕРґРёРЅР°РєРѕРІС‹Рµ РјРѕРґРµР»Рё, СѓРєР°Р¶РёС‚Рµ СЃРµСЂРёР№РЅС‹Р№ РЅРѕРјРµСЂ' $ftText 1 $null)
AppendSqlLine (Emit-Item 8506 14 6 'cond_led' 'РџСЂРѕРІРµСЂРєР° СЃРѕСЃС‚РѕСЏРЅРёСЏ РёРЅРґРёРєР°С†РёРё (СЃРІРµС‚РѕРґРёРѕРґРѕРІ) РљРћ, Р•Р–Рќ' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8507 14 7 'cond_drain' 'РџСЂРѕРІРµСЂРєР° СЂР°Р±РѕС‚С‹ Рё РѕС‚РІРѕРґР° РєРѕРЅРґРµРЅСЃР°С‚Р° РљРћ, Р•Р–Рќ' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8508 14 8 'cond_clean' 'Р§РёСЃС‚РєР° РєРѕСЂРїСѓСЃР° Рё РєР»Р°РїР°РЅР° РљРћ, Р•Р–Р“' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8509 14 9 'cond_wear' 'Р—Р°РјРµРЅР° РёР·РЅР°С€РёРІР°СЋС‰РёС…СЃСЏ РґРµС‚Р°Р»РµР№ РљРћ, Р•Р–Р“' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8510 14 10 'extra_cond' 'Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ СЂР°Р±РѕС‚С‹, РљРћ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8511 14 11 'remarks_cond' 'Р—Р°РјРµС‡Р°РЅРёСЏ Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё, РљРћ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8512 14 12 'end_date' 'Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ' $null $ftDate 0 $null)
Push-Radio3 8506 'РСЃРїСЂР°РІРЅР°' 'РќРµРёСЃРїСЂР°РІРЅР°' 'РќРµ РІС‹РїРѕР»РЅРµРЅРѕ'
Push-Radio3 8507 'РСЃРїСЂР°РІРЅР°' 'РќРµРёСЃРїСЂР°РІРЅР°' 'РќРµ РІС‹РїРѕР»РЅРµРЅРѕ'
Push-Radio2 8508 'Р’С‹РїРѕР»РЅРµРЅРѕ' 'РќРµ РІС‹РїРѕР»РЅРµРЅРѕ'
Push-Radio2 8509 'Р’С‹РїРѕР»РЅРµРЅРѕ' 'РќРµ РІС‹РїРѕР»РЅРµРЅРѕ'

Unified-Head 15 8601
AppendSqlLine (Emit-Item 8605 15 5 'wms_model' 'РњРѕРґРµР»СЊ/С‚РёРї Р’РњРЎ' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8606 15 6 'wms_indicators' 'РџСЂРѕРІРµСЂРєР° СЃРѕСЃС‚РѕСЏРЅРёСЏ РёРЅРґРёРєР°С‚РѕСЂРѕРІ Р’РњРЎ, Р•Р–Рњ' 'Р’РёР·СѓР°Р»СЊРЅС‹Р№ РєРѕРЅС‚СЂРѕР»СЊ Р·Р° РёРЅРґРёРєР°С‚РѕСЂР°РјРё РІРѕРґРѕРјР°СЃР»СЏРЅРѕРіРѕ СЃРµРїР°СЂР°С‚РѕСЂР°' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8607 15 7 'wms_filters' 'Р—Р°РјРµРЅР° С„РёР»СЊС‚СЂРѕРІ РїРѕ РёРЅРґРёРєР°С‚РѕСЂР°Рј, Р’РњРЎ' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8608 15 8 'extra_wms' 'Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ СЂР°Р±РѕС‚С‹, Р’РњРЎ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8609 15 9 'remarks_wms' 'Р—Р°РјРµС‡Р°РЅРёСЏ Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё, Р’РњРЎ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8610 15 10 'end_date' 'Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ' $null $ftDate 0 $null)
Push-Radio2 8606 'РќРѕСЂРјР°' 'РћС‚РєР»РѕРЅРµРЅРёРµ'
Push-Radio2 8607 'Р’С‹РїРѕР»РЅРµРЅРѕ' 'РќРµ РІС‹РїРѕР»РЅРµРЅРѕ'

Unified-Head 16 8701
AppendSqlLine (Emit-Item 8705 16 5 'recv_model' 'РњРѕРґРµР»СЊ/С‚РёРї СЂРµСЃРёРІРµСЂР°' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8706 16 6 'recv_place' 'РњРµСЃС‚Рѕ СѓСЃС‚Р°РЅРѕРІРєРё СЂРµСЃРёРІРµСЂР°' 'Р’С‹Р±РµСЂРёС‚Рµ РјРµСЃС‚Рѕ СѓСЃС‚Р°РЅРѕРІРєРё РІ СЃРµС‚Рё' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8707 16 7 'recv_drain' 'РџСЂРѕРІРµСЂРєР° РЅР° СЃРѕРґРµСЂР¶Р°РЅРёРµ С‡Р°СЃС‚РёС† РёР»Рё Р¶РёРґРєРѕСЃС‚Рё (РІРѕРґР°/РјР°СЃР»Рѕ)' 'РќРµРЅР°РґРѕР»РіРѕ РїСЂРёРѕС‚РєСЂРѕР№С‚Рµ РЅР° РґРЅРµ СЂРµСЃРёРІРµСЂР° СЂСѓС‡РЅРѕР№ РєР»Р°РїР°РЅ СЃР±СЂРѕСЃР° РєРѕРЅРґРµРЅСЃР°С‚Р°' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8708 16 8 'recv_leak' 'РџСЂРѕРІРµСЂРєР° РЅР°Р»РёС‡РёСЏ РЅРµРіРµСЂРјРµС‚РёС‡РЅРѕСЃС‚РµР№' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8709 16 9 'recv_pressure' 'РџСЂРѕРІРµСЂРєР° СѓСЂРѕРІРЅСЏ РґР°РІР»РµРЅРёСЏ РЅР° РјР°РЅРѕРјРµС‚СЂРµ' 'Р’РёР·СѓР°Р»СЊРЅР°СЏ РїСЂРѕРІРµСЂРєР° СѓСЂРѕРІРЅСЏ РґР°РІР»РµРЅРёСЏ РЅР° РјР°РЅРѕРјРµС‚СЂРµ' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8710 16 10 'extra_recv' 'Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ СЂР°Р±РѕС‚С‹ РїРѕ СЂРµСЃРёРІРµСЂСѓ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8711 16 11 'remarks_recv' 'Р—Р°РјРµС‡Р°РЅРёСЏ Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё РїРѕ СЂРµСЃРёРІРµСЂСѓ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8712 16 12 'end_date' 'Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ' $null $ftDate 0 $null)
Push-Radio2 8706 'РЎР¶Р°С‚С‹Р№ РІРѕР·РґСѓС…' 'РђР·РѕС‚'
Push-Radio2 8707 'Р’С‹РїРѕР»РЅРµРЅРѕ' 'РќРµ РІС‹РїРѕР»РЅСЏР»РѕСЃСЊ'
Push-Radio3 8708 'Р•СЃС‚СЊ' 'РћС‚СЃСѓС‚СЃС‚РІСѓСЋС‚' 'РќРµ РІС‹РїРѕР»РЅСЏР»РѕСЃСЊ'
Push-Radio2 8709 'РќРѕСЂРјР°' 'РћС‚РєР»РѕРЅРµРЅРёРµ'

Unified-Head 17 8801
AppendSqlLine (Emit-Item 8805 17 5 'grm_model' 'РњРѕРґРµР»СЊ/С‚РёРї Р“Р Рњ' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 8806 17 6 'grm_main_switch' 'РџСЂРѕРІРµСЂСЊС‚Рµ РїРѕР»РѕР¶РµРЅРёРµ РіР»Р°РІРЅРѕРіРѕ РІС‹РєР»СЋС‡Р°С‚РµР»СЏ РЅР° РјРѕРґСѓР»Рµ, Р•Р–Рќ' 'Р’С‹РєР»СЋС‡Р°С‚РµР»СЊ РґРѕР»Р¶РµРЅ РЅР°С…РѕРґРёС‚СЊСЃСЏ РІ РїРѕР»РѕР¶РµРЅРёРё ON' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8807 17 7 'grm_states' 'РџСЂРѕРІРµСЂРєР° Р·Р°РґР°РЅРЅС‹С… СЃРѕСЃС‚РѕСЏРЅРёР№ Рё РїСЂРµРґРµР»СЊРЅС‹С… Р·РЅР°С‡РµРЅРёР№ Р“Р Рњ, Р•Р–Рќ' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8808 17 8 'grm_pressure' 'РџСЂРѕРІРµСЂРєР° СѓСЂРѕРІРЅСЏ РґР°РІР»РµРЅРёСЏ РЅР° РјР°РЅРѕРјРµС‚СЂРµ Р“Р Рњ, Р•Р–Рќ' 'Р’РёР·СѓР°Р»СЊРЅР°СЏ РїСЂРѕРІРµСЂРєР° СѓСЂРѕРІРЅСЏ РґР°РІР»РµРЅРёСЏ РЅР° РјР°РЅРѕРјРµС‚СЂРµ' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8809 17 9 'grm_replace_3y' 'Р—Р°РјРµРЅР° РѕР±РѕСЂСѓРґРѕРІР°РЅРёСЏ Р“Р Рњ, РєР°Р¶РґС‹Рµ 3 РіРѕРґР°' 'РћС‚РјРµС‚СЊС‚Рµ РєР°РєРѕРµ РѕР±РѕСЂСѓРґРѕРІР°РЅРёРµ Р±С‹Р»Рѕ Р·Р°РјРµРЅРµРЅРѕ' $ftCheckbox 0 $null)
AppendSqlLine (Emit-Item 8810 17 10 'grm_replace_5y' 'Р—Р°РјРµРЅР° РѕР±РѕСЂСѓРґРѕРІР°РЅРёСЏ Р“Р Рњ, РєР°Р¶РґС‹Рµ 5 Р»РµС‚' 'РћС‚РјРµС‚СЊС‚Рµ РєР°РєРѕРµ РѕР±РѕСЂСѓРґРѕРІР°РЅРёРµ Р±С‹Р»Рѕ Р·Р°РјРµРЅРµРЅРѕ' $ftCheckbox 0 $null)
AppendSqlLine (Emit-Item 8811 17 11 'extra_grm' 'Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ СЂР°Р±РѕС‚С‹, Р“Р Рњ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8812 17 12 'remarks_grm' 'Р—Р°РјРµС‡Р°РЅРёСЏ Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё, Р“Р Рњ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8813 17 13 'end_date' 'Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ' $null $ftDate 0 $null)
Push-Radio2 8806 'Р’РєР»СЋС‡РµРЅ' 'РћС‚РєР»СЋС‡РµРЅ'
Push-Radio2 8807 'РќРѕСЂРјР°' 'РћС‚РєР»РѕРЅРµРЅРёРµ'
Push-Radio2 8808 'РќРѕСЂРјР°' 'РћС‚РєР»РѕРЅРµРЅРёРµ'
Push-Opt 8809 @('РњР°РЅРѕРјРµС‚СЂ В«Р’С…РѕРґ СЃР¶Р°С‚РѕРіРѕ РІРѕР·РґСѓС…Р°В»', 'РњР°РЅРѕРјРµС‚СЂ В«РќР°РєРѕРїРёС‚РµР»СЊРЅР°СЏ РµРјРєРѕСЃС‚СЊ CMS1В»', 'РњР°РЅРѕРјРµС‚СЂ В«РќР°РєРѕРїРёС‚РµР»СЊРЅР°СЏ РµРјРєРѕСЃС‚СЊ CMS2В»', 'РРЅС‚РµСЂС„РµР№СЃ СѓРїСЂР°РІР»РµРЅРёСЏ СЂР°СЃРїСЂРµРґРµР»РµРЅРёРµРј СЃР¶Р°С‚РѕРіРѕ РІРѕР·РґСѓС…Р°', 'Р”Р°С‚С‡РёРє РєРёСЃР»РѕСЂРѕРґР°')
Push-Opt 8810 @('РЁР»Р°РЅРіРё СЃР¶Р°С‚РѕРіРѕ РІРѕР·РґСѓС…Р° Рё СѓРїСЂР°РІР»СЏСЋС‰РµРіРѕ РІРѕР·РґСѓС…Р° (РєРѕРјРїР»РµРєС‚)', 'РљР»Р°РїР°РЅ СѓРїСЂР°РІР»РµРЅРёСЏ 1', 'РљР»Р°РїР°РЅ СѓРїСЂР°РІР»РµРЅРёСЏ 2', 'Р РµР»Рµ РґР°РІР»РµРЅРёСЏ В«Р’С…РѕРґ СЃР¶Р°С‚РѕРіРѕ РІРѕР·РґСѓС…Р°В»', 'Р РµРґСѓРєС‚РѕСЂ РґР°РІР»РµРЅРёСЏ В«РЈРїСЂР°РІР»СЏСЋС‰РёР№ РІРѕР·РґСѓС…В»', 'Р РµРґСѓРєС‚РѕСЂ РґР°РІР»РµРЅРёСЏ В«Р”Р°С‚С‡РёРє РєРёСЃР»РѕСЂРѕРґР°В»')

Unified-Head 18 8901
AppendSqlLine (Emit-Item 8905 18 5 'cshu_number' 'РќРѕРјРµСЂ Р¦РЁРЈ' 'РЈРєР°Р¶РёС‚Рµ РЅРѕРјРµСЂ С€РєР°С„Р°/С‰РёС‚Р° РЅР° С‚Р°Р±Р»РёС‡РєРµ' $ftText 1 $null)
AppendSqlLine (Emit-Item 8906 18 6 'cshu_inspect' 'РћСЃРјРѕС‚СЂ Р¦РЁРЈ, Р•Р–Рќ' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8907 18 7 'cshu_battery_model' 'РњРѕРґРµР»СЊ/С‚РёРї РђРљР‘ Р¦РЁРЈ' $null $ftText 0 $null)
AppendSqlLine (Emit-Item 8908 18 8 'cshu_battery_state' 'РџСЂРѕРІРµСЂРєР° СЃРѕСЃС‚РѕСЏРЅРёСЏ РђРљР‘ Р¦РЁРЈ, Р•Р–Рќ' 'РџСЂРѕРІРµСЂРёС‚СЊ РІРёР·СѓР°Р»СЊРЅРѕ СЃРѕСЃС‚РѕСЏРЅРёРµ РђРљР‘. Р—Р°РјРµРЅР° РЅРµ СЂРµР¶Рµ 1 СЂР°Р·Р° РІ 2 РіРѕРґР°' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 8909 18 9 'extra_cshu' 'Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ СЂР°Р±РѕС‚С‹, Р¦РЁРЈ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8910 18 10 'remarks_cshu' 'Р—Р°РјРµС‡Р°РЅРёСЏ Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё, Р¦РЁРЈ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 8911 18 11 'end_date' 'Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ' $null $ftDate 0 $null)
Push-Radio2 8906 'РћСЃРјРѕС‚СЂРµРЅРѕ' 'РќРµ РІС‹РїРѕР»РЅСЏР»РѕСЃСЊ'
Push-Radio4 8908 'РќРѕСЂРјР°' 'РћС‚РєР»РѕРЅРµРЅРёРµ' 'Р—Р°РјРµРЅР°' 'РќРµ РІС‹РїРѕР»РЅСЏР»РѕСЃСЊ'

Unified-Head 19 9001
AppendSqlLine (Emit-Item 9005 19 5 'shuzz_number' 'РќРѕРјРµСЂ РЁРЈР—Р—' 'РЈРєР°Р¶РёС‚Рµ РЅРѕРјРµСЂ С€РєР°С„Р°/С‰РёС‚Р° РЅР° С‚Р°Р±Р»РёС‡РєРµ' $ftText 1 $null)
AppendSqlLine (Emit-Item 9006 19 6 'shuzz_inspect' 'РћСЃРјРѕС‚СЂ РЁРЈР—Р—, Р•Р–Рќ' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 9007 19 7 'shuzz_battery_model' 'РњРѕРґРµР»СЊ/С‚РёРї РђРљР‘ РЁРЈР—Р—' $null $ftText 0 $null)
AppendSqlLine (Emit-Item 9008 19 8 'shuzz_battery_state' 'РџСЂРѕРІРµСЂРєР° СЃРѕСЃС‚РѕСЏРЅРёСЏ РђРљР‘ РЁРЈР—Р—, Р•Р–Рќ' 'РџСЂРѕРІРµСЂРёС‚СЊ РІРёР·СѓР°Р»СЊРЅРѕ СЃРѕСЃС‚РѕСЏРЅРёРµ РђРљР‘. Р—Р°РјРµРЅР° РЅРµ СЂРµР¶Рµ 1 СЂР°Р·Р° РІ 2 РіРѕРґР°' $ftRadio 0 $null)
AppendSqlLine (Emit-Item 9009 19 9 'extra_shuzz' 'Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ СЂР°Р±РѕС‚С‹, РЁРЈР—Р—' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 9010 19 10 'remarks_shuzz' 'Р—Р°РјРµС‡Р°РЅРёСЏ Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё, РЁРЈР—Р—' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 9011 19 11 'end_date' 'Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ' $null $ftDate 0 $null)
Push-Radio2 9006 'РћСЃРјРѕС‚СЂРµРЅРѕ' 'РќРµ РІС‹РїРѕР»РЅСЏР»РѕСЃСЊ'
Push-Radio4 9008 'РќРѕСЂРјР°' 'РћС‚РєР»РѕРЅРµРЅРёРµ' 'Р—Р°РјРµРЅР°' 'РќРµ РІС‹РїРѕР»РЅСЏР»РѕСЃСЊ'

$dcmIntro = 'РЈСЃС‚СЂРѕР№СЃС‚РІР° РёРјРµСЋС‚ СЃРёСЃС‚РµРјСѓ СЃР°РјРѕРґРёР°РіРЅРѕСЃС‚РёРєРё Рё РїРѕСЃС‚РѕСЏРЅРЅРѕ РїСЂРѕРІРµСЂСЏСЋС‚СЃСЏ РІ РїРѕРІСЃРµРґРЅРµРІРЅРѕР№ СЌРєСЃРїР»СѓР°С‚Р°С†РёРё. РџСЂРѕРІРµСЂРєР° СѓСЃС‚СЂРѕР№СЃС‚РІ РїСЂРѕРёР·РІРѕРґРёС‚СЃСЏ РІ СЃР»СѓС‡Р°Рµ СЃРѕРѕР±С‰РµРЅРёР№ Рѕ РЅРµРёСЃРїСЂР°РІРЅРѕСЃС‚Рё. Р—Р°РјРµРЅР° РєРѕРјРїРѕРЅРµРЅС‚РѕРІ РїСЂРѕРёР·РІРѕРґРёС‚СЃСЏ СЃРѕРіР»Р°СЃРЅРѕ СЂРµРіР»Р°РјРµРЅС‚Сѓ.'
Patch-Template 20 $dcmIntro

Unified-Head 20 9101
AppendSqlLine (Emit-Item 9105 20 5 'dcm_model' 'РњРѕРґРµР»СЊ/С‚РёРї СѓСЃС‚СЂРѕР№СЃС‚РІР°' $null $ftText 1 $null)
AppendSqlLine (Emit-Item 9106 20 6 'dcm_fault_check' 'РџСЂРѕРІРµСЂРєР° СѓСЃС‚СЂРѕР№СЃС‚РІР° РїРѕ СЃРѕРѕР±С‰РµРЅРёСЋ Рѕ РЅРµРёСЃРїСЂР°РІРЅРѕСЃС‚Рё' $null $ftRadio 0 $null)
AppendSqlLine (Emit-Item 9107 20 7 'extra_dcm' 'Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ СЂР°Р±РѕС‚С‹, Р”РљРњ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 9108 20 8 'remarks_dcm' 'Р—Р°РјРµС‡Р°РЅРёСЏ Рё СЂРµРєРѕРјРµРЅРґР°С†РёРё, Р”РљРњ' $null $ftTextArea 0 $null)
AppendSqlLine (Emit-Item 9109 20 9 'end_date' 'Р”Р°С‚Р° РѕРєРѕРЅС‡Р°РЅРёСЏ' $null $ftDate 0 $null)
Push-Radio4 9106 'РќРѕСЂРјР°' 'РћС‚РєР»РѕРЅРµРЅРёРµ' 'Р—Р°РјРµРЅР°' 'РќРµ РІС‹РїРѕР»РЅРµРЅРѕ'

AppendSqlLine ''
AppendSqlLine '-- Р’Р°СЂРёР°РЅС‚С‹ РѕС‚РІРµС‚РѕРІ'
foreach ($ol in $optLines) { AppendSqlLine $ol }

[System.IO.File]::WriteAllText($outPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $outPath"
