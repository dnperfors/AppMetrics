﻿// Copyright (c) Allan Hardy. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using App.Metrics.Core.Interfaces;
using App.Metrics.Data;
using App.Metrics.Internal;
using App.Metrics.ReservoirSampling;

// Originally Written by Iulian Margarintescu https://github.com/etishor/Metrics.NET and will retain the same license
// Ported/Refactored to .NET Standard Library by Allan Hardy
namespace App.Metrics.Core
{
    public sealed class HistogramMetric : IHistogramMetric
    {
        private readonly Lazy<IReservoir> _reservoir;
        private bool _disposed;
        private UserValueWrapper _last;

        /// <summary>
        ///     Initializes a new instance of the <see cref="HistogramMetric" /> class.
        /// </summary>
        /// <param name="reservoir">The reservoir to use for sampling.</param>
        public HistogramMetric(Lazy<IReservoir> reservoir)
        {
            if (reservoir == null)
            {
                throw new ArgumentNullException(nameof(reservoir));
            }

            _reservoir = reservoir;
        }

        [AppMetricsExcludeFromCodeCoverage]
        ~HistogramMetric() { Dispose(false); }

        public HistogramValue Value => GetValue();

        [AppMetricsExcludeFromCodeCoverage]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        [AppMetricsExcludeFromCodeCoverage]
        public void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Free any other managed objects here.
                }
            }

            _disposed = true;
        }

        /// <inheritdoc />
        public HistogramValue GetValue(bool resetMetric = false)
        {
            var value = new HistogramValue(_last.Value, _last.UserValue, _reservoir.Value.GetSnapshot(resetMetric));

            if (resetMetric)
            {
                _last = UserValueWrapper.Empty;
            }

            return value;
        }

        /// <inheritdoc />
        public void Reset()
        {
            _last = UserValueWrapper.Empty;
            _reservoir.Value.Reset();
        }

        /// <inheritdoc />
        public void Update(long value, string userValue)
        {
            _last = new UserValueWrapper(value, userValue);
            _reservoir.Value.Update(value, userValue);
        }

        /// <inheritdoc />
        public void Update(long value)
        {
            _last = new UserValueWrapper(value);
            _reservoir.Value.Update(value);
        }
    }
}