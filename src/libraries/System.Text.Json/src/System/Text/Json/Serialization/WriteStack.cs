// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Json
{
    [DebuggerDisplay("Path:{PropertyPath()} Current: ClassType.{Current.JsonClassInfo.ClassType}, {Current.JsonClassInfo.Type.Name}")]
    internal struct WriteStack
    {
        /// <summary>
        /// The number of stack frames when the continuation started.
        /// </summary>
        private int _continuationCount;

        /// <summary>
        /// The number of stack frames including Current. _previous will contain _count-1 higher frames.
        /// </summary>
        private int _count;

        public bool StackContainsPendingTasks;
        public CancellationToken CancellationToken;
        public List<IAsyncDisposable>? PendingAsyncDisposables;

        private List<WriteStackFrame> _previous;

        // A field is used instead of a property to avoid value semantics.
        public WriteStackFrame Current;

        /// <summary>
        /// The amount of bytes to write before the underlying Stream should be flushed and the
        /// current buffer adjusted to remove the processed bytes.
        /// </summary>
        public int FlushThreshold;

        public bool IsContinuation => _continuationCount != 0;

        // The bag of preservable references.
        public ReferenceResolver ReferenceResolver;

        /// <summary>
        /// Internal flag to let us know that we need to read ahead in the inner read loop.
        /// </summary>
        public bool SupportContinuation;

        private void AddCurrent()
        {
            if (_previous == null)
            {
                _previous = new List<WriteStackFrame>();
            }

            if (_count > _previous.Count)
            {
                // Need to allocate a new array element.
                _previous.Add(Current);
            }
            else
            {
                // Use a previously allocated slot.
                _previous[_count - 1] = Current;
            }

            _count++;
        }

        /// <summary>
        /// Initialize the state without delayed initialization of the JsonClassInfo.
        /// </summary>
        public JsonConverter Initialize(Type type, JsonSerializerOptions options, bool supportContinuation)
        {
            JsonClassInfo jsonClassInfo = options.GetOrAddClassForRootType(type);

            Current.JsonClassInfo = jsonClassInfo;
            Current.DeclaredJsonPropertyInfo = jsonClassInfo.PropertyInfoForClassInfo;
            Current.NumberHandling = Current.DeclaredJsonPropertyInfo.NumberHandling;

            if (options.ReferenceHandlingStrategy != ReferenceHandlingStrategy.None)
            {
                Debug.Assert(options.ReferenceHandler != null);
                ReferenceResolver = options.ReferenceHandler.CreateResolver(writing: true);
            }

            SupportContinuation = supportContinuation;

            return jsonClassInfo.PropertyInfoForClassInfo.ConverterBase;
        }

        public void Push()
        {
            if (_continuationCount == 0)
            {
                if (_count == 0)
                {
                    // The first stack frame is held in Current.
                    _count = 1;
                }
                else
                {
                    JsonClassInfo jsonClassInfo = Current.GetPolymorphicJsonPropertyInfo().RuntimeClassInfo;
                    JsonNumberHandling? numberHandling = Current.NumberHandling;

                    AddCurrent();
                    Current.Reset();

                    Current.JsonClassInfo = jsonClassInfo;
                    Current.DeclaredJsonPropertyInfo = jsonClassInfo.PropertyInfoForClassInfo;
                    // Allow number handling on property to win over handling on type.
                    Current.NumberHandling = numberHandling ?? Current.DeclaredJsonPropertyInfo.NumberHandling;
                }
            }
            else if (_continuationCount == 1)
            {
                // No need for a push since there is only one stack frame.
                Debug.Assert(_count == 1);
                _continuationCount = 0;
            }
            else
            {
                // A continuation, adjust the index.
                Current = _previous[_count - 1];

                // Check if we are done.
                if (_count == _continuationCount)
                {
                    _continuationCount = 0;
                }
                else
                {
                    _count++;
                }
            }
        }

        public void Pop(bool success)
        {
            Debug.Assert(_count > 0);

            if (!success)
            {
                // Check if we need to initialize the continuation.
                if (_continuationCount == 0)
                {
                    if (_count == 1)
                    {
                        // No need for a continuation since there is only one stack frame.
                        _continuationCount = 1;
                        _count = 1;
                    }
                    else
                    {
                        AddCurrent();
                        _count--;
                        _continuationCount = _count;
                        _count--;
                        Current = _previous[_count - 1];
                    }

                    return;
                }

                if (_continuationCount == 1)
                {
                    // No need for a pop since there is only one stack frame.
                    Debug.Assert(_count == 1);
                    return;
                }

                // Update the list entry to the current value.
                _previous[_count - 1] = Current;

                Debug.Assert(_count > 0);
            }
            else
            {
                Debug.Assert(_continuationCount == 0);

                if (Current.AsyncEnumeratorSerializationState is AsyncEnumeratorSerializationState enumeratorState)
                {
                    // we have completed serialization of an AsyncEnumerator,
                    // pop from the stack and schedule for async disposal.
                    PendingAsyncDisposables ??= new();
                    PendingAsyncDisposables.Add(enumeratorState.Enumerator);
                }
            }

            if (_count > 1)
            {
                Current = _previous[--_count - 1];
            }
        }

        // Walk the stack, asynchronously awaiting any pending stacks that resumable converters depend on.
        public async ValueTask AwaitPendingStackTasks()
        {
            Debug.Assert(StackContainsPendingTasks);

            // TODO this is clearly wrong, we should only be enumerating continuation frames (really just the innermost continuation frame)
            foreach (WriteStackFrame frame in _previous)
            {
                if (frame.AsyncEnumeratorSerializationState is { MoveNextTask: var moveNextTask })
                {
                    // asynchronously await the task and cache the result for consumption by the converter
                    try
                    {
                        bool result = await moveNextTask.ConfigureAwait(false);
                        moveNextTask = ValueTask.FromResult(result);
                    }
                    catch (Exception e)
                    {
                        // TODO: use ExceptionDispatchInfo
                        moveNextTask = new ValueTask<bool>(Task.FromException<bool>(e));
                    }

                    // NB AsyncEnumeratorSerializationState is a class
                    frame.AsyncEnumeratorSerializationState.MoveNextTask = moveNextTask;
                }
            }
        }

        // Asynchronously dispose of any AsyncDisposables that have been scheduled for disposal
        public async ValueTask DisposePendingAsyncDisposables()
        {
            Debug.Assert(PendingAsyncDisposables?.Count > 0);
            List<Exception>? exceptions = null;

            foreach (IAsyncDisposable asyncDisposable in PendingAsyncDisposables)
            {
                try
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exn)
                {
                    exceptions ??= new();
                    exceptions.Add(exn);
                }
            }

            if (exceptions is not null)
            {
                throw new AggregateException(exceptions);
            }

            PendingAsyncDisposables.Clear();
        }

        // Return a property path as a simple JSONPath using dot-notation when possible. When special characters are present, bracket-notation is used:
        // $.x.y.z
        // $['PropertyName.With.Special.Chars']
        public string PropertyPath()
        {
            StringBuilder sb = new StringBuilder("$");

            // If a continuation, always report back full stack.
            int count = Math.Max(_count, _continuationCount);

            for (int i = 0; i < count - 1; i++)
            {
                AppendStackFrame(sb, _previous[i]);
            }

            if (_continuationCount == 0)
            {
                AppendStackFrame(sb, Current);
            }

            return sb.ToString();

            void AppendStackFrame(StringBuilder sb, in WriteStackFrame frame)
            {
                // Append the property name.
                string? propertyName = frame.DeclaredJsonPropertyInfo?.MemberInfo?.Name;
                if (propertyName == null)
                {
                    // Attempt to get the JSON property name from the property name specified in re-entry.
                    propertyName = frame.JsonPropertyNameAsString;
                }

                AppendPropertyName(sb, propertyName);
            }

            void AppendPropertyName(StringBuilder sb, string? propertyName)
            {
                if (propertyName != null)
                {
                    if (propertyName.IndexOfAny(ReadStack.SpecialCharacters) != -1)
                    {
                        sb.Append(@"['");
                        sb.Append(propertyName);
                        sb.Append(@"']");
                    }
                    else
                    {
                        sb.Append('.');
                        sb.Append(propertyName);
                    }
                }
            }
        }
    }
}
