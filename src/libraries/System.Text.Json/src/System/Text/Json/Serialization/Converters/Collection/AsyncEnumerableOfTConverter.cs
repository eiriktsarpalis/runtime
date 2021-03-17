// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Json.Serialization.Converters
{
    internal class AsyncEnumeratorSerializationState
    {
        internal AsyncEnumeratorSerializationState(IAsyncDisposable enumerator)
        {
            Enumerator = enumerator;
        }

        internal IAsyncDisposable Enumerator { get; }
        internal ValueTask<bool> MoveNextTask { get; set; }
    }

    internal sealed class AsyncEnumerableOfTConverter<TAsyncEnumerable, TElement>
        : IEnumerableDefaultConverter<TAsyncEnumerable, TElement>
        where TAsyncEnumerable : IAsyncEnumerable<TElement>
    {
        protected override void Add(in TElement value, ref ReadStack state)
        {
            ((BufferedAsyncEnumerable)state.Current.ReturnValue!)._buffer.Add(value);
        }

        protected override void CreateCollection(ref Utf8JsonReader reader, ref ReadStack state, JsonSerializerOptions options)
        {
            if (!TypeToConvert.IsAssignableFrom(RuntimeType))
            {
                ThrowHelper.ThrowNotSupportedException_CannotPopulateCollection(TypeToConvert, ref reader, ref state);
            }

            state.Current.ReturnValue = new BufferedAsyncEnumerable();
        }

        [Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "Converter needs to consume ValueTask in a non-async context")]
        protected override bool OnWriteResume(Utf8JsonWriter writer, TAsyncEnumerable value, JsonSerializerOptions options, ref WriteStack state)
        {
            AsyncEnumeratorSerializationState? enumeratorState = state.Current.AsyncEnumeratorSerializationState;
            IAsyncEnumerator<TElement> enumerator;
            ValueTask<bool> moveNextTask;

            if (enumeratorState is null)
            {
                enumerator = value.GetAsyncEnumerator(state.CancellationToken);
                moveNextTask = enumerator.MoveNextAsync();
                enumeratorState = new AsyncEnumeratorSerializationState(enumerator);
            }
            else
            {
                Debug.Assert(enumeratorState.Enumerator is IAsyncEnumerator<TElement>);
                Debug.Assert(enumeratorState.MoveNextTask.IsCompleted);
                enumerator = (IAsyncEnumerator<TElement>)enumeratorState.Enumerator;
                moveNextTask = enumeratorState.MoveNextTask;
            }

            bool isCompleted;
            JsonConverter<TElement> converter = GetElementConverter(ref state);

            // iterate through the enumerator while elements are being returned synchronously
            for (; isCompleted = moveNextTask.IsCompleted; moveNextTask = enumerator.MoveNextAsync())
            {
                if (!moveNextTask.Result)
                {
                    return true;
                }

                if (ShouldFlush(writer, ref state))
                {
                    // avoid reading the task result twice when the converter resumes
                    moveNextTask = ValueTask.FromResult(true);
                    break;
                }

                TElement element = enumerator.Current;
                if (!converter.TryWrite(writer, element, options, ref state))
                {
                    // avoid reading the task result twice when the converter resumes
                    moveNextTask = ValueTask.FromResult(true);
                    break;
                }
            }

            // the current MoveNextAsync task is pending completion OR we need to flush the write buffer
            // persist the enumerator state to the current stack and return false
            enumeratorState.MoveNextTask = moveNextTask;
            state.StackContainsPendingTasks = !isCompleted;
            state.Current.AsyncEnumeratorSerializationState = enumeratorState;
            return false;
        }

        private class BufferedAsyncEnumerable : IAsyncEnumerable<TElement>
        {
            public readonly List<TElement> _buffer = new();

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            public async IAsyncEnumerator<TElement> GetAsyncEnumerator(CancellationToken _)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            {
                foreach (TElement element in _buffer)
                {
                    yield return element;
                }
            }
        }
    }
}
