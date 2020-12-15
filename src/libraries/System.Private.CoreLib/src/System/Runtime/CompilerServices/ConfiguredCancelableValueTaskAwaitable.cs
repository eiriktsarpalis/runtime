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
        /// <summary>The wrapped <see cref="Task"/>.</summary>
        private readonly ConfiguredCancelableValueTaskAwaiter _awaiter;

        /// <summary>Initializes the awaitable.</summary>
        /// <param name="value">The wrapped <see cref="ValueTask"/>.</param>
        /// <param name="awaitBehavior"></param>
        /// <param name="cancellationToken"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ConfiguredCancelableValueTaskAwaitable(in ValueTask value, AwaitBehavior awaitBehavior, CancellationToken cancellationToken)
        {
            _awaiter = new ConfiguredCancelableValueTaskAwaiter(in value, awaitBehavior, cancellationToken);
        }

        /// <summary>Returns an awaiter for this <see cref="ConfiguredCancelableValueTaskAwaitable"/> instance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConfiguredCancelableValueTaskAwaiter GetAwaiter() => _awaiter;

        /// <summary>Provides an awaiter for a <see cref="ConfiguredCancelableValueTaskAwaitable"/>.</summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct ConfiguredCancelableValueTaskAwaiter : ICriticalNotifyCompletion, IStateMachineBoxAwareAwaiter
        {
            /// <summary>The value being awaited.</summary>
            private readonly ValueTask _value;
            private readonly AwaitBehavior _awaitBehavior;
            private readonly CancellationToken _cancellationToken;

            /// <summary>Initializes the awaiter.</summary>
            /// <param name="value">The value to be awaited.</param>
            /// <param name="awaitBehavior"></param>
            /// <param name="cancellationToken"></param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ConfiguredCancelableValueTaskAwaiter(in ValueTask value, AwaitBehavior awaitBehavior, CancellationToken cancellationToken)
            {
                _value = value;
                _awaitBehavior = awaitBehavior;
                _cancellationToken = cancellationToken;
            }

            /// <summary>Gets whether the <see cref="ConfiguredCancelableValueTaskAwaitable"/> has completed.</summary>
            public bool IsCompleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get =>
                    (_value.IsCompleted || _cancellationToken.IsCancellationRequested) &&
                    (_awaitBehavior & AwaitBehavior.ForceAsync) != AwaitBehavior.ForceAsync;
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
                        TaskAwaiter.ValidateEnd(t, _awaitBehavior, _cancellationToken);
                    }
                    else
                    {
                        CancelableConfiguredValueTaskAwaitableHelpers.GetResult(Unsafe.As<IValueTaskSource>(obj), _value._token, _awaitBehavior, _cancellationToken);
                    }
                }
            }

            /// <summary>Schedules the continuation action for the <see cref="ConfiguredCancelableValueTaskAwaitable"/>.</summary>
            public void OnCompleted(Action continuation)
            {
                object? obj = _value._obj;
                Debug.Assert(obj is null || obj is Task || obj is IValueTaskSource);

                if (obj is Task t)
                {
                    t.ConfigureAwait(_awaitBehavior, _cancellationToken).GetAwaiter().OnCompleted(continuation);
                }
                else if (obj != null)
                {
                    IValueTaskSource vts = Unsafe.As<IValueTaskSource>(obj);
                    CancelableConfiguredValueTaskAwaitableHelpers.SubscribeContinuation(vts, ValueTaskAwaiter.s_invokeActionDelegate, continuation, _value._token, _awaitBehavior, _cancellationToken, flowExecutionContext: true);
                }
                else
                {
                    Task.CompletedTask.ConfigureAwait(_awaitBehavior, _cancellationToken).GetAwaiter().OnCompleted(continuation);
                }
            }

            /// <summary>Schedules the continuation action for the <see cref="ConfiguredCancelableValueTaskAwaitable"/>.</summary>
            public void UnsafeOnCompleted(Action continuation)
            {
                object? obj = _value._obj;
                Debug.Assert(obj is null || obj is Task || obj is IValueTaskSource);

                if (obj is Task t)
                {
                    t.ConfigureAwait(_awaitBehavior, _cancellationToken).GetAwaiter().UnsafeOnCompleted(continuation);
                }
                else if (obj is not null)
                {
                    IValueTaskSource vts = Unsafe.As<IValueTaskSource>(obj);
                    CancelableConfiguredValueTaskAwaitableHelpers.SubscribeContinuation(vts, ValueTaskAwaiter.s_invokeActionDelegate, continuation, _value._token, _awaitBehavior, _cancellationToken, flowExecutionContext: false);
                }
                else
                {
                    Task.CompletedTask.ConfigureAwait(_awaitBehavior, _cancellationToken).GetAwaiter().UnsafeOnCompleted(continuation);
                }
            }

            void IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted(IAsyncStateMachineBox box)
            {
                object? obj = _value._obj;
                Debug.Assert(obj is null || obj is Task || obj is IValueTaskSource);

                if (obj is Task t)
                {
                    bool continueOnCapturedContext = (_awaitBehavior & AwaitBehavior.NoCapturedContext) != AwaitBehavior.NoCapturedContext;
                    TaskAwaiter.UnsafeOnCompletedInternal(Task.WithCancellation(t, _cancellationToken), box, continueOnCapturedContext);
                }
                else if (obj is not null)
                {
                    IValueTaskSource vts = Unsafe.As<IValueTaskSource>(obj);
                    CancelableConfiguredValueTaskAwaitableHelpers.SubscribeContinuation(vts, ThreadPool.s_invokeAsyncStateMachineBox, box, _value._token, _awaitBehavior, _cancellationToken, flowExecutionContext: false);
                }
                else
                {
                    bool continueOnCapturedContext = (_awaitBehavior & AwaitBehavior.NoCapturedContext) != AwaitBehavior.NoCapturedContext;
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
        private readonly ConfiguredCancelableValueTaskAwaiter _awaiter;

        /// <summary>Initializes the awaitable.</summary>
        /// <param name="value">The wrapped <see cref="ValueTask"/>.</param>
        /// <param name="awaitBehavior"></param>
        /// <param name="cancellationToken"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ConfiguredCancelableValueTaskAwaitable(in ValueTask<TResult> value, AwaitBehavior awaitBehavior, CancellationToken cancellationToken)
        {
            _awaiter = new ConfiguredCancelableValueTaskAwaiter(in value, awaitBehavior, cancellationToken);
        }

        /// <summary>Returns an awaiter for this <see cref="ConfiguredCancelableValueTaskAwaitable{TResult}"/> instance.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConfiguredCancelableValueTaskAwaiter GetAwaiter() => _awaiter;

        /// <summary>Provides an awaiter for a <see cref="ConfiguredCancelableValueTaskAwaitable{TResult}"/>.</summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct ConfiguredCancelableValueTaskAwaiter : ICriticalNotifyCompletion, IStateMachineBoxAwareAwaiter
        {
            /// <summary>The value being awaited.</summary>
            private readonly ValueTask<TResult> _value;
            private readonly AwaitBehavior _awaitBehavior;
            private readonly CancellationToken _cancellationToken;

            /// <summary>Initializes the awaiter.</summary>
            /// <param name="value">The value to be awaited.</param>
            /// <param name="awaitBehavior"></param>
            /// <param name="cancellationToken"></param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ConfiguredCancelableValueTaskAwaiter(in ValueTask<TResult> value, AwaitBehavior awaitBehavior, CancellationToken cancellationToken)
            {
                _value = value;
                _awaitBehavior = awaitBehavior;
                _cancellationToken = cancellationToken;
            }

            /// <summary>Gets whether the <see cref="ConfiguredCancelableValueTaskAwaitable{TResult}"/> has completed.</summary>
            public bool IsCompleted
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get =>
                    (_value.IsCompleted || _cancellationToken.IsCancellationRequested) &&
                    (_awaitBehavior & AwaitBehavior.ForceAsync) != AwaitBehavior.ForceAsync;
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
                    TaskAwaiter.ValidateEnd(t, _awaitBehavior, _cancellationToken);
                    return t.ResultOnSuccess;
                }

                return CancelableConfiguredValueTaskAwaitableHelpers.GetResult(Unsafe.As<IValueTaskSource<TResult>>(obj), _value._token, _awaitBehavior, _cancellationToken);
            }

            /// <summary>Schedules the continuation action for the <see cref="ConfiguredCancelableValueTaskAwaitable{TResult}"/>.</summary>
            public void OnCompleted(Action continuation)
            {
                object? obj = _value._obj;
                Debug.Assert(obj is null || obj is Task<TResult> || obj is IValueTaskSource<TResult>);

                if (obj is Task<TResult> t)
                {
                    t.ConfigureAwait(_awaitBehavior, _cancellationToken).GetAwaiter().OnCompleted(continuation);
                }
                else if (obj != null)
                {
                    IValueTaskSource<TResult> vts = Unsafe.As<IValueTaskSource<TResult>>(obj);
                    CancelableConfiguredValueTaskAwaitableHelpers.SubscribeContinuation(vts, ValueTaskAwaiter.s_invokeActionDelegate, continuation, _value._token, _awaitBehavior, _cancellationToken, flowExecutionContext: true);
                }
                else
                {
                    Task.CompletedTask.ConfigureAwait(_awaitBehavior, _cancellationToken).GetAwaiter().OnCompleted(continuation);
                }
            }

            /// <summary>Schedules the continuation action for the <see cref="ConfiguredCancelableValueTaskAwaitable{TResult}"/>.</summary>
            public void UnsafeOnCompleted(Action continuation)
            {
                object? obj = _value._obj;
                Debug.Assert(obj is null || obj is Task<TResult> || obj is IValueTaskSource<TResult>);

                if (obj is Task<TResult> t)
                {
                    t.ConfigureAwait(_awaitBehavior, _cancellationToken).GetAwaiter().UnsafeOnCompleted(continuation);
                }
                else if (obj != null)
                {
                    IValueTaskSource<TResult> vts = Unsafe.As<IValueTaskSource<TResult>>(obj);
                    CancelableConfiguredValueTaskAwaitableHelpers.SubscribeContinuation(vts, ValueTaskAwaiter.s_invokeActionDelegate, continuation, _value._token, _awaitBehavior, _cancellationToken, flowExecutionContext: false);
                }
                else
                {
                    Task.CompletedTask.ConfigureAwait(_awaitBehavior, _cancellationToken).GetAwaiter().UnsafeOnCompleted(continuation);
                }
            }

            void IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted(IAsyncStateMachineBox box)
            {
                object? obj = _value._obj;
                Debug.Assert(obj is null || obj is Task<TResult> || obj is IValueTaskSource<TResult>);

                if (obj is Task<TResult> t)
                {
                    bool continueOnCapturedContext = (_awaitBehavior & AwaitBehavior.NoCapturedContext) != AwaitBehavior.NoCapturedContext;
                    TaskAwaiter.UnsafeOnCompletedInternal(Task.WithCancellation(t, _cancellationToken), box, continueOnCapturedContext);
                }
                else if (obj != null)
                {
                    IValueTaskSource<TResult> vts = Unsafe.As<IValueTaskSource<TResult>>(obj);
                    CancelableConfiguredValueTaskAwaitableHelpers.SubscribeContinuation(vts, ThreadPool.s_invokeAsyncStateMachineBox, box, _value._token, _awaitBehavior, _cancellationToken, flowExecutionContext: false);
                }
                else
                {
                    bool continueOnCapturedContext = (_awaitBehavior & AwaitBehavior.NoCapturedContext) != AwaitBehavior.NoCapturedContext;
                    TaskAwaiter.UnsafeOnCompletedInternal(Task.CompletedTask, box, continueOnCapturedContext);
                }
            }
        }
    }

    internal static class CancelableConfiguredValueTaskAwaitableHelpers
    {
        public static void GetResult(IValueTaskSource source, short token, AwaitBehavior awaitBehavior, CancellationToken cancellationToken)
        {
            if ((awaitBehavior & AwaitBehavior.NoThrow) == AwaitBehavior.NoThrow)
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
                        // and eat any exceptions it may yield. This is to honor both `NoThrow` and the
                        // `IValueTaskSource` wait semantics. We don't mind exceptions being thrown here
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

            if (cancellationToken.IsCancellationRequested && source.GetStatus(token) == ValueTaskSourceStatus.Pending)
            {
                throw new OperationCanceledException(SR.OperationCanceled, cancellationToken);
            }

            source.GetResult(token);
        }

        public static TResult GetResult<TResult>(IValueTaskSource<TResult> source, short token, AwaitBehavior awaitBehavior, CancellationToken cancellationToken)
        {
            if ((awaitBehavior & AwaitBehavior.NoThrow) == AwaitBehavior.NoThrow)
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
                        // and eat any exceptions it may yield. This is to honor both `NoThrow` and the
                        // `IValueTaskSource` wait semantics. We don't mind exceptions being thrown here
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

            if (cancellationToken.IsCancellationRequested && source.GetStatus(token) == ValueTaskSourceStatus.Pending)
            {
                throw new OperationCanceledException(SR.OperationCanceled, cancellationToken);
            }

            return source.GetResult(token);
        }

        public static void SubscribeContinuation(IValueTaskSource source, Action<object?> continuation, object? state, short token, AwaitBehavior awaitBehavior, CancellationToken cancellationToken, bool flowExecutionContext)
        {
            if (cancellationToken.CanBeCanceled)
            {
                Task subscribedTask = CreateSubscribedTask(source, token, cancellationToken);
                bool continueOnCapturedContext = (awaitBehavior & AwaitBehavior.NoCapturedContext) != AwaitBehavior.NoCapturedContext;
                // need to allocate one more delegate to convert Action<object?> to Action
                TaskAwaiter.OnCompletedInternal(subscribedTask, (() => continuation(state)), continueOnCapturedContext, flowExecutionContext);
            }
            else
            {
                ValueTaskSourceOnCompletedFlags vtsFlags = flowExecutionContext ? ValueTaskSourceOnCompletedFlags.FlowExecutionContext : ValueTaskSourceOnCompletedFlags.None;
                if ((awaitBehavior & AwaitBehavior.NoCapturedContext) != AwaitBehavior.NoCapturedContext)
                {
                    vtsFlags |= ValueTaskSourceOnCompletedFlags.UseSchedulingContext;
                }

                source.OnCompleted(continuation, state, token, vtsFlags);
            }

            static Task CreateSubscribedTask(IValueTaskSource source, short token, CancellationToken cancellationToken)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.CompletedTask; // do we need Task.FromCanceled here?
                }

                var tcs = new CancelableTaskCompletionSource(cancellationToken);
                source.OnCompleted(CompleteTcs, tcs, token, ValueTaskSourceOnCompletedFlags.None);
                return tcs.Task;

                static void CompleteTcs(object? state)
                {
                    var tcs = (CancelableTaskCompletionSource)state!;
                    if (tcs.TrySetResult())
                    {
                        tcs.Dispose();
                    }
                }
            }
        }

        public static void SubscribeContinuation<TResult>(IValueTaskSource<TResult> source, Action<object?> continuation, object? state, short token, AwaitBehavior awaitBehavior, CancellationToken cancellationToken, bool flowExecutionContext)
        {
            if (cancellationToken.CanBeCanceled)
            {
                Task subscribedTask = CreateSubscribedTask(source, token, cancellationToken);
                bool continueOnCapturedContext = (awaitBehavior & AwaitBehavior.NoCapturedContext) != AwaitBehavior.NoCapturedContext;
                // need to allocate one more delegate to convert Action<object?> to Action
                TaskAwaiter.OnCompletedInternal(subscribedTask, (() => continuation(state)), continueOnCapturedContext, flowExecutionContext);
            }
            else
            {
                ValueTaskSourceOnCompletedFlags vtsFlags = flowExecutionContext ? ValueTaskSourceOnCompletedFlags.FlowExecutionContext : ValueTaskSourceOnCompletedFlags.None;
                if ((awaitBehavior & AwaitBehavior.NoCapturedContext) != AwaitBehavior.NoCapturedContext)
                {
                    vtsFlags |= ValueTaskSourceOnCompletedFlags.UseSchedulingContext;
                }

                source.OnCompleted(continuation, state, token, vtsFlags);
            }

            static Task CreateSubscribedTask(IValueTaskSource<TResult> source, short token, CancellationToken cancellationToken)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.CompletedTask; // do we need Task.FromCanceled here?
                }

                var tcs = new CancelableTaskCompletionSource(cancellationToken);
                source.OnCompleted(CompleteTcs, tcs, token, ValueTaskSourceOnCompletedFlags.None);
                return tcs.Task;

                static void CompleteTcs(object? state)
                {
                    var tcs = (CancelableTaskCompletionSource)state!;
                    if (tcs.TrySetResult())
                    {
                        tcs.Dispose();
                    }
                }
            }
        }

        private class CancelableTaskCompletionSource : TaskCompletionSource, IDisposable
        {
            private readonly CancellationTokenRegistration _registration;

            public CancelableTaskCompletionSource(CancellationToken cancellationToken)
            {
                _registration = cancellationToken.UnsafeRegister(static state =>
                {
                    var self = (CancelableTaskCompletionSource)state!;
                    if (self.TrySetCanceled())
                    {
                        self.Dispose();
                    }
                }, this);
            }

            public void Dispose() => _registration.Dispose();
        }
    }
}
