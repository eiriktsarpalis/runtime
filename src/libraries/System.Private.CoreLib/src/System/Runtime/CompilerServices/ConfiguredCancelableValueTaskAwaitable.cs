// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

using Internal.Runtime.CompilerServices;

namespace System.Runtime.CompilerServices
{
    /// <summary>Provides an awaitable type that enables configured awaits on a <see cref="ValueTask"/>.</summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ConfiguredCancelableValueTaskAwaitable
    {
        ///// <summary>The wrapped <see cref="Task"/>.</summary>
        private readonly ValueTask _value;
        private readonly AwaitBehavior _awaitBehavior;

        /// <summary>Initializes the awaitable.</summary>
        /// <param name="value">The wrapped <see cref="ValueTask"/>.</param>
        /// <param name="awaitBehavior"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ConfiguredCancelableValueTaskAwaitable(in ValueTask value, AwaitBehavior awaitBehavior)
        {
            _value = value;
            _awaitBehavior = awaitBehavior;
        }

        /// <summary>Returns an awaiter for this <see cref="ConfiguredCancelableValueTaskAwaitable"/> instance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConfiguredCancelableValueTaskAwaiter GetAwaiter()
        {
            ValueTask cancelableTask = CancelableConfiguredValueTaskAwaitableHelpers.WithCancellation(_value, _awaitBehavior.CancellationToken, _awaitBehavior.MillisecondTimeout);
            return new ConfiguredCancelableValueTaskAwaiter(cancelableTask, _awaitBehavior.AwaitFlags);
        }

        /// <summary>Provides an awaiter for a <see cref="ConfiguredCancelableValueTaskAwaitable"/>.</summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct ConfiguredCancelableValueTaskAwaiter : ICriticalNotifyCompletion, IStateMachineBoxAwareAwaiter
        {
            /// <summary>The value being awaited.</summary>
            private readonly ValueTask _value;
            private readonly AwaitBehavior.Flags _awaitFlags;

            /// <summary>Initializes the awaiter.</summary>
            /// <param name="value">The value to be awaited.</param>
            /// <param name="awaitFlags"></param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ConfiguredCancelableValueTaskAwaiter(in ValueTask value, AwaitBehavior.Flags awaitFlags)
            {
                _value = value;
                _awaitFlags = awaitFlags;
            }

            /// <summary>Gets whether the <see cref="ConfiguredCancelableValueTaskAwaitable"/> has completed.</summary>
            public bool IsCompleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _value.IsCompleted && (_awaitFlags & AwaitBehavior.Flags.ForceAsync) != AwaitBehavior.Flags.ForceAsync;
            }

            /// <summary>Gets the result of the ValueTask.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GetResult()
            {
                object? obj = _value._obj;
                Debug.Assert(obj is null || obj is Task || obj is IValueTaskSource);

                if (obj != null)
                {
                    if (obj is Task t)
                    {
                        TaskAwaiter.ValidateEnd(t, _awaitFlags);
                    }
                    else
                    {
                        CancelableConfiguredValueTaskAwaitableHelpers.GetResult(Unsafe.As<IValueTaskSource>(obj), _value._token, _awaitFlags);
                    }
                }
            }

            /// <summary>Schedules the continuation action for the <see cref="ConfiguredCancelableValueTaskAwaitable"/>.</summary>
            public void OnCompleted(Action continuation)
            {
                object? obj = _value._obj;
                Debug.Assert(obj is null || obj is Task || obj is IValueTaskSource);

                bool continueOnCapturedContext = (_awaitFlags & AwaitBehavior.Flags.NoCapturedContext) != AwaitBehavior.Flags.NoCapturedContext;
                if (obj is Task t)
                {
                    t.ConfigureAwait(continueOnCapturedContext).GetAwaiter().OnCompleted(continuation);
                }
                else if (obj is not null)
                {
                    Unsafe.As<IValueTaskSource>(obj).OnCompleted(ValueTaskAwaiter.s_invokeActionDelegate, continuation, _value._token,
                        ValueTaskSourceOnCompletedFlags.FlowExecutionContext |
                            (continueOnCapturedContext ? ValueTaskSourceOnCompletedFlags.UseSchedulingContext : ValueTaskSourceOnCompletedFlags.None));
                }
                else
                {
                    Task.CompletedTask.ConfigureAwait(continueOnCapturedContext).GetAwaiter().OnCompleted(continuation);
                }
            }

            /// <summary>Schedules the continuation action for the <see cref="ConfiguredCancelableValueTaskAwaitable"/>.</summary>
            public void UnsafeOnCompleted(Action continuation)
            {
                object? obj = _value._obj;
                Debug.Assert(obj is null || obj is Task || obj is IValueTaskSource);

                bool continueOnCapturedContext = (_awaitFlags & AwaitBehavior.Flags.NoCapturedContext) != AwaitBehavior.Flags.NoCapturedContext;
                if (obj is Task t)
                {
                    t.ConfigureAwait(continueOnCapturedContext).GetAwaiter().UnsafeOnCompleted(continuation);
                }
                else if (obj is not null)
                {
                    Unsafe.As<IValueTaskSource>(obj).OnCompleted(ValueTaskAwaiter.s_invokeActionDelegate, continuation, _value._token,
                        continueOnCapturedContext ? ValueTaskSourceOnCompletedFlags.UseSchedulingContext : ValueTaskSourceOnCompletedFlags.None);
                }
                else
                {
                    Task.CompletedTask.ConfigureAwait(continueOnCapturedContext).GetAwaiter().UnsafeOnCompleted(continuation);
                }
            }

            void IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted(IAsyncStateMachineBox box)
            {
                object? obj = _value._obj;
                Debug.Assert(obj is null || obj is Task || obj is IValueTaskSource);

                bool continueOnCapturedContext = (_awaitFlags & AwaitBehavior.Flags.NoCapturedContext) != AwaitBehavior.Flags.NoCapturedContext;
                if (obj is Task t)
                {
                    TaskAwaiter.UnsafeOnCompletedInternal(t, box, continueOnCapturedContext);
                }
                else if (obj != null)
                {
                    Unsafe.As<IValueTaskSource>(obj).OnCompleted(ThreadPool.s_invokeAsyncStateMachineBox, box, _value._token,
                        continueOnCapturedContext ? ValueTaskSourceOnCompletedFlags.UseSchedulingContext : ValueTaskSourceOnCompletedFlags.None);
                }
                else
                {
                    TaskAwaiter.UnsafeOnCompletedInternal(Task.CompletedTask, box, continueOnCapturedContext);
                }
            }
        }
    }

    /// <summary>Provides an awaitable type that enables configured awaits on a <see cref="ValueTask{TResult}"/>.</summary>
    /// <typeparam name="TResult">The type of the result produced.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ConfiguredCancelableValueTaskAwaitable<TResult>
    {
        /// <summary>The wrapped <see cref="Task"/>.</summary>
        private readonly ValueTask<TResult> _value;
        private readonly AwaitBehavior _awaitBehavior;

        /// <summary>Initializes the awaitable.</summary>
        /// <param name="value">The wrapped <see cref="ValueTask"/>.</param>
        /// <param name="awaitBehavior"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ConfiguredCancelableValueTaskAwaitable(in ValueTask<TResult> value, AwaitBehavior awaitBehavior)
        {
            _value = value;
            _awaitBehavior = awaitBehavior;
        }

        /// <summary>Returns an awaiter for this <see cref="ConfiguredCancelableValueTaskAwaitable{TResult}"/> instance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConfiguredCancelableValueTaskAwaiter GetAwaiter()
        {
            ValueTask<TResult> cancelableTask = CancelableConfiguredValueTaskAwaitableHelpers.WithCancellation(_value, _awaitBehavior.CancellationToken, _awaitBehavior.MillisecondTimeout);
            return new ConfiguredCancelableValueTaskAwaiter(cancelableTask, _awaitBehavior.AwaitFlags);
        }

        /// <summary>Provides an awaiter for a <see cref="ConfiguredCancelableValueTaskAwaitable{TResult}"/>.</summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct ConfiguredCancelableValueTaskAwaiter : ICriticalNotifyCompletion, IStateMachineBoxAwareAwaiter
        {
            /// <summary>The value being awaited.</summary>
            private readonly ValueTask<TResult> _value;
            private readonly AwaitBehavior.Flags _awaitFlags;

            /// <summary>Initializes the awaiter.</summary>
            /// <param name="value">The value to be awaited.</param>
            /// <param name="awaitFlags"></param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ConfiguredCancelableValueTaskAwaiter(in ValueTask<TResult> value, AwaitBehavior.Flags awaitFlags)
            {
                _value = value;
                _awaitFlags = awaitFlags;
            }

            /// <summary>Gets whether the <see cref="ConfiguredCancelableValueTaskAwaitable{TResult}"/> has completed.</summary>
            public bool IsCompleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _value.IsCompleted && (_awaitFlags & AwaitBehavior.Flags.ForceAsync) != AwaitBehavior.Flags.ForceAsync;
            }

            /// <summary>Gets the result of the ValueTask.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public TResult GetResult()
            {
                object? obj = _value._obj;
                Debug.Assert(obj is null || obj is Task || obj is IValueTaskSource<TResult>);

                if (obj is null)
                {
                    return _value._result!;
                }

                if (obj is Task<TResult> t)
                {
                    TaskAwaiter.ValidateEnd(t, _awaitFlags);
                    return t.ResultOnSuccess;
                }

                return CancelableConfiguredValueTaskAwaitableHelpers.GetResult(Unsafe.As<IValueTaskSource<TResult>>(obj), _value._token, _awaitFlags);
            }

            /// <summary>Schedules the continuation action for the <see cref="ConfiguredCancelableValueTaskAwaitable{TResult}"/>.</summary>
            public void OnCompleted(Action continuation)
            {
                object? obj = _value._obj;
                Debug.Assert(obj is null || obj is Task<TResult> || obj is IValueTaskSource<TResult>);

                bool continueOnCapturedContext = (_awaitFlags & AwaitBehavior.Flags.NoCapturedContext) != AwaitBehavior.Flags.NoCapturedContext;
                if (obj is Task<TResult> t)
                {
                    t.ConfigureAwait(continueOnCapturedContext).GetAwaiter().OnCompleted(continuation);
                }
                else if (obj != null)
                {
                    Unsafe.As<IValueTaskSource<TResult>>(obj).OnCompleted(ValueTaskAwaiter.s_invokeActionDelegate, continuation, _value._token,
                        ValueTaskSourceOnCompletedFlags.FlowExecutionContext |
                            (continueOnCapturedContext ? ValueTaskSourceOnCompletedFlags.UseSchedulingContext : ValueTaskSourceOnCompletedFlags.None));
                }
                else
                {
                    Task.CompletedTask.ConfigureAwait(continueOnCapturedContext).GetAwaiter().OnCompleted(continuation);
                }
            }

            /// <summary>Schedules the continuation action for the <see cref="ConfiguredCancelableValueTaskAwaitable{TResult}"/>.</summary>
            public void UnsafeOnCompleted(Action continuation)
            {
                object? obj = _value._obj;
                Debug.Assert(obj is null || obj is Task<TResult> || obj is IValueTaskSource<TResult>);

                bool continueOnCapturedContext = (_awaitFlags & AwaitBehavior.Flags.NoCapturedContext) != AwaitBehavior.Flags.NoCapturedContext;
                if (obj is Task<TResult> t)
                {
                    t.ConfigureAwait(continueOnCapturedContext).GetAwaiter().UnsafeOnCompleted(continuation);
                }
                else if (obj != null)
                {
                    Unsafe.As<IValueTaskSource<TResult>>(obj).OnCompleted(ValueTaskAwaiter.s_invokeActionDelegate, continuation, _value._token,
                        continueOnCapturedContext ? ValueTaskSourceOnCompletedFlags.UseSchedulingContext : ValueTaskSourceOnCompletedFlags.None);
                }
                else
                {
                    Task.CompletedTask.ConfigureAwait(continueOnCapturedContext).GetAwaiter().UnsafeOnCompleted(continuation);
                }
            }

            void IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted(IAsyncStateMachineBox box)
            {
                object? obj = _value._obj;
                Debug.Assert(obj is null || obj is Task<TResult> || obj is IValueTaskSource<TResult>);

                bool continueOnCapturedContext = (_awaitFlags & AwaitBehavior.Flags.NoCapturedContext) != AwaitBehavior.Flags.NoCapturedContext;
                if (obj is Task<TResult> t)
                {
                    TaskAwaiter.UnsafeOnCompletedInternal(t, box, continueOnCapturedContext);
                }
                else if (obj != null)
                {
                    Unsafe.As<IValueTaskSource<TResult>>(obj).OnCompleted(ThreadPool.s_invokeAsyncStateMachineBox, box, _value._token,
                        continueOnCapturedContext ? ValueTaskSourceOnCompletedFlags.UseSchedulingContext : ValueTaskSourceOnCompletedFlags.None);
                }
                else
                {
                    TaskAwaiter.UnsafeOnCompletedInternal(Task.CompletedTask, box, continueOnCapturedContext);
                }
            }
        }
    }

    internal static class CancelableConfiguredValueTaskAwaitableHelpers
    {
        public static ValueTask WithCancellation(in ValueTask task, CancellationToken cancellationToken, uint millisecondsTimeout)
        {
            if (task.IsCompleted || !cancellationToken.CanBeCanceled && millisecondsTimeout == Timeout.UnsignedInfinite)
            {
                return task;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled(cancellationToken);
            }

            // _obj cannot be null since task.IsCompleted has evaluated to false
            object? obj = task._obj;
            Debug.Assert(obj is Task || obj is IValueTaskSource);

            if (obj is Task t)
            {
                return new ValueTask(Task.WithCancellation(t, cancellationToken, millisecondsTimeout));
            }
            else
            {
                IValueTaskSource vts = Unsafe.As<IValueTaskSource>(obj);
                IValueTaskSource cancellableVts = new CancelableValueTaskSource(vts, task._token, cancellationToken, millisecondsTimeout);
                return new ValueTask(cancellableVts, task._token);
            }
        }

        public static ValueTask<TResult> WithCancellation<TResult>(in ValueTask<TResult> task, CancellationToken cancellationToken, uint millisecondsTimeout)
        {
            if (task.IsCompleted || !cancellationToken.CanBeCanceled && millisecondsTimeout == Timeout.UnsignedInfinite)
            {
                return task;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<TResult>(cancellationToken);
            }

            // _obj cannot be null since task.IsCompleted has evaluated to false
            object? obj = task._obj;
            Debug.Assert(obj is Task<TResult> || obj is IValueTaskSource<TResult>);

            if (obj is Task<TResult> t)
            {
                return new ValueTask<TResult>(Task.WithCancellation(t, cancellationToken, millisecondsTimeout));
            }
            else
            {
                IValueTaskSource<TResult> vts = Unsafe.As<IValueTaskSource<TResult>>(obj);
                IValueTaskSource<TResult> cancellableVts = new CancelableValueTaskSource<TResult>(vts, task._token, cancellationToken, millisecondsTimeout);
                return new ValueTask<TResult>(cancellableVts, task._token);
            }
        }

        public static void GetResult(IValueTaskSource source, short token, AwaitBehavior.Flags awaitFlags)
        {
            if ((awaitFlags & AwaitBehavior.Flags.SuppressExceptions) == AwaitBehavior.Flags.SuppressExceptions)
            {
                switch (source.GetStatus(token))
                {
                    case ValueTaskSourceStatus.Succeeded:
                    case ValueTaskSourceStatus.Faulted:
                    case ValueTaskSourceStatus.Canceled:
                        return;

                    case ValueTaskSourceStatus.Pending:
                    default:
                        // Since we can't yet tell the outcome of the task, just call source.GetResult()
                        // and eat any exceptions it may yield. This is to honor both `SuppressExceptions`
                        // and any `IValueTaskSource` wait semantics. We don't mind exceptions being thrown
                        // since this branch would only be reached if awaiter.GetResult() is called without
                        // checking awaiter.IsCompleted beforehand: this can't happen in state machines.
                        try
                        {
                            source.GetResult(token);
                        }
                        catch
                        {

                        }
                        return;
                }
            }

            source.GetResult(token);
        }

        public static TResult GetResult<TResult>(IValueTaskSource<TResult> source, short token, AwaitBehavior.Flags awaitFlags)
        {
            if ((awaitFlags & AwaitBehavior.Flags.SuppressExceptions) == AwaitBehavior.Flags.SuppressExceptions)
            {
                switch (source.GetStatus(token))
                {
                    case ValueTaskSourceStatus.Succeeded:
                        return source.GetResult(token);
                    case ValueTaskSourceStatus.Faulted:
                    case ValueTaskSourceStatus.Canceled:
                        return default!;

                    case ValueTaskSourceStatus.Pending:
                    default:
                        // Since we can't yet tell the outcome of the task, just call source.GetResult()
                        // and eat any exceptions it may yield. This is to honor both `SuppressExceptions`
                        // and any `IValueTaskSource` wait semantics. We don't mind exceptions being thrown
                        // since this branch would only be reached if awaiter.GetResult() is called without
                        // checking awaiter.IsCompleted beforehand: this can't happen in state machines.
                        try
                        {
                            return source.GetResult(token);
                        }
                        catch
                        {
                            return default!;
                        }
                }
            }

            return source.GetResult(token);
        }

        private class CancelableValueTaskSource : Task.CancellationPromise, IValueTaskSource
        {
            private readonly IValueTaskSource _source;
            private readonly short _token;

            internal CancelableValueTaskSource(IValueTaskSource source, short token, CancellationToken cancellationToken, uint millisecondsTimeout)
                : base(cancellationToken, millisecondsTimeout)
            {
                Debug.Assert(source is not null);
                Debug.Assert(cancellationToken.CanBeCanceled || millisecondsTimeout != Timeout.UnsignedInfinite);

                _source = source;
                _token = token;

                _source.OnCompleted(Completed, this, _token, ValueTaskSourceOnCompletedFlags.None);

                static void Completed(object? state)
                {
                    CancelableValueTaskSource thisRef = (CancelableValueTaskSource)state!;
                    if (thisRef.TrySetResult())
                    {
                        thisRef.Cleanup();
                    }
                }
            }

            void IValueTaskSource.GetResult(short token)
            {
                Debug.Assert(token == _token);

                if (IsFaulted)
                {
                    // TODO: determine how to best throw this
                    throw Exception!;
                }

                _source.GetResult(_token);
            }

            ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
            {
                Debug.Assert(token == _token);

                switch (Status)
                {
                    case TaskStatus.Canceled: return ValueTaskSourceStatus.Canceled;
                    case TaskStatus.Faulted: return ValueTaskSourceStatus.Faulted;
                    default: return _source.GetStatus(_token);
                }
            }

            void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            {
                Debug.Assert(token == _token);

                bool continueOnCapturedContext = (flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) == ValueTaskSourceOnCompletedFlags.UseSchedulingContext;
                bool flowExecutionContext = (flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) == ValueTaskSourceOnCompletedFlags.FlowExecutionContext;
                // TODO avoid allocating an extra closure here
                TaskAwaiter.OnCompletedInternal(this, (() => continuation(state)), continueOnCapturedContext, flowExecutionContext);
            }
        }

        private class CancelableValueTaskSource<TResult> : Task.CancellationPromise<TResult>, IValueTaskSource<TResult>
        {
            private readonly IValueTaskSource<TResult> _source;
            private readonly short _token;

            internal CancelableValueTaskSource(IValueTaskSource<TResult> source, short token, CancellationToken cancellationToken, uint millisecondsTimeout)
                : base(cancellationToken, millisecondsTimeout)
            {
                Debug.Assert(source is not null);
                Debug.Assert(cancellationToken.CanBeCanceled || millisecondsTimeout != Timeout.UnsignedInfinite);

                _source = source;
                _token = token;

                _source.OnCompleted(Completed, this, _token, ValueTaskSourceOnCompletedFlags.None);

                static void Completed(object? state)
                {
                    CancelableValueTaskSource<TResult> thisRef = (CancelableValueTaskSource<TResult>)state!;
                    if (thisRef.TrySetResult())
                    {
                        thisRef.Cleanup();
                    }
                }
            }

            TResult IValueTaskSource<TResult>.GetResult(short token)
            {
                Debug.Assert(token == _token);

                if (IsFaulted)
                {
                    // TODO: determine how to best throw this
                    throw Exception!;
                }

                return _source.GetResult(_token);
            }

            ValueTaskSourceStatus IValueTaskSource<TResult>.GetStatus(short token)
            {
                Debug.Assert(token == _token);

                switch (Status)
                {
                    case TaskStatus.Canceled: return ValueTaskSourceStatus.Canceled;
                    case TaskStatus.Faulted: return ValueTaskSourceStatus.Faulted;
                    default: return _source.GetStatus(_token);
                }
            }

            void IValueTaskSource<TResult>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            {
                Debug.Assert(token == _token);

                bool continueOnCapturedContext = (flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) == ValueTaskSourceOnCompletedFlags.UseSchedulingContext;
                bool flowExecutionContext = (flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) == ValueTaskSourceOnCompletedFlags.FlowExecutionContext;
                // TODO avoid allocating an extra closure here
                TaskAwaiter.OnCompletedInternal(this, (() => continuation(state)), continueOnCapturedContext, flowExecutionContext);
            }
        }
    }
}
