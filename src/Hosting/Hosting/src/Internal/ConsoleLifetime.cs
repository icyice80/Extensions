// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Hosting.Internal
{
    /// <summary>
    /// Listens for Ctrl+C or SIGTERM and initiates shutdown.
    /// </summary>
    public class ConsoleLifetime : IHostLifetime, IDisposable
    {
        private readonly ManualResetEvent _shutdownBlock = new ManualResetEvent(false);
        private CancellationTokenRegistration _applicationStartedRegistration;

        public ConsoleLifetime(IOptions<ConsoleLifetimeOptions> options, IHostingEnvironment environment, IApplicationLifetime applicationLifetime)
            : this(options, environment, applicationLifetime, NullLoggerFactory.Instance) { }

        public ConsoleLifetime(IOptions<ConsoleLifetimeOptions> options, IHostingEnvironment environment, IApplicationLifetime applicationLifetime, ILoggerFactory loggerFactory)
        {
            Options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            ApplicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
            Logger = loggerFactory.CreateLogger("Microsoft.Extensions.Hosting.Host");
        }

        private ConsoleLifetimeOptions Options { get; }

        private IHostingEnvironment Environment { get; }

        private IApplicationLifetime ApplicationLifetime { get; }

        private ILogger Logger { get; }

        public Task WaitForStartAsync(CancellationToken cancellationToken)
        {
            if (!Options.SuppressStatusMessages)
            {
                _applicationStartedRegistration = ApplicationLifetime.ApplicationStarted.Register(state =>
                {
                    ((ConsoleLifetime)state).OnApplicationStarted();
                },
                this);
            }

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.CancelKeyPress += OnCancelKeyPress;

            // Console applications start immediately.
            return Task.CompletedTask;
        }

        private void OnApplicationStarted()
        {
            Logger.LogInformation("Application started. Press Ctrl+C to shut down.");
            Logger.LogInformation("Hosting environment: {envName}", Environment.EnvironmentName);
            Logger.LogInformation("Content root path: {contentRoot}", Environment.ContentRootPath);
        }

        private void OnProcessExit(object sender, EventArgs e)
        {
            ApplicationLifetime.StopApplication();
            _shutdownBlock.WaitOne();
        }

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            ApplicationLifetime.StopApplication();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // There's nothing to do here
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _shutdownBlock.Set();

            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            Console.CancelKeyPress -= OnCancelKeyPress;

            _applicationStartedRegistration.Dispose();
        }
    }
}
