// Improved architecture with separation of concerns

// Domain models
public class TradingDayData
{
    public DateTime Date { get; }
    public double High { get; }
    public double Low { get; }
    public double Open { get; }
    public double Close { get; }
    public double Range => High - Low;

    public TradingDayData(DateTime date, double high, double low, double open, double close)
    {
        if (high < low) throw new ArgumentException("High cannot be less than low");
        Date = date;
        High = high;
        Low = low;
        Open = open;
        Close = close;
    }
}

public class PriceLevel
{
    public string Name { get; }
    public double Value { get; }
    public LevelType Type { get; }
    public Brush Color { get; }

    public PriceLevel(string name, double value, LevelType type, Brush color)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Value = value;
        Type = type;
        Color = color ?? throw new ArgumentNullException(nameof(color));
    }
}

// Service interfaces for dependency injection
public interface IPriceLevelCalculator
{
    IReadOnlyCollection<PriceLevel> CalculateLevels(TradingDayData priorDay, CalculationParameters parameters);
}

public interface ISessionManager
{
    bool IsNewTradingDay(DateTime currentTime);
    TradingDayData GetPriorDayData(DateTime currentTime);
}

public interface ILevelRenderer
{
    void RenderLevels(IEnumerable<PriceLevel> levels, RenderContext context);
}

// Configuration object
public class CalculationParameters
{
    public double BasePrice { get; }
    public bool UseGapCalculation { get; }
    public NR2LevelType LevelType { get; }

    public CalculationParameters(double basePrice, bool useGapCalculation, NR2LevelType levelType)
    {
        if (basePrice <= 0) throw new ArgumentException("Base price must be positive");
        BasePrice = basePrice;
        UseGapCalculation = useGapCalculation;
        LevelType = levelType;
    }
}

// Main indicator becomes orchestrator
public class AV1s : Indicator
{
    private readonly IPriceLevelCalculator calculator;
    private readonly ISessionManager sessionManager;
    private readonly ILevelRenderer renderer;

    public AV1s() : this(
        new StandardPriceLevelCalculator(),
        new NinjaTraderSessionManager(),
        new SharpDXLevelRenderer())
    {
    }

    // Constructor injection for testability
    internal AV1s(IPriceLevelCalculator calculator, ISessionManager sessionManager, ILevelRenderer renderer)
    {
        this.calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        this.sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        this.renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    protected override void OnBarUpdate()
    {
        if (!UseAutomaticDate) return;

        try
        {
            if (sessionManager.IsNewTradingDay(Time[0]))
            {
                var priorDayData = sessionManager.GetPriorDayData(Time[0]);
                var parameters = CreateCalculationParameters();
                var levels = calculator.CalculateLevels(priorDayData, parameters);

                UpdateCurrentDayLevels(levels);
            }
        }
        catch (Exception ex)
        {
            LogError($"Error in OnBarUpdate: {ex.Message}");
        }
    }
}