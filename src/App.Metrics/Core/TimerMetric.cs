﻿// Copyright (c) Allan Hardy. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using App.Metrics.Concurrency;
using App.Metrics.Core.Interfaces;
using App.Metrics.Data;
using App.Metrics.ReservoirSampling;

// Originally Written by Iulian Margarintescu https://github.com/etishor/Metrics.NET and will retain the same license
// Ported/Refactored to .NET Standard Library by Allan Hardy
namespace App.Metrics.Core
{
    public sealed class TimerMetric : ITimerMetric, IDisposable
    {
        private readonly StripedLongAdder _activeSessionsCounter = new StripedLongAdder();
        private readonly IClock _clock;
        private readonly IHistogramMetric _histogram;
        private readonly IMeterMetric _meter;
        private readonly StripedLongAdder _totalRecordedTime = new StripedLongAdder();
        private bool _disposed;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TimerMetric" /> class.
        /// </summary>
        /// <param name="histogram">The histogram implementation to use.</param>
        /// <param name="clock">The clock to use to measure processing duration.</param>
        public TimerMetric(IHistogramMetric histogram, IClock clock)
            : this(histogram, new MeterMetric(clock), clock) { }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TimerMetric" /> class.
        /// </summary>
        /// <param name="reservoir">The reservoir implementation to use for sampling values to generate the histogram.</param>
        /// <param name="clock">The clock to use to measure processing duration.</param>
        public TimerMetric(Lazy<IReservoir> reservoir, IClock clock)
            : this(new HistogramMetric(reservoir), new MeterMetric(clock), clock) { }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TimerMetric" /> class.
        /// </summary>
        /// <param name="reservoir">The reservoir to use for sampling within the histogram.</param>
        /// <param name="meter">The meter implementation to use to genreate the rate of events over time.</param>
        /// <param name="clock">The clock to use to measure processing duration.</param>
        public TimerMetric(Lazy<IReservoir> reservoir, IMeterMetric meter, IClock clock)
            : this(new HistogramMetric(reservoir), meter, clock) { }

        /// <summary>
        ///     Initializes a new instance of the <see cref="TimerMetric" /> class.
        /// </summary>
        /// <param name="histogram">The histogram implementation to use.</param>
        /// <param name="meter">The meter implementation to use to genreate the rate of events over time.</param>
        /// <param name="clock">The clock to use to measure processing duration.</param>
        public TimerMetric(IHistogramMetric histogram, IMeterMetric meter, IClock clock)
        {
            _clock = clock;
            _meter = meter;
            _histogram = histogram;
        }

        ~TimerMetric() { Dispose(false); }

        /// <inheritdoc />
        public TimerValue Value => GetValue();

        /// <inheritdoc />
        public long CurrentTime() { return _clock.Nanoseconds; }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Free any other managed objects here.
                    _histogram?.Dispose();

                    _meter?.Dispose();
                }
            }

            _disposed = true;
        }

        /// <inheritdoc />
        public long EndRecording()
        {
            _activeSessionsCounter.Decrement();
            return _clock.Nanoseconds;
        }

        /// <inheritdoc />
        public TimerValue GetValue(bool resetMetric = false)
        {
            var total = resetMetric ? _totalRecordedTime.GetAndReset() : _totalRecordedTime.GetValue();
            return new TimerValue(
                _meter.GetValue(resetMetric),
                _histogram.GetValue(resetMetric),
                _activeSessionsCounter.GetValue(),
                total,
                TimeUnit.Nanoseconds);
        }

        /// <inheritdoc />
        public TimerContext NewContext(string userValue) { return new TimerContext(this, userValue); }

        /// <inheritdoc />
        public TimerContext NewContext() { return NewContext(null); }

        /// <inheritdoc />
        public void Record(long duration, TimeUnit unit, string userValue)
        {
            var nanos = unit.ToNanoseconds(duration);
            if (nanos < 0)
            {
                return;
            }

            _histogram.Update(nanos, userValue);
            _meter.Mark(userValue);
            _totalRecordedTime.Add(nanos);
        }

        /// <inheritdoc />
        public void Record(long time, TimeUnit unit) { Record(time, unit, null); }

        /// <inheritdoc />
        public void Reset()
        {
            _meter.Reset();
            _histogram.Reset();
        }

        /// <inheritdoc />
        public long StartRecording()
        {
            _activeSessionsCounter.Increment();
            return _clock.Nanoseconds;
        }

        /// <inheritdoc />
        public void Time(Action action, string userValue)
        {
            var start = _clock.Nanoseconds;
            try
            {
                _activeSessionsCounter.Increment();
                action();
            }
            finally
            {
                _activeSessionsCounter.Decrement();
                Record(_clock.Nanoseconds - start, TimeUnit.Nanoseconds, userValue);
            }
        }

        /// <inheritdoc />
        public T Time<T>(Func<T> action, string userValue)
        {
            var start = _clock.Nanoseconds;
            try
            {
                _activeSessionsCounter.Increment();
                return action();
            }
            finally
            {
                _activeSessionsCounter.Decrement();
                Record(_clock.Nanoseconds - start, TimeUnit.Nanoseconds, userValue);
            }
        }

        /// <inheritdoc />
        public void Time(Action action) { Time(action, null); }

        /// <inheritdoc />
        public T Time<T>(Func<T> action) { return Time(action, null); }
    }
}