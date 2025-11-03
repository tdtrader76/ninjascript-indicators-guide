//@version=5

indicator("Expected Move Plotter", max_bars_back = 252, overlay=true)
length = input.int(252) 
showema = input.bool(true, "Show EMA 9/21")
showbacktest = input.bool(true, "Show Backtest Results") 

timeframe = input.timeframe("D", "Timeframe")

[hi, lo, op, top, hitoop, optolo] = request.security(syminfo.ticker, timeframe, [high[1], low[1], open[1], open, (high - open)[1], (open - low)[1]], lookahead=barmerge.lookahead_on) 

isbullishday = hi - op > op - lo 
isbearishday = op - lo > hi - op 

bull_sum = 0.0
bear_sum = 0.0
for i = 0 to length
    bull_sum += hitoop[i]
    bear_sum += optolo[i]

daily_bull_avg = request.security(syminfo.ticker, timeframe, bull_sum / length, lookahead=barmerge.lookahead_on)
daily_bear_avg = request.security(syminfo.ticker, timeframe, bear_sum / length, lookahead=barmerge.lookahead_on)

plota = plot(top + daily_bull_avg, 'Max Hi', color=color.green)
plotb = plot(top - daily_bear_avg, 'Max Lo', color=color.red)

// Sentiment 

ema9 = ta.ema(close, 9) 
ema21 = ta.ema(close, 21) 


ema_9 = 0.0 
ema_21 = 0.0 
if showema 
    ema_9 := ema9 
    ema_21 := ema21 

plot(ema_9, "EMA9", color=color.yellow, linewidth=2) 
plot(ema_21, "EMA21", color=color.purple, linewidth=2) 

// Success Rate 

hi_test = request.security(syminfo.ticker, timeframe, high, lookahead=barmerge.lookahead_on) 
lo_test = request.security(syminfo.ticker, timeframe, low, lookahead=barmerge.lookahead_on) 

bool high_success = hi_test >= top + daily_bull_avg 
bool low_success = lo_test <= top - daily_bear_avg 

var float high_success_result = 0.0 
var float low_success_result = 0.0 

for i = 0 to length 
    if high_success 
        high_success_result := high_success_result + 1 
    if low_success    
        low_success_result := low_success_result + 1 

bull_success = (high_success_result / (high_success_result + low_success_result)) * 100 
bear_success = (low_success_result / (high_success_result + low_success_result)) * 100 

var backtest = table.new(position.middle_right, 5, 5, bgcolor = color.purple) 

if showbacktest
    table.cell(backtest, 1, 1, text = "Bull Success = " + str.tostring(math.round(bull_success,2)) + "%", bgcolor = color.black, text_color = color.white) 
    table.cell(backtest, 1, 2, text = "Bear Success = " + str.tostring(math.round(bear_success, 2)) + "%", bgcolor = color.black, text_color = color.white)
    table.cell(backtest, 1, 3, text = "High EM = " + str.tostring(math.round(top + daily_bull_avg, 2)), bgcolor = color.black, text_color = color.white)
    table.cell(backtest, 1, 4, text = "Low EM = " + str.tostring(math.round(top - daily_bear_avg, 2)), bgcolor = color.black, text_color = color.white)