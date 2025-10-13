using System;
using System.Collections.Generic;
using System.Linq;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators.AV1s.Domain;
using NinjaTrader.NinjaScript.Indicators.AV1s.Services;
using NinjaTrader.NinjaScript.Indicators.AV1s.Rendering;

namespace NinjaTrader.NinjaScript.Indicators.AV1s.Infrastructure
{
    #region Dependency Injection Interfaces

    /// <summary>
    /// Service container for dependency injection
    /// </summary>
    public interface IServiceContainer : IDisposable
    {
        /// <summary>
        /// Registers a singleton service instance
        /// </summary>
        void RegisterSingleton<TService>(TService instance) where TService : class;

        /// <summary>
        /// Registers a factory function for creating service instances
        /// </summary>
        void RegisterFactory<TService>(Func<IServiceContainer, TService> factory) where TService : class;

        /// <summary>
        /// Registers a transient service type
        /// </summary>
        void RegisterTransient<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService, new();

        /// <summary>
        /// Resolves a service instance
        /// </summary>
        TService Resolve<TService>() where TService : class;

        /// <summary>
        /// Attempts to resolve a service instance
        /// </summary>
        bool TryResolve<TService>(out TService service) where TService : class;

        /// <summary>
        /// Checks if a service is registered
        /// </summary>
        bool IsRegistered<TService>() where TService : class;
    }

    /// <summary>
    /// Factory for creating and configuring service containers
    /// </summary>
    public interface IContainerFactory
    {
        /// <summary>
        /// Creates a container configured for NinjaTrader environment
        /// </summary>
        IServiceContainer CreateNinjaTraderContainer(NinjaScriptBase indicatorInstance);

        /// <summary>
        /// Creates a container configured for testing
        /// </summary>
        IServiceContainer CreateTestContainer();
    }

    #endregion

    #region Service Container Implementation

    /// <summary>
    /// Simple dependency injection container for AV1s services
    /// </summary>
    public sealed class AV1sServiceContainer : IServiceContainer
    {
        private readonly Dictionary<Type, object> singletons;
        private readonly Dictionary<Type, Func<IServiceContainer, object>> factories;
        private readonly Dictionary<Type, Type> transients;
        private readonly object containerLock = new object();
        private bool disposed = false;

        public AV1sServiceContainer()
        {
            singletons = new Dictionary<Type, object>();
            factories = new Dictionary<Type, Func<IServiceContainer, object>>();
            transients = new Dictionary<Type, Type>();
        }

        public void RegisterSingleton<TService>(TService instance) where TService : class
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(AV1sServiceContainer));

            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            lock (containerLock)
            {
                var serviceType = typeof(TService);

                if (singletons.ContainsKey(serviceType))
                    throw new InvalidOperationException($"Service {serviceType.Name} is already registered");

                singletons[serviceType] = instance;
            }
        }

        public void RegisterFactory<TService>(Func<IServiceContainer, TService> factory) where TService : class
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(AV1sServiceContainer));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            lock (containerLock)
            {
                var serviceType = typeof(TService);

                if (factories.ContainsKey(serviceType))
                    throw new InvalidOperationException($"Factory for {serviceType.Name} is already registered");

                factories[serviceType] = container => factory(container);
            }
        }

        public void RegisterTransient<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService, new()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(AV1sServiceContainer));

            lock (containerLock)
            {
                var serviceType = typeof(TService);
                var implementationType = typeof(TImplementation);

                if (transients.ContainsKey(serviceType))
                    throw new InvalidOperationException($"Transient service {serviceType.Name} is already registered");

                transients[serviceType] = implementationType;
            }
        }

        public TService Resolve<TService>() where TService : class
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(AV1sServiceContainer));

            var serviceType = typeof(TService);

            lock (containerLock)
            {
                // Check singletons first
                if (singletons.TryGetValue(serviceType, out var singleton))
                {
                    return (TService)singleton;
                }

                // Check factories
                if (factories.TryGetValue(serviceType, out var factory))
                {
                    try
                    {
                        return (TService)factory(this);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Failed to create service {serviceType.Name} using factory: {ex.Message}", ex);
                    }
                }

                // Check transients
                if (transients.TryGetValue(serviceType, out var implementationType))
                {
                    try
                    {
                        return (TService)Activator.CreateInstance(implementationType);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Failed to create transient service {serviceType.Name}: {ex.Message}", ex);
                    }
                }

                throw new InvalidOperationException($"Service {serviceType.Name} is not registered");
            }
        }

        public bool TryResolve<TService>(out TService service) where TService : class
        {
            try
            {
                service = Resolve<TService>();
                return true;
            }
            catch
            {
                service = null;
                return false;
            }
        }

        public bool IsRegistered<TService>() where TService : class
        {
            if (disposed)
                return false;

            var serviceType = typeof(TService);

            lock (containerLock)
            {
                return singletons.ContainsKey(serviceType) ||
                       factories.ContainsKey(serviceType) ||
                       transients.ContainsKey(serviceType);
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            lock (containerLock)
            {
                if (disposed)
                    return;

                disposed = true;

                // Dispose all disposable singletons
                foreach (var singleton in singletons.Values.OfType<IDisposable>())
                {
                    try
                    {
                        singleton.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error disposing service: {ex.Message}");
                    }
                }

                singletons.Clear();
                factories.Clear();
                transients.Clear();
            }
        }
    }

    #endregion

    #region Container Factory Implementation

    /// <summary>
    /// Factory for creating configured service containers
    /// </summary>
    public sealed class AV1sContainerFactory : IContainerFactory
    {
        public IServiceContainer CreateNinjaTraderContainer(NinjaScriptBase indicatorInstance)
        {
            if (indicatorInstance == null)
                throw new ArgumentNullException(nameof(indicatorInstance));

            var container = new AV1sServiceContainer();

            try
            {
                // Register logging service
                var logger = ServiceFactory.CreateNinjaTraderLogger(indicatorInstance.Print);
                container.RegisterSingleton<ILoggingService>(logger);

                // Register calculation service
                var calculator = ServiceFactory.CreateCalculator(logger);
                container.RegisterSingleton<IPriceLevelCalculator>(calculator);

                // Register session manager (requires SessionIterator from NinjaTrader)
                container.RegisterFactory<ISessionManager>(c =>
                {
                    var log = c.Resolve<ILoggingService>();
                    // Note: In real implementation, we'd get SessionIterator from indicatorInstance
                    // For now, we'll create a placeholder
                    return new NinjaTraderSessionManager(null, log);
                });

                // Register level renderer
                container.RegisterFactory<ILevelRenderer>(c =>
                {
                    var log = c.Resolve<ILoggingService>();
                    return RendererFactory.CreateNinjaTraderRenderer(log);
                });

                // Register indicator configuration service
                container.RegisterFactory<IIndicatorConfiguration>(c =>
                {
                    return new NinjaTraderIndicatorConfiguration(indicatorInstance);
                });

                return container;
            }
            catch
            {
                container.Dispose();
                throw;
            }
        }

        public IServiceContainer CreateTestContainer()
        {
            var container = new AV1sServiceContainer();

            try
            {
                // Register console logging for tests
                var logger = ServiceFactory.CreateConsoleLogger();
                container.RegisterSingleton<ILoggingService>(logger);

                // Register test calculator
                var calculator = ServiceFactory.CreateCalculator(logger);
                container.RegisterSingleton<IPriceLevelCalculator>(calculator);

                // Register mock session manager for tests
                container.RegisterFactory<ISessionManager>(c =>
                {
                    var log = c.Resolve<ILoggingService>();
                    return new MockSessionManager(log);
                });

                // Register mock renderer for tests
                container.RegisterFactory<ILevelRenderer>(c =>
                {
                    var log = c.Resolve<ILoggingService>();
                    return new MockLevelRenderer(log);
                });

                return container;
            }
            catch
            {
                container.Dispose();
                throw;
            }
        }
    }

    #endregion

    #region Configuration Services

    /// <summary>
    /// Service for accessing indicator configuration
    /// </summary>
    public interface IIndicatorConfiguration
    {
        /// <summary>
        /// Gets render parameters from indicator settings
        /// </summary>
        RenderParameters GetRenderParameters();

        /// <summary>
        /// Gets calculation parameters from indicator settings
        /// </summary>
        CalculationParameters GetCalculationParameters();

        /// <summary>
        /// Gets the selected date for calculations
        /// </summary>
        DateTime GetSelectedDate();

        /// <summary>
        /// Gets the number of days to draw
        /// </summary>
        int GetDaysToDraw();
    }

    /// <summary>
    /// NinjaTrader-specific configuration service
    /// </summary>
    public sealed class NinjaTraderIndicatorConfiguration : IIndicatorConfiguration
    {
        private readonly NinjaScriptBase indicatorInstance;

        public NinjaTraderIndicatorConfiguration(NinjaScriptBase indicatorInstance)
        {
            this.indicatorInstance = indicatorInstance ?? throw new ArgumentNullException(nameof(indicatorInstance));
        }

        public RenderParameters GetRenderParameters()
        {
            // In real implementation, these would come from indicator properties
            return new RenderParameters(
                lineWidth: 2,
                lineBufferPixels: 10,
                showDynamicLabels: true,
                labelOffsetX: 50,
                labelVerticalSpacing: 20,
                showHistoricalLevels: true
            );
        }

        public CalculationParameters GetCalculationParameters()
        {
            // In real implementation, these would come from indicator properties
            return new CalculationParameters(
                basePrice: 100.0,
                useGapCalculation: false,
                levelType: NR2LevelType.PreviousDayClose,
                manualPriceOverride: 0.0
            );
        }

        public DateTime GetSelectedDate()
        {
            // In real implementation, this would come from indicator property
            return DateTime.Today;
        }

        public int GetDaysToDraw()
        {
            // In real implementation, this would come from indicator property
            return 5;
        }
    }

    #endregion

    #region Mock Services for Testing

    /// <summary>
    /// Mock session manager for testing
    /// </summary>
    public sealed class MockSessionManager : ISessionManager
    {
        private readonly ILoggingService logger;

        public MockSessionManager(ILoggingService logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsNewTradingDay(DateTime currentTime)
        {
            logger.LogDebug($"MockSessionManager.IsNewTradingDay({currentTime})");
            return true; // Simplified for testing
        }

        public TradingDayData GetPriorDayData(DateTime currentTime)
        {
            logger.LogDebug($"MockSessionManager.GetPriorDayData({currentTime})");
            return new TradingDayData(
                currentTime.AddDays(-1),
                105.0, // High
                95.0,  // Low
                100.0, // Open
                102.0  // Close
            );
        }

        public TradingDayData GetCurrentDayData(DateTime currentTime)
        {
            logger.LogDebug($"MockSessionManager.GetCurrentDayData({currentTime})");
            return new TradingDayData(
                currentTime,
                110.0, // High
                98.0,  // Low
                102.0, // Open
                108.0  // Close
            );
        }

        public DateTime? GetTradingDay(DateTime date)
        {
            logger.LogDebug($"MockSessionManager.GetTradingDay({date})");
            return date.Date;
        }

        public double ApplyGapCalculation(TradingDayData priorDayData, TradingDayData currentDayData, bool useGapCalculation)
        {
            logger.LogDebug($"MockSessionManager.ApplyGapCalculation(gap: {useGapCalculation})");
            return priorDayData?.Range ?? 10.0;
        }
    }

    /// <summary>
    /// Mock level renderer for testing
    /// </summary>
    public sealed class MockLevelRenderer : ILevelRenderer, IDisposable
    {
        private readonly ILoggingService logger;
        private readonly List<string> renderedLevels;
        private bool disposed = false;

        public MockLevelRenderer(ILoggingService logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.renderedLevels = new List<string>();
        }

        public void RenderLevels(DayLevels dayLevels, RenderContext context)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(MockLevelRenderer));

            logger.LogDebug($"MockLevelRenderer.RenderLevels for {dayLevels.TradingDay:yyyy-MM-dd}");

            foreach (var level in dayLevels.GetValidLevels())
            {
                var levelKey = $"{dayLevels.TradingDay:yyyyMMdd}_{level.Name}";
                renderedLevels.Add(levelKey);
                logger.LogDebug($"Rendered mock level: {levelKey} at {level.Value:F4}");
            }
        }

        public void RenderDynamicLabels(DayLevels dayLevels, RenderContext context)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(MockLevelRenderer));

            logger.LogDebug($"MockLevelRenderer.RenderDynamicLabels for {dayLevels.TradingDay:yyyy-MM-dd}");
        }

        public void ClearLevels(DateTime tradingDay)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(MockLevelRenderer));

            logger.LogDebug($"MockLevelRenderer.ClearLevels for {tradingDay:yyyy-MM-dd}");

            var toRemove = renderedLevels.Where(l => l.StartsWith(tradingDay.ToString("yyyyMMdd"))).ToList();
            foreach (var level in toRemove)
            {
                renderedLevels.Remove(level);
            }
        }

        public void ClearAllLevels()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(MockLevelRenderer));

            logger.LogDebug("MockLevelRenderer.ClearAllLevels");
            renderedLevels.Clear();
        }

        public void UpdateRenderParameters(RenderParameters parameters)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(MockLevelRenderer));

            logger.LogDebug("MockLevelRenderer.UpdateRenderParameters");
        }

        public void Dispose()
        {
            if (!disposed)
            {
                logger.LogDebug("MockLevelRenderer disposed");
                disposed = true;
            }
        }
    }

    #endregion

    #region Service Locator Pattern

    /// <summary>
    /// Static service locator for easy access to services
    /// </summary>
    public static class ServiceLocator
    {
        private static IServiceContainer currentContainer;
        private static readonly object locatorLock = new object();

        /// <summary>
        /// Sets the current service container
        /// </summary>
        public static void SetContainer(IServiceContainer container)
        {
            lock (locatorLock)
            {
                currentContainer?.Dispose();
                currentContainer = container;
            }
        }

        /// <summary>
        /// Gets the current service container
        /// </summary>
        public static IServiceContainer GetContainer()
        {
            lock (locatorLock)
            {
                if (currentContainer == null)
                    throw new InvalidOperationException("No service container has been configured. Call SetContainer first.");

                return currentContainer;
            }
        }

        /// <summary>
        /// Resolves a service from the current container
        /// </summary>
        public static TService Resolve<TService>() where TService : class
        {
            return GetContainer().Resolve<TService>();
        }

        /// <summary>
        /// Disposes the current container
        /// </summary>
        public static void Dispose()
        {
            lock (locatorLock)
            {
                currentContainer?.Dispose();
                currentContainer = null;
            }
        }
    }

    #endregion
}