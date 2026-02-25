/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Unity-MCP)    │
│  Copyright (c) 2025 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/

#nullable enable

using System;
using System.Threading;
using R3;

namespace com.IvanMurzak.Unity.MCP
{
    using LogLevel = Runtime.Utils.LogLevel;

    public partial class UnityMcpPlugin : IDisposable
    {
        public const string Version = "0.48.1";

        private static int _singletonCount = 0;
        public static bool HasAnyInstance => _singletonCount > 0;
        protected static void IncrementSingletonCount() => Interlocked.Increment(ref _singletonCount);
        protected static void DecrementSingletonCount() => Interlocked.Decrement(ref _singletonCount);

        private static LogLevel _configuredLogLevel = LogLevel.Warning;
        // Uses direct enum comparison: configured threshold <= requested level means enabled
        public static bool IsLogEnabled(LogLevel level) => _configuredLogLevel <= level;
        public static void ApplyLogLevel(LogLevel level) => _configuredLogLevel = level;

        protected readonly CompositeDisposable _disposables = new();

        public UnityLogCollector? LogCollector { get; protected set; } = null;

        protected UnityMcpPlugin() { }

        public void AddUnityLogCollector(ILogStorage logStorage)
        {
            if (logStorage == null)
                throw new ArgumentNullException(nameof(logStorage));

            if (LogCollector != null)
                throw new InvalidOperationException($"{nameof(UnityLogCollector)} is already added.");

            LogCollector = new UnityLogCollector(logStorage);
            _disposables.Add(LogCollector);
        }

        public void AddUnityLogCollectorIfNeeded(Func<ILogStorage> logStorageProvider)
        {
            if (LogCollector != null)
                return;

            AddUnityLogCollector(logStorageProvider());
        }

        public void DisposeLogCollector()
        {
            LogCollector?.Save();
            LogCollector?.Dispose();
            LogCollector = null;
        }

        public virtual void Dispose()
        {
            _disposables.Dispose();
            // LogCollector is disposed by _disposables
            LogCollector = null;
        }

        /// <summary>
        /// Generates a cryptographically random URL-safe token.
        /// Used by <see cref="UnityConnectionConfig.SetDefault"/> for initial token generation.
        /// </summary>
        public static string GenerateToken()
        {
            var bytes = new byte[32];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Generate a deterministic TCP port based on current directory.
        /// Uses SHA256 hash for better distribution and less collisions.
        /// Port range: 50000-59999 (less commonly used dynamic ports).
        /// </summary>
        public static int GeneratePortFromDirectory()
        {
            const int MinPort = 50000; // Higher range to avoid common dynamic ports
            const int MaxPort = 59999;
            const int PortRange = MaxPort - MinPort + 1;

            var currentDir = System.Environment.CurrentDirectory.ToLowerInvariant();

            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(currentDir));

                // Use first 4 bytes to create an integer
                var hash = System.BitConverter.ToInt32(hashBytes, 0);

                // Map to port range
                var port = MinPort + (System.Math.Abs(hash) % PortRange);

                return port;
            }
        }
    }
}
