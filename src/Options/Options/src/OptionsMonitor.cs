// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Extensions.Options
{
    /// <summary>
    /// Implementation of IOptionsMonitor.
    /// </summary>
    /// <typeparam name="TOptions"></typeparam>
    public class OptionsMonitor<TOptions> : IOptionsMonitor<TOptions>, IDisposable where TOptions : class, new()
    {
        private readonly IOptionsMonitorCache<TOptions> _cache;
        private readonly IOptionsFactory<TOptions> _factory;
        private readonly IEnumerable<IOptionsChangeTokenSource<TOptions>> _sources;
        private readonly List<IDisposable> _registrations = new List<IDisposable>();
        internal event Action<TOptions, string> _onChange;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="factory">The factory to use to create options.</param>
        /// <param name="sources">The sources used to listen for changes to the options instance.</param>
        /// <param name="cache">The cache used to store options.</param>
        public OptionsMonitor(IOptionsFactory<TOptions> factory, IEnumerable<IOptionsChangeTokenSource<TOptions>> sources, IOptionsMonitorCache<TOptions> cache)
        {
            _factory = factory;
            _sources = sources;
            _cache = cache;

            foreach (var source in _sources)
            {
                var registration = ChangeToken.OnChange(
                      () => source.GetChangeToken(),
                      (name) => InvokeChanged(name),
                      source.Name);

                _registrations.Add(registration);
            }
        }

        private void InvokeChanged(string name)
        {
            name = name ?? Options.DefaultName;
            _cache.TryRemove(name);
            var options = Get(name);
            if (_onChange != null)
            {
                _onChange.Invoke(options, name);
            }
        }

        /// <summary>
        /// The present value of the options.
        /// </summary>
        public TOptions CurrentValue
        {
            get => Get(Options.DefaultName);
        }

        /// <summary>
        /// Returns a configured TOptions instance with the given name.
        /// </summary>
        public virtual TOptions Get(string name)
        {
            name = name ?? Options.DefaultName;
            return _cache.GetOrAdd(name, () => _factory.Create(name));
        }

        /// <summary>
        /// Registers a listener to be called whenever TOptions changes.
        /// </summary>
        /// <param name="listener">The action to be invoked when TOptions has changed.</param>
        /// <returns>An IDisposable which should be disposed to stop listening for changes.</returns>
        public IDisposable OnChange(Action<TOptions, string> listener)
        {
            var disposable = new ChangeTrackerDisposable(this, listener);
            _onChange += disposable.OnChange;
            return disposable;
        }

        /// <summary>
        /// Removes all change registration subscriptions.
        /// </summary>
        public void Dispose()
        {
            // Remove all subscriptions to the change tokens
            foreach (var registration in _registrations)
            {
                registration.Dispose();
            }

            _registrations.Clear();
        }

        internal class ChangeTrackerDisposable : IDisposable
        {
            private readonly Action<TOptions, string> _listener;
            private readonly OptionsMonitor<TOptions> _monitor;

            public ChangeTrackerDisposable(OptionsMonitor<TOptions> monitor, Action<TOptions, string> listener)
            {
                _listener = listener;
                _monitor = monitor;
            }

            public void OnChange(TOptions options, string name) => _listener.Invoke(options, name);

            public void Dispose() => _monitor._onChange -= OnChange;
        }
    }
}
