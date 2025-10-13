
using System;
using System.Collections.Generic;

namespace NinjaTrader.NinjaScript.Indicators.RyF
{
    /// <summary>
    /// A helper class to manage a collection of IDisposable resources,
    /// ensuring they are all disposed of safely, even if exceptions occur during disposal.
    /// </summary>
    public sealed class DisposableResourceManager : IDisposable
    {
        private readonly List<IDisposable> resources = new List<IDisposable>();
        private readonly object lockObject = new object();
        private bool disposed = false;

        public T AddResource<T>(T resource) where T : IDisposable
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(DisposableResourceManager));

            lock (lockObject)
            {
                if (!disposed && resource != null)
                {
                    resources.Add(resource);
                }
            }
            return resource;
        }

        public void Dispose()
        {
            if (disposed) return;

            lock (lockObject)
            {
                if (disposed) return;
                disposed = true;

                // Dispose resources in reverse order of creation
                for (int i = resources.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        resources[i]?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        // Log but don't throw in dispose. Using Debug.WriteLine as a fallback logger.
                        System.Diagnostics.Debug.WriteLine($"Error disposing resource: {ex.Message}");
                    }
                }
                resources.Clear();
            }
        }
    }
}
