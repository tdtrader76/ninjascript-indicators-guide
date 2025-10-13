using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using NinjaTrader.NinjaScript.Indicators.AV1s.Domain;
using NinjaTrader.NinjaScript.Indicators.AV1s.Services;

namespace NinjaTrader.NinjaScript.Indicators.AV1s.Testing
{
    #region Test Framework Base Classes

    /// <summary>
    /// Base class for unit tests with common assertion methods
    /// </summary>
    public abstract class TestBase
    {
        protected List<string> testResults = new List<string>();
        protected int passedTests = 0;
        protected int failedTests = 0;

        /// <summary>
        /// Asserts that a condition is true
        /// </summary>
        protected void Assert(bool condition, string message)
        {
            if (condition)
            {
                passedTests++;
                testResults.Add($"✅ PASS: {message}");
            }
            else
            {
                failedTests++;
                testResults.Add($"❌ FAIL: {message}");
            }
        }

        /// <summary>
        /// Asserts that two values are equal
        /// </summary>
        protected void AssertEqual<T>(T expected, T actual, string message)
        {
            bool isEqual = EqualityComparer<T>.Default.Equals(expected, actual);
            if (isEqual)
            {
                passedTests++;
                testResults.Add($"✅ PASS: {message} (Expected: {expected}, Actual: {actual})");
            }
            else
            {
                failedTests++;
                testResults.Add($"❌ FAIL: {message} (Expected: {expected}, Actual: {actual})");
            }
        }

        /// <summary>
        /// Asserts that two double values are approximately equal
        /// </summary>
        protected void AssertApproximateEqual(double expected, double actual, double tolerance, string message)
        {
            bool isEqual = Math.Abs(expected - actual) <= tolerance;
            if (isEqual)
            {
                passedTests++;
                testResults.Add($"✅ PASS: {message} (Expected: {expected:F4}, Actual: {actual:F4}, Tolerance: {tolerance:F4})");
            }
            else
            {
                failedTests++;
                testResults.Add($"❌ FAIL: {message} (Expected: {expected:F4}, Actual: {actual:F4}, Tolerance: {tolerance:F4})");
            }
        }

        /// <summary>
        /// Asserts that a condition throws an exception
        /// </summary>
        protected void AssertThrows<T>(Action action, string message) where T : Exception
        {
            try
            {
                action();
                failedTests++;
                testResults.Add($"❌ FAIL: {message} (Expected exception {typeof(T).Name} but none was thrown)");
            }
            catch (T)
            {
                passedTests++;
                testResults.Add($"✅ PASS: {message} (Expected exception {typeof(T).Name} was thrown)");
            }
            catch (Exception ex)
            {
                failedTests++;
                testResults.Add($"❌ FAIL: {message} (Expected {typeof(T).Name} but got {ex.GetType().Name})");
            }
        }

        /// <summary>
        /// Runs all test methods and returns summary
        /// </summary>
        public virtual TestSummary RunAllTests()
        {
            testResults.Clear();
            passedTests = 0;
            failedTests = 0;

            var testMethods = GetType().GetMethods()
                .Where(m => m.Name.StartsWith("Test") && m.IsPublic && m.GetParameters().Length == 0);

            foreach (var method in testMethods)
            {
                try
                {
                    testResults.Add($"🧪 Running {method.Name}...");
                    method.Invoke(this, null);
                }
                catch (Exception ex)
                {
                    failedTests++;
                    testResults.Add($"❌ FAIL: {method.Name} threw unexpected exception: {ex.Message}");
                }
            }

            return new TestSummary(GetType().Name, passedTests, failedTests, testResults);
        }
    }

    /// <summary>
    /// Summary of test execution results
    /// </summary>
    public sealed class TestSummary
    {
        public string TestClassName { get; }
        public int PassedTests { get; }
        public int FailedTests { get; }
        public int TotalTests => PassedTests + FailedTests;
        public bool AllPassed => FailedTests == 0;
        public List<string> Details { get; }

        public TestSummary(string testClassName, int passedTests, int failedTests, List<string> details)
        {
            TestClassName = testClassName;
            PassedTests = passedTests;
            FailedTests = failedTests;
            Details = new List<string>(details);
        }

        public override string ToString()
        {
            return $"{TestClassName}: {PassedTests}/{TotalTests} passed ({(AllPassed ? "✅ ALL PASSED" : "❌ SOME FAILED")})";
        }
    }

    #endregion

    #region Domain Model Tests

    /// <summary>
    /// Tests for TradingDayData domain model
    /// </summary>
    public class TradingDayDataTests : TestBase
    {
        public void TestValidTradingDayData()
        {
            var date = new DateTime(2024, 1, 15);
            var data = new TradingDayData(date, 100.50, 99.25, 99.75, 100.00);

            Assert(data.IsValid, "Valid trading day data should be marked as valid");
            AssertEqual(date, data.Date, "Date should be preserved");
            AssertApproximateEqual(100.50, data.High, 0.001, "High should be preserved");
            AssertApproximateEqual(99.25, data.Low, 0.001, "Low should be preserved");
            AssertApproximateEqual(1.25, data.Range, 0.001, "Range should be calculated correctly");
        }

        public void TestInvalidTradingDayData()
        {
            AssertThrows<ArgumentException>(() =>
                new TradingDayData(DateTime.Today, 99.0, 100.0, 99.5, 99.75),
                "Should throw when high < low");

            AssertThrows<ArgumentException>(() =>
                new TradingDayData(DateTime.Today, double.NaN, 99.0, 99.5, 99.75),
                "Should throw when high is NaN");
        }

        public void TestTradingDayDataEquality()
        {
            var date = DateTime.Today;
            var data1 = new TradingDayData(date, 100.0, 99.0, 99.5, 99.75);
            var data2 = new TradingDayData(date, 100.0, 99.0, 99.5, 99.75);

            Assert(data1.Equals(data2), "Identical trading day data should be equal");
            AssertEqual(data1.GetHashCode(), data2.GetHashCode(), "Equal objects should have same hash code");
        }
    }

    /// <summary>
    /// Tests for PriceLevel domain model
    /// </summary>
    public class PriceLevelTests : TestBase
    {
        public void TestValidPriceLevel()
        {
            var level = new PriceLevel("Q1", 100.50, LevelType.Quarter, Brushes.Yellow, "Test level", 0.5);

            Assert(level.IsValid, "Valid price level should be marked as valid");
            AssertEqual("Q1", level.Name, "Name should be preserved");
            AssertApproximateEqual(100.50, level.Value, 0.001, "Value should be preserved");
            AssertEqual(LevelType.Quarter, level.Type, "Type should be preserved");
        }

        public void TestInvalidPriceLevel()
        {
            AssertThrows<ArgumentNullException>(() =>
                new PriceLevel(null, 100.0, LevelType.Quarter, Brushes.Yellow),
                "Should throw when name is null");

            AssertThrows<ArgumentException>(() =>
                new PriceLevel("", 100.0, LevelType.Quarter, Brushes.Yellow),
                "Should throw when name is empty");
        }

        public void TestPriceLevelWithNaNValue()
        {
            var level = new PriceLevel("Test", double.NaN, LevelType.Quarter, Brushes.Yellow);
            Assert(!level.IsValid, "Price level with NaN value should be invalid");
        }
    }

    /// <summary>
    /// Tests for CalculationParameters domain model
    /// </summary>
    public class CalculationParametersTests : TestBase
    {
        public void TestValidCalculationParameters()
        {
            var parameters = new CalculationParameters(100.0, true, NR2LevelType.PreviousDayClose, 105.0);

            Assert(parameters.IsValid, "Valid parameters should be marked as valid");
            AssertApproximateEqual(100.0, parameters.BasePrice, 0.001, "Base price should be preserved");
            Assert(parameters.UseGapCalculation, "Gap calculation flag should be preserved");
            AssertEqual(NR2LevelType.PreviousDayClose, parameters.LevelType, "Level type should be preserved");
        }

        public void TestInvalidCalculationParameters()
        {
            AssertThrows<ArgumentException>(() =>
                new CalculationParameters(-1.0, false, NR2LevelType.PreviousDayClose),
                "Should throw when base price is negative");

            AssertThrows<ArgumentException>(() =>
                new CalculationParameters(double.NaN, false, NR2LevelType.PreviousDayClose),
                "Should throw when base price is NaN");
        }
    }

    #endregion

    #region Service Tests

    /// <summary>
    /// Tests for StandardPriceLevelCalculator service
    /// </summary>
    public class StandardPriceLevelCalculatorTests : TestBase
    {
        private IPriceLevelCalculator calculator;

        public StandardPriceLevelCalculatorTests()
        {
            calculator = ServiceFactory.CreateCalculator(new TestLoggingService());
        }

        public void TestValidLevelCalculation()
        {
            var tradingDay = new TradingDayData(DateTime.Today, 102.0, 100.0, 100.5, 101.0);
            var parameters = new CalculationParameters(101.0, false, NR2LevelType.PreviousDayClose);

            var result = calculator.CalculateLevels(tradingDay, parameters);

            Assert(result.Success, "Calculation should succeed with valid inputs");
            Assert(result.DayLevels != null, "Result should contain day levels");
            Assert(result.DayLevels.HasValidLevels, "Day levels should contain valid levels");

            // Test specific level calculations
            var q1Level = result.DayLevels.GetLevel("Q1");
            Assert(q1Level != null, "Q1 level should be calculated");
            AssertApproximateEqual(102.0, q1Level.Value, 0.1, "Q1 should be base + 50% of range");
        }

        public void TestInvalidInputHandling()
        {
            var result = calculator.CalculateLevels(null, null);

            Assert(!result.Success, "Calculation should fail with null inputs");
            Assert(!string.IsNullOrEmpty(result.ErrorMessage), "Error message should be provided");
        }

        public void TestInputValidation()
        {
            var validTradingDay = new TradingDayData(DateTime.Today, 102.0, 100.0, 100.5, 101.0);
            var validParameters = new CalculationParameters(101.0, false, NR2LevelType.PreviousDayClose);

            var (isValid, _) = calculator.ValidateInputs(validTradingDay, validParameters);
            Assert(isValid, "Valid inputs should pass validation");

            var (isInvalid, errorMsg) = calculator.ValidateInputs(null, validParameters);
            Assert(!isInvalid, "Null trading day should fail validation");
            Assert(!string.IsNullOrEmpty(errorMsg), "Error message should be provided for invalid inputs");
        }

        public void TestSpecificLevelCalculation()
        {
            var tradingDay = new TradingDayData(DateTime.Today, 102.0, 100.0, 100.5, 101.0);
            var parameters = new CalculationParameters(101.0, false, NR2LevelType.PreviousDayClose);
            var q1Definition = StandardLevelDefinitions.Q1;

            var level = calculator.CalculateLevel(q1Definition, tradingDay, parameters);

            Assert(level != null, "Level calculation should return a result");
            AssertEqual("Q1", level.Name, "Level name should match definition");
            Assert(level.IsValid, "Calculated level should be valid");
        }
    }

    /// <summary>
    /// Tests for StandardLevelDefinitions
    /// </summary>
    public class StandardLevelDefinitionsTests : TestBase
    {
        public void TestAllDefinitionsAreValid()
        {
            var definitions = StandardLevelDefinitions.All;

            Assert(definitions.Count > 0, "Should have at least one level definition");

            foreach (var definition in definitions)
            {
                Assert(!string.IsNullOrEmpty(definition.Name), $"Definition {definition.Name} should have a name");
                Assert(definition.Color != null, $"Definition {definition.Name} should have a color");
                Assert(definition.Multiplier >= 0, $"Definition {definition.Name} should have non-negative multiplier");
            }
        }

        public void TestSpecificDefinitions()
        {
            var q1 = StandardLevelDefinitions.Q1;
            AssertEqual("Q1", q1.Name, "Q1 definition should have correct name");
            AssertApproximateEqual(0.5, q1.Multiplier, 0.001, "Q1 should have 0.5 multiplier");

            var std1Plus = StandardLevelDefinitions.Std1Plus;
            AssertEqual("Std1+", std1Plus.Name, "Std1+ definition should have correct name");
            AssertApproximateEqual(0.0855, std1Plus.Multiplier, 0.0001, "Std1+ should have correct multiplier");
        }

        public void TestFilterByType()
        {
            var quarterLevels = StandardLevelDefinitions.ByType(LevelType.Quarter).ToList();
            Assert(quarterLevels.Count > 0, "Should have quarter level definitions");

            var stdLevels = StandardLevelDefinitions.ByType(LevelType.StandardDeviation).ToList();
            Assert(stdLevels.Count > 0, "Should have standard deviation level definitions");
        }

        public void TestEnabledOnly()
        {
            var enabledLevels = StandardLevelDefinitions.EnabledOnly().ToList();
            Assert(enabledLevels.Count > 0, "Should have enabled level definitions");
            Assert(enabledLevels.All(l => l.IsEnabled), "All returned levels should be enabled");
        }
    }

    #endregion

    #region Test Utilities

    /// <summary>
    /// Test logging service that captures log messages for verification
    /// </summary>
    public class TestLoggingService : ILoggingService
    {
        public List<string> ErrorMessages { get; } = new List<string>();
        public List<string> WarningMessages { get; } = new List<string>();
        public List<string> InfoMessages { get; } = new List<string>();
        public List<string> DebugMessages { get; } = new List<string>();

        public void LogError(string message, Exception exception = null)
        {
            var fullMessage = exception != null ? $"{message} - {exception.Message}" : message;
            ErrorMessages.Add(fullMessage);
        }

        public void LogWarning(string message)
        {
            WarningMessages.Add(message);
        }

        public void LogInfo(string message)
        {
            InfoMessages.Add(message);
        }

        public void LogDebug(string message)
        {
            DebugMessages.Add(message);
        }

        public void Clear()
        {
            ErrorMessages.Clear();
            WarningMessages.Clear();
            InfoMessages.Clear();
            DebugMessages.Clear();
        }
    }

    /// <summary>
    /// Test data factory for creating test objects
    /// </summary>
    public static class TestDataFactory
    {
        public static TradingDayData CreateValidTradingDay(DateTime? date = null)
        {
            return new TradingDayData(
                date ?? DateTime.Today,
                high: 102.50,
                low: 100.25,
                open: 100.75,
                close: 101.50
            );
        }

        public static CalculationParameters CreateValidParameters(double? basePrice = null)
        {
            return new CalculationParameters(
                basePrice ?? 101.0,
                useGapCalculation: false,
                NR2LevelType.PreviousDayClose
            );
        }

        public static PriceLevel CreateValidPriceLevel(string name = "Test", double value = 100.0)
        {
            return new PriceLevel(
                name,
                value,
                LevelType.Quarter,
                Brushes.Yellow,
                "Test level"
            );
        }
    }

    /// <summary>
    /// Test runner that executes all test classes
    /// </summary>
    public static class TestRunner
    {
        public static List<TestSummary> RunAllTests()
        {
            var testClasses = new List<TestBase>
            {
                new TradingDayDataTests(),
                new PriceLevelTests(),
                new CalculationParametersTests(),
                new StandardPriceLevelCalculatorTests(),
                new StandardLevelDefinitionsTests()
            };

            var summaries = new List<TestSummary>();

            foreach (var testClass in testClasses)
            {
                try
                {
                    var summary = testClass.RunAllTests();
                    summaries.Add(summary);
                }
                catch (Exception ex)
                {
                    summaries.Add(new TestSummary(
                        testClass.GetType().Name,
                        0,
                        1,
                        new List<string> { $"❌ Test class failed to run: {ex.Message}" }
                    ));
                }
            }

            return summaries;
        }

        public static string GenerateReport(List<TestSummary> summaries)
        {
            var report = new System.Text.StringBuilder();
            var totalPassed = summaries.Sum(s => s.PassedTests);
            var totalFailed = summaries.Sum(s => s.FailedTests);
            var totalTests = totalPassed + totalFailed;

            report.AppendLine("=== AV1s Unit Test Report ===");
            report.AppendLine($"Overall: {totalPassed}/{totalTests} tests passed");
            report.AppendLine($"Status: {(totalFailed == 0 ? "✅ ALL TESTS PASSED" : "❌ SOME TESTS FAILED")}");
            report.AppendLine();

            foreach (var summary in summaries)
            {
                report.AppendLine($"{summary}");
            }

            report.AppendLine();
            report.AppendLine("=== Detailed Results ===");

            foreach (var summary in summaries)
            {
                report.AppendLine($"\n--- {summary.TestClassName} ---");
                foreach (var detail in summary.Details)
                {
                    report.AppendLine(detail);
                }
            }

            return report.ToString();
        }
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Integration tests for the complete AV1s system
    /// </summary>
    public sealed class AV1sIntegrationTests : TestBase
    {
        public void RunAllTests()
        {
            TestResults("=== AV1s Integration Tests ===");

            TestServiceContainerConfiguration();
            TestFullCalculationPipeline();
            TestDependencyInjectionIntegration();
            TestErrorHandlingIntegration();
            TestPerformanceBaseline();

            PrintTestSummary();
        }

        private void TestServiceContainerConfiguration()
        {
            TestResults("\n--- Service Container Configuration Tests ---");

            try
            {
                // Test container creation and service registration
                var containerFactory = new AV1sContainerFactory();
                var container = containerFactory.CreateTestContainer();

                Assert(container != null, "Container created successfully");
                Assert(container.IsRegistered<ILoggingService>(), "Logging service registered");
                Assert(container.IsRegistered<IPriceLevelCalculator>(), "Calculator service registered");
                Assert(container.IsRegistered<ISessionManager>(), "Session manager registered");
                Assert(container.IsRegistered<ILevelRenderer>(), "Renderer service registered");

                // Test service resolution
                var logger = container.Resolve<ILoggingService>();
                var calculator = container.Resolve<IPriceLevelCalculator>();
                var sessionManager = container.Resolve<ISessionManager>();
                var renderer = container.Resolve<ILevelRenderer>();

                Assert(logger != null, "Logger resolved successfully");
                Assert(calculator != null, "Calculator resolved successfully");
                Assert(sessionManager != null, "Session manager resolved successfully");
                Assert(renderer != null, "Renderer resolved successfully");

                // Test singleton behavior
                var logger2 = container.Resolve<ILoggingService>();
                Assert(ReferenceEquals(logger, logger2), "Singleton services return same instance");

                container.Dispose();
                Assert(true, "Container disposed without errors");
            }
            catch (Exception ex)
            {
                Assert(false, $"Service container configuration failed: {ex.Message}");
            }
        }

        private void TestFullCalculationPipeline()
        {
            TestResults("\n--- Full Calculation Pipeline Tests ---");

            try
            {
                var containerFactory = new AV1sContainerFactory();
                using var container = containerFactory.CreateTestContainer();

                var calculator = container.Resolve<IPriceLevelCalculator>();
                var sessionManager = container.Resolve<ISessionManager>();

                // Create test trading day data
                var testDate = DateTime.Today;
                var tradingDayData = new TradingDayData(testDate, 110.0, 90.0, 100.0, 105.0);
                var parameters = new CalculationParameters(105.0, false, NR2LevelType.PreviousDayClose);

                // Test full pipeline
                var result = calculator.CalculateLevels(tradingDayData, parameters);

                Assert(result != null, "Calculation result returned");
                Assert(result.Success, "Calculation succeeded");
                Assert(result.DayLevels != null, "Day levels created");
                Assert(result.DayLevels.HasValidLevels, "Valid levels generated");
                Assert(result.DayLevels.Levels.Count > 0, $"Levels generated: {result.DayLevels.Levels.Count}");

                // Verify specific levels exist
                var q1Level = result.DayLevels.GetLevel("Q1");
                var q8Level = result.DayLevels.GetLevel("Q8");

                Assert(q1Level != null, "Q1 level generated");
                Assert(q8Level != null, "Q8 level generated");
                Assert(q1Level.IsValid, "Q1 level is valid");
                Assert(q8Level.IsValid, "Q8 level is valid");
                Assert(q1Level.Value > tradingDayData.Close, "Q1 level above close price");
                Assert(q8Level.Value < tradingDayData.Close, "Q8 level below close price");
            }
            catch (Exception ex)
            {
                Assert(false, $"Full calculation pipeline failed: {ex.Message}");
            }
        }

        private void TestDependencyInjectionIntegration()
        {
            TestResults("\n--- Dependency Injection Integration Tests ---");

            try
            {
                var containerFactory = new AV1sContainerFactory();
                using var container = containerFactory.CreateTestContainer();

                // Test that all services can access their dependencies
                var calculator = container.Resolve<IPriceLevelCalculator>();
                var renderer = container.Resolve<ILevelRenderer>();

                // Create test data
                var testDate = DateTime.Today;
                var tradingDayData = new TradingDayData(testDate, 110.0, 90.0, 100.0, 105.0);
                var parameters = new CalculationParameters(105.0, false, NR2LevelType.PreviousDayClose);

                // Test calculation service works
                var result = calculator.CalculateLevels(tradingDayData, parameters);
                Assert(result.Success, "Calculator works with injected dependencies");

                // Test rendering service can process results
                if (result.Success)
                {
                    var renderParams = new RenderParameters(2, 10, true, 50, 20);
                    var renderContext = new RenderContext(testDate, 0, 100, 50, 1.0, renderParams);

                    renderer.RenderLevels(result.DayLevels, renderContext);
                    Assert(true, "Renderer works with injected dependencies");
                }

                // Test service disposal chain
                container.Dispose();
                Assert(true, "All services disposed properly through container");
            }
            catch (Exception ex)
            {
                Assert(false, $"Dependency injection integration failed: {ex.Message}");
            }
        }

        private void TestErrorHandlingIntegration()
        {
            TestResults("\n--- Error Handling Integration Tests ---");

            try
            {
                var containerFactory = new AV1sContainerFactory();
                using var container = containerFactory.CreateTestContainer();

                var calculator = container.Resolve<IPriceLevelCalculator>();

                // Test with invalid data
                var invalidData = new TradingDayData(DateTime.Today, double.NaN, 90.0, 100.0, 105.0);
                var validParams = new CalculationParameters(105.0, false, NR2LevelType.PreviousDayClose);

                var result = calculator.CalculateLevels(invalidData, validParams);
                Assert(!result.Success, "Calculator properly handles invalid trading data");
                Assert(!string.IsNullOrEmpty(result.ErrorMessage), "Error message provided for invalid data");

                // Test with null parameters
                try
                {
                    var validData = new TradingDayData(DateTime.Today, 110.0, 90.0, 100.0, 105.0);
                    var nullResult = calculator.CalculateLevels(validData, null);
                    Assert(!nullResult.Success, "Calculator handles null parameters gracefully");
                }
                catch (ArgumentNullException)
                {
                    Assert(true, "Calculator throws appropriate exception for null parameters");
                }

                // Test container error handling
                try
                {
                    container.Resolve<IPriceLevelCalculator>();
                    container.Dispose();
                    container.Resolve<IPriceLevelCalculator>(); // Should fail
                    Assert(false, "Should have thrown ObjectDisposedException");
                }
                catch (ObjectDisposedException)
                {
                    Assert(true, "Container properly throws exception when used after disposal");
                }
            }
            catch (Exception ex)
            {
                Assert(false, $"Error handling integration test failed: {ex.Message}");
            }
        }

        private void TestPerformanceBaseline()
        {
            TestResults("\n--- Performance Baseline Tests ---");

            try
            {
                var containerFactory = new AV1sContainerFactory();
                using var container = containerFactory.CreateTestContainer();

                var calculator = container.Resolve<IPriceLevelCalculator>();

                // Create test data
                var testDate = DateTime.Today;
                var tradingDayData = new TradingDayData(testDate, 110.0, 90.0, 100.0, 105.0);
                var parameters = new CalculationParameters(105.0, false, NR2LevelType.PreviousDayClose);

                // Measure calculation performance
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var iterations = 1000;

                for (int i = 0; i < iterations; i++)
                {
                    var result = calculator.CalculateLevels(tradingDayData, parameters);
                    Assert(result.Success, $"Calculation {i} succeeded");
                }

                stopwatch.Stop();
                var avgTime = stopwatch.ElapsedMilliseconds / (double)iterations;

                Assert(avgTime < 10.0, $"Average calculation time acceptable: {avgTime:F2}ms");
                Assert(stopwatch.ElapsedMilliseconds < 5000, $"Total time for {iterations} calculations: {stopwatch.ElapsedMilliseconds}ms");

                TestResults($"Performance: {avgTime:F2}ms avg, {stopwatch.ElapsedMilliseconds}ms total for {iterations} calculations");
            }
            catch (Exception ex)
            {
                Assert(false, $"Performance baseline test failed: {ex.Message}");
            }
        }
    }

    #endregion

    #region Test Runners and Utilities

    /// <summary>
    /// Test runner for executing all AV1s tests
    /// </summary>
    public static class AV1sTestRunner
    {
        /// <summary>
        /// Runs all unit and integration tests
        /// </summary>
        public static TestSummary RunAllTests()
        {
            var summary = new TestSummary();

            try
            {
                Console.WriteLine("=== AV1s Test Suite ===");
                Console.WriteLine($"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine();

                // Run domain model tests
                var domainTests = new TradingDayDataTests();
                domainTests.RunAllTests();
                summary.AddResults(domainTests.GetSummary());

                var priceLevelTests = new PriceLevelTests();
                priceLevelTests.RunAllTests();
                summary.AddResults(priceLevelTests.GetSummary());

                var calculationParametersTests = new CalculationParametersTests();
                calculationParametersTests.RunAllTests();
                summary.AddResults(calculationParametersTests.GetSummary());

                // Run service tests
                var calculatorTests = new PriceLevelCalculatorTests();
                calculatorTests.RunAllTests();
                summary.AddResults(calculatorTests.GetSummary());

                // Run integration tests
                var integrationTests = new AV1sIntegrationTests();
                integrationTests.RunAllTests();
                summary.AddResults(integrationTests.GetSummary());

                // Print final summary
                Console.WriteLine("\n" + "=".PadRight(50, '='));
                Console.WriteLine("FINAL TEST SUMMARY");
                Console.WriteLine("=".PadRight(50, '='));
                Console.WriteLine($"Total Tests: {summary.TotalTests}");
                Console.WriteLine($"Passed: {summary.PassedTests} ✅");
                Console.WriteLine($"Failed: {summary.FailedTests} ❌");
                Console.WriteLine($"Success Rate: {summary.SuccessRate:F1}%");
                Console.WriteLine($"Completed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                if (summary.FailedTests > 0)
                {
                    Console.WriteLine("\n❌ Some tests failed. Review the output above for details.");
                }
                else
                {
                    Console.WriteLine("\n✅ All tests passed successfully!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Test runner failed: {ex.Message}");
                summary.AddFailure($"Test runner exception: {ex.Message}");
            }

            return summary;
        }

        /// <summary>
        /// Runs only integration tests
        /// </summary>
        public static TestSummary RunIntegrationTests()
        {
            var integrationTests = new AV1sIntegrationTests();
            integrationTests.RunAllTests();
            return integrationTests.GetSummary();
        }

        /// <summary>
        /// Runs only unit tests
        /// </summary>
        public static TestSummary RunUnitTests()
        {
            var summary = new TestSummary();

            var domainTests = new TradingDayDataTests();
            domainTests.RunAllTests();
            summary.AddResults(domainTests.GetSummary());

            var priceLevelTests = new PriceLevelTests();
            priceLevelTests.RunAllTests();
            summary.AddResults(priceLevelTests.GetSummary());

            var calculationParametersTests = new CalculationParametersTests();
            calculationParametersTests.RunAllTests();
            summary.AddResults(calculationParametersTests.GetSummary());

            var calculatorTests = new PriceLevelCalculatorTests();
            calculatorTests.RunAllTests();
            summary.AddResults(calculatorTests.GetSummary());

            return summary;
        }
    }

    /// <summary>
    /// Test result summary
    /// </summary>
    public sealed class TestSummary
    {
        public int PassedTests { get; private set; }
        public int FailedTests { get; private set; }
        public int TotalTests => PassedTests + FailedTests;
        public double SuccessRate => TotalTests > 0 ? (PassedTests / (double)TotalTests) * 100 : 0;
        public List<string> FailureMessages { get; } = new List<string>();

        public void AddResults(TestSummary other)
        {
            PassedTests += other.PassedTests;
            FailedTests += other.FailedTests;
            FailureMessages.AddRange(other.FailureMessages);
        }

        public void AddPass()
        {
            PassedTests++;
        }

        public void AddFailure(string message)
        {
            FailedTests++;
            FailureMessages.Add(message);
        }

        public override string ToString()
        {
            return $"Tests: {TotalTests}, Passed: {PassedTests}, Failed: {FailedTests}, Success Rate: {SuccessRate:F1}%";
        }
    }

    /// <summary>
    /// Test utilities and helpers
    /// </summary>
    public static class TestUtilities
    {
        /// <summary>
        /// Creates sample trading day data for testing
        /// </summary>
        public static TradingDayData CreateSampleTradingDay(DateTime? date = null)
        {
            return new TradingDayData(
                date ?? DateTime.Today,
                110.0, // High
                90.0,  // Low
                100.0, // Open
                105.0  // Close
            );
        }

        /// <summary>
        /// Creates sample calculation parameters for testing
        /// </summary>
        public static CalculationParameters CreateSampleParameters()
        {
            return new CalculationParameters(
                105.0, // Base price
                false, // Use gap calculation
                NR2LevelType.PreviousDayClose
            );
        }

        /// <summary>
        /// Creates sample render parameters for testing
        /// </summary>
        public static RenderParameters CreateSampleRenderParameters()
        {
            return new RenderParameters(
                2,     // Line width
                10,    // Line buffer pixels
                true,  // Show dynamic labels
                50,    // Label offset X
                20     // Label vertical spacing
            );
        }

        /// <summary>
        /// Validates that a price level has reasonable values
        /// </summary>
        public static bool IsReasonablePriceLevel(PriceLevel level, double basePrice, double range)
        {
            if (level == null || !level.IsValid)
                return false;

            // Level should be within reasonable bounds of base price
            var maxDeviation = range * 2; // Allow up to 2x range deviation
            var deviation = Math.Abs(level.Value - basePrice);

            return deviation <= maxDeviation && level.Value > 0;
        }

        /// <summary>
        /// Measures execution time of an action
        /// </summary>
        public static TimeSpan MeasureExecutionTime(Action action)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            action?.Invoke();
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }
    }

    #endregion
}