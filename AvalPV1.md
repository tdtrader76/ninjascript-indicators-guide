//@version=6
indicator("AvalPV1", shorttitle="APV1", overlay=true)

// ══════════════════════════════════════════════════════════════════════
// 📋 CONFIGURACIÓN Y PARÁMETROS
// ══════════════════════════════════════════════════════════════════════

// --- Parámetros de Cálculo ---
use_automatic_date = input.bool(true, "Use Automatic Date", group="Parameters", tooltip="If true, calculates range from the prior day automatically. If false, uses the 'Selected Date' below.")
nr2_level_type = input.string("Previous Day Close", "NR2 Level Type", options=["Previous Day Close", "Current Day Open"], group="Parameters", tooltip="Select whether NR2 should use the previous day's close or current day's open.")
gap_mode = input.string("Automatic", "Gap Calculation Mode", options=["Automatic", "Manual"], group="Parameters", tooltip="Select whether the gap is calculated automatically or manually.")
manual_gap = input.float(0.0, "Manual Gap", group="Parameters", tooltip="The manual gap value. Only used if Gap Calculation Mode is 'Manual'.")
selected_date = input.time(timestamp("2023-01-01"), "Selected Date", group="Parameters", tooltip="Date for which levels will be drawn, based on the prior day's data.")
manual_price = input.float(0.0, "Manual Price", group="Parameters", step=0.25, tooltip="Base price for levels. If 0, uses prior day's close or current day's open based on NR2 Level Type.")

// --- Configuración Visual ---
line_width = input.int(1, "Line Width", minval=1, group="Visuals")
show_labels = input.bool(true, "Show Labels", group="Visuals")
label_pos = input.string("Above", "Label Position", options=["Above", "Below"], group="Visuals", tooltip="Positions the labels above or below the level lines.")
show_table = input.bool(true, "Show Info Table", group="Visuals")
table_pos_str = input.string("Top Right", "Table Position", options=["Top Right", "Top Left", "Bottom Right", "Bottom Left"], group="Visuals")

// ══════════════════════════════════════════════════════════════════════
// 🧮 FUNCIONES AUXILIARES
// ══════════════════════════════════════════════════════════════════════

roundToQuarter(value) =>
    math.round(value * 4) / 4

drawLevel(price, day_range, line_color, p_text, text_color, line_array, label_array, start_bar_index) =>
    line_id = line.new(start_bar_index, price, start_bar_index + 1, price, xloc.bar_index, extend.right, line_color, width=line_width)
    array.push(line_array, line_id)
    if show_labels
        float offset = day_range * 0.015 * (label_pos == "Above" ? 1 : -1)
        string label_text = p_text + ": " + str.tostring(price, format.mintick)
        label_id = label.new(start_bar_index, price + offset, label_text, xloc.bar_index, yloc.price, color(na), label.style_label_left, text_color, size=size.normal)
        array.push(label_array, label_id)

clearDrawings(line_array, label_array) =>
    for l in line_array
        line.delete(l)
    for l in label_array
        label.delete(l)
    array.clear(line_array)
    array.clear(label_array)

getTablePosition(pos_string) =>
    switch pos_string
        "Top Right" => position.top_right
        "Top Left" => position.top_left
        "Bottom Right" => position.bottom_right
        "Bottom Left" => position.bottom_left
        => position.bottom_right

fillCell(tbl, col, row, txt, bgcolor) =>
    table.cell(tbl, col, row, txt, text_halign=text.align_center, bgcolor=bgcolor, text_color=color.black)



calculateAndDrawLevels(day_range, base_price, line_array, label_array, start_bar_index, info_table, p_date, p_high, p_low, p_initial_range, p_gap, p_corrected_range) =>
    float half_range = day_range / 2.0
    float q1_level = roundToQuarter(base_price + half_range)
    float q4_level = roundToQuarter(base_price - half_range)
    float d1_plus = roundToQuarter(q1_level + day_range * 0.50)
    float d1_minus = roundToQuarter(q4_level - day_range * 0.50)
    drawLevel(q1_level, day_range, color.yellow, "Q1", color.white, line_array, label_array, start_bar_index)
    drawLevel(q4_level, day_range, color.yellow, "Q4", color.white, line_array, label_array, start_bar_index)
    drawLevel(roundToQuarter(q1_level - day_range * 0.25), day_range, color.new(#ebc6f1, 0), "Q2", color.white, line_array, label_array, start_bar_index)
    drawLevel(roundToQuarter(q4_level + day_range * 0.25), day_range, color.new(#ebc6f1, 0), "Q3", color.white, line_array, label_array, start_bar_index)
    drawLevel(roundToQuarter(q1_level - day_range * 0.375), day_range, color.green, "Q2/3", color.white, line_array, label_array, start_bar_index)
    drawLevel(roundToQuarter(q4_level + day_range * 0.375), day_range, color.red, "Q3/4", color.white, line_array, label_array, start_bar_index)
    drawLevel(roundToQuarter(base_price), day_range, color.new(color.orange, 0), "NR2", color.white, line_array, label_array, start_bar_index)
    drawLevel(roundToQuarter(q1_level - day_range * 0.125), day_range, color.green, "TC", color.white, line_array, label_array, start_bar_index)
    drawLevel(roundToQuarter(q1_level - day_range * 0.159), day_range, color.new(color.purple, 50), "NR1", color.white, line_array, label_array, start_bar_index)
    drawLevel(roundToQuarter(q4_level + day_range * 0.159), day_range, color.new(color.purple, 50), "NR3", color.white, line_array, label_array, start_bar_index)
    drawLevel(roundToQuarter(q4_level + day_range * 0.125), day_range, color.red, "TV", color.white, line_array, label_array, start_bar_index)
    drawLevel(roundToQuarter(q1_level + day_range * 0.125), day_range, color.green, "Std1+", color.white, line_array, label_array, start_bar_index)
    drawLevel(roundToQuarter(q1_level + day_range * 0.25), day_range, color.green, "Std2+", color.white, line_array, label_array, start_bar_index)
    drawLevel(roundToQuarter(q1_level + day_range * 0.375), day_range, color.green, "Std3+", color.white, line_array, label_array, start_bar_index)
    drawLevel(d1_plus, day_range, color.new(color.orange, 0), "1D+", color.white, line_array, label_array, start_bar_index)
    drawLevel(roundToQuarter(q4_level - day_range * 0.125), day_range, color.red, "Std1-", color.white, line_array, label_array, start_bar_index)
    drawLevel(roundToQuarter(q4_level - day_range * 0.25), day_range, color.red, "Std2-", color.white, line_array, label_array, start_bar_index)
    drawLevel(roundToQuarter(q4_level - day_range * 0.375), day_range, color.red, "Std3-", color.white, line_array, label_array, start_bar_index)
    drawLevel(d1_minus, day_range, color.new(color.orange, 0), "1D-", color.white, line_array, label_array, start_bar_index)
    
    // Update the info table with Q1 and Q4 levels
    if show_table and not na(info_table)
        table.clear(info_table, 0, 0, 1, 8)
        fillCell(info_table, 0, 0, "Concept", color.gray)
        fillCell(info_table, 1, 0, "Value", color.gray)
        fillCell(info_table, 0, 1, "Date (for Range)", color.silver)
        fillCell(info_table, 1, 1, str.format("{0,date,yyyy.MM.dd}", p_date), color.silver)
        fillCell(info_table, 0, 2, "High", color.silver)
        fillCell(info_table, 1, 2, str.tostring(p_high, format.mintick), color.silver)
        fillCell(info_table, 0, 3, "Low", color.silver)
        fillCell(info_table, 1, 3, str.tostring(p_low, format.mintick), color.silver)
        fillCell(info_table, 0, 4, "Range", color.silver)
        fillCell(info_table, 1, 4, str.tostring(p_initial_range, format.mintick), color.silver)
        fillCell(info_table, 0, 5, "Gap", color.silver)
        fillCell(info_table, 1, 5, str.tostring(p_gap, format.mintick), color.silver)
        fillCell(info_table, 0, 6, "Corrected Range", color.silver)
        fillCell(info_table, 1, 6, str.tostring(p_corrected_range, format.mintick), color.silver)
        fillCell(info_table, 0, 7, "Max Level (Q1)", color.silver)
        fillCell(info_table, 1, 7, str.tostring(q1_level, format.mintick), color.silver)
        fillCell(info_table, 0, 8, "Min Level (Q4)", color.silver)
        fillCell(info_table, 1, 8, str.tostring(q4_level, format.mintick), color.silver)

f_day_id(t) =>
    year(t) * 10000 + month(t) * 100 + dayofmonth(t)

// ══════════════════════════════════════════════════════════════════════
// 📊 CÁLCULOS Y LÓGICA PRINCIPAL
// ══════════════════════════════════════════════════════════════════════

var line[] auto_lines = array.new_line()
var label[] auto_labels = array.new_label()
var line[] manual_lines = array.new_line()
var label[] manual_labels = array.new_label()
var table info_table = na

// --- Lógica de gestión de la tabla ---
if show_table
    if na(info_table)
        info_table := table.new(getTablePosition(table_pos_str), 2, 9, border_width=1)
    table.set_position(info_table, getTablePosition(table_pos_str))
else
    if not na(info_table)
        table.delete(info_table)
        info_table := na

// --- Lógica de modo automático ---
if use_automatic_date
    if array.size(manual_lines) > 0
        clearDrawings(manual_lines, manual_labels)

    [prev_day_time, prev_day_high, prev_day_low, prev_day_open, prev_day_close] = request.security(syminfo.tickerid, "D", [time[1], high[1], low[1], open[1], close[1]], lookahead=barmerge.lookahead_on)
    [current_day_open] = request.security(syminfo.tickerid, "D", [open], lookahead=barmerge.lookahead_on)
    is_new_day = na(dayofmonth[1]) or dayofmonth != dayofmonth[1]

    if is_new_day
        clearDrawings(auto_lines, auto_labels)
        float initial_range = prev_day_high - prev_day_low
        float gap = gap_mode == "Automatic" ? math.abs(current_day_open - prev_day_close) : manual_gap
        float corrected_range = initial_range + gap
        float base_price = na
        if manual_price > 0
            base_price := manual_price
        else if nr2_level_type == "Current Day Open"
            base_price := current_day_open
        else
            base_price := prev_day_close

        if not na(corrected_range) and not na(base_price) and corrected_range > 0
            calculateAndDrawLevels(corrected_range, base_price, auto_lines, auto_labels, bar_index, info_table, prev_day_time, prev_day_high, prev_day_low, initial_range, gap, corrected_range)

    if show_labels and array.size(auto_labels) > 0
        for i = 0 to array.size(auto_labels) - 1
            label.set_x(array.get(auto_labels, i), bar_index)
// --- Lógica de modo manual ---
else
    if array.size(auto_lines) > 0
        clearDrawings(auto_lines, auto_labels)

    if ta.change(selected_date) != 0
        clearDrawings(manual_lines, manual_labels)

    // --- Obtención de datos históricos (se ejecuta en cada barra para consistencia) ---
    [d_id, d_time, d_high, d_low, d_open, d_close] = request.security(syminfo.tickerid, "D", [f_day_id(time), time, high, low, open, close], lookahead=barmerge.lookahead_on)
    
    // 1. Definir IDs de fecha para el día de datos y el día de dibujo
    data_day_id = f_day_id(selected_date - 24 * 60 * 60 * 1000)
    drawing_day_id = f_day_id(selected_date)

    // 2. Crear series booleanas para encontrar esos días
    is_data_day = d_id == data_day_id
    is_drawing_day = d_id == drawing_day_id

    // 3. Usar valuewhen para capturar los valores de esos días específicos
    range_day_high = ta.valuewhen(is_data_day, d_high, 0)
    range_day_low = ta.valuewhen(is_data_day, d_low, 0)
    range_day_close = ta.valuewhen(is_data_day, d_close, 0)
    range_day_time = ta.valuewhen(is_data_day, d_time, 0)
    drawing_day_open = ta.valuewhen(is_drawing_day, d_open, 0)
    start_bar = ta.valuewhen(is_drawing_day, bar_index, 0)

    // --- Lógica de Dibujo (se ejecuta una sola vez) ---
    if array.size(manual_lines) == 0 and not na(range_day_high) and not na(start_bar)
        float initial_range = range_day_high - range_day_low
        float gap = gap_mode == "Automatic" ? math.abs(drawing_day_open - range_day_close) : manual_gap
        float corrected_range = initial_range + gap
        float base_price = na
        if manual_price > 0
            base_price := manual_price
        else if nr2_level_type == "Current Day Open"
            base_price := drawing_day_open
        else
            base_price := range_day_close

        if not na(corrected_range) and not na(base_price) and corrected_range > 0
            calculateAndDrawLevels(corrected_range, base_price, manual_lines, manual_labels, start_bar, info_table, range_day_time, range_day_high, range_day_low, initial_range, gap, corrected_range)

    if show_labels and array.size(manual_labels) > 0
        for i = 0 to array.size(manual_labels) - 1
            label.set_x(array.get(manual_labels, i), bar_index)
            