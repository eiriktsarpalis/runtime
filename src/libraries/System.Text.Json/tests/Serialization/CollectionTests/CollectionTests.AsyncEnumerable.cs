// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Json.Tests.Serialization
{
    public static partial class CollectionTests
    {
        [Theory]
        [MemberData(nameof(GetAsyncEnumerableSources))]
        public static async Task WriteRootLevelAsyncEnumerable<TElement>(IEnumerable<TElement> source, int delayInterval)
        {
            string expectedJson = JsonSerializer.Serialize(source);

            using var stream = new Utf8MemoryStream();
            var asyncEnumerable = new MockedAsyncEnumerable<TElement>(source, delayInterval);
            await JsonSerializer.SerializeAsync(stream, asyncEnumerable);

            Assert.Equal(expectedJson, stream.ToString());
            Assert.Equal(1, asyncEnumerable.TotalCreatedEnumerators);
            Assert.Equal(1, asyncEnumerable.TotalDisposedEnumerators);
        }

        [Theory]
        [MemberData(nameof(GetAsyncEnumerableSources))]
        public static async Task WriteNestedAsyncEnumerable<TElement>(IEnumerable<TElement> source, int delayInterval)
        {
            string expectedJson = JsonSerializer.Serialize(new { Data = source });

            using var stream = new Utf8MemoryStream();
            var asyncEnumerable = new MockedAsyncEnumerable<TElement>(source, delayInterval);
            await JsonSerializer.SerializeAsync(stream, new { Data = asyncEnumerable });

            Assert.Equal(expectedJson, stream.ToString());
            Assert.Equal(1, asyncEnumerable.TotalCreatedEnumerators);
            Assert.Equal(1, asyncEnumerable.TotalDisposedEnumerators);
        }

        [Theory]
        [MemberData(nameof(GetAsyncEnumerableSources))]
        public static async Task WriteSequentialNestedAsyncEnumerables<TElement>(IEnumerable<TElement> source, int delayInterval)
        {
            string expectedJson = JsonSerializer.Serialize(new { Data1 = source, Data2 = source });

            using var stream = new Utf8MemoryStream();
            var asyncEnumerable = new MockedAsyncEnumerable<TElement>(source, delayInterval);
            await JsonSerializer.SerializeAsync(stream, new { Data1 = asyncEnumerable, Data2 = asyncEnumerable });

            Assert.Equal(expectedJson, stream.ToString());
            Assert.Equal(2, asyncEnumerable.TotalCreatedEnumerators);
            Assert.Equal(2, asyncEnumerable.TotalDisposedEnumerators);
        }

        [Theory]
        [MemberData(nameof(GetAsyncEnumerableSources))]
        public static async Task WriteAsyncEnumerableOfAsyncEnumerables<TElement>(IEnumerable<TElement> source, int delayInterval)
        {
            const int OuterEnumerableCount = 5;
            string expectedJson = JsonSerializer.Serialize(Enumerable.Repeat(source, OuterEnumerableCount));

            var innerAsyncEnumerable = new MockedAsyncEnumerable<TElement>(source, delayInterval);
            var outerAsyncEnumerable =
                new MockedAsyncEnumerable<IAsyncEnumerable<TElement>>(
                    Enumerable.Repeat(innerAsyncEnumerable, OuterEnumerableCount), delayInterval);

            using var stream = new Utf8MemoryStream();
            await JsonSerializer.SerializeAsync(stream, outerAsyncEnumerable);

            Assert.Equal(expectedJson, stream.ToString());
            Assert.Equal(1, outerAsyncEnumerable.TotalCreatedEnumerators);
            Assert.Equal(1, outerAsyncEnumerable.TotalDisposedEnumerators);
            Assert.Equal(OuterEnumerableCount, innerAsyncEnumerable.TotalCreatedEnumerators);
            Assert.Equal(OuterEnumerableCount, innerAsyncEnumerable.TotalDisposedEnumerators);
        }

        [Fact]
        public static void WriteRootLevelAsyncEnumerableSync_ThrowsNotSupportedException()
        {
            IAsyncEnumerable<int> asyncEnumerable = new MockedAsyncEnumerable<int>(Enumerable.Range(1, 10));
            Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(asyncEnumerable));
        }

        [Fact]
        public static void WriteNestedAsyncEnumerableSync_ThrowsNotSupportedException()
        {
            IAsyncEnumerable<int> asyncEnumerable = new MockedAsyncEnumerable<int>(Enumerable.Range(1, 10));
            Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new { Data = asyncEnumerable }));
        }

        [Fact]
        public static async Task ReadRootLevelAsyncEnumerable_ThrowsNotSupportedException()
        {
            var utf8Stream = new Utf8MemoryStream("[0,1,2,3,4]");

            await Assert.ThrowsAsync<NotSupportedException>(async () => await JsonSerializer.DeserializeAsync<IAsyncEnumerable<int>>(utf8Stream));
        }

        [Fact]
        public static async Task ReadNestedAsyncEnumerable_ThrowsNotSupportedException()
        {
            var utf8Stream = new Utf8MemoryStream("[[0,1,2,3,4]]");

            await Assert.ThrowsAsync<NotSupportedException>(async () => await JsonSerializer.DeserializeAsync<List<IAsyncEnumerable<int>>>(utf8Stream));
        }

        public static IEnumerable<object[]> GetAsyncEnumerableSources()
        {
            yield return WrapArgs(Enumerable.Empty<int>(), 0);
            yield return WrapArgs(Enumerable.Range(0, 20), 0);
            yield return WrapArgs(Enumerable.Range(0, 100), 20);

            static object[] WrapArgs<TSource>(IEnumerable<TSource> source, int delayInterval) => new object[]{ source, delayInterval };
        }

        private class MockedAsyncEnumerable<TElement> : IAsyncEnumerable<TElement>, IEnumerable<TElement>
        {
            private readonly IEnumerable<TElement> _source;
            private readonly TimeSpan _delay;
            private readonly int _delayInterval;

            public int TotalCreatedEnumerators { get; private set; }
            public int TotalDisposedEnumerators { get; private set; }
            public int TotalEnumeratedElements { get; private set; }

            public MockedAsyncEnumerable(IEnumerable<TElement> source, int delayInterval = 0, TimeSpan? delay = null)
            {
                _source = source;
                _delay = delay ?? TimeSpan.FromMilliseconds(20);
                _delayInterval = delayInterval;
            }

            public IAsyncEnumerator<TElement> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return new MockedAsyncEnumerator(this, cancellationToken);
            }

            // Enumerator class required to instrument IAsyncDisposable calls
            private class MockedAsyncEnumerator : IAsyncEnumerator<TElement>
            {
                private readonly MockedAsyncEnumerable<TElement> _enumerable;
                private IAsyncEnumerator<TElement> _innerEnumerator;

                public MockedAsyncEnumerator(MockedAsyncEnumerable<TElement> enumerable, CancellationToken token)
                {
                    _enumerable = enumerable;
                    _innerEnumerator = enumerable.GetAsyncEnumeratorInner(token);
                }

                public TElement Current => _innerEnumerator.Current;
                public ValueTask DisposeAsync()
                {
                    _enumerable.TotalDisposedEnumerators++;
                    return _innerEnumerator.DisposeAsync();
                }

                public ValueTask<bool> MoveNextAsync() => _innerEnumerator.MoveNextAsync();
            }

            private async IAsyncEnumerator<TElement> GetAsyncEnumeratorInner(CancellationToken cancellationToken = default)
            {
                TotalCreatedEnumerators++;
                int i = 0;
                foreach (TElement element in _source)
                {
                    if (_delayInterval > 0 && i > 0 && i % _delayInterval == 0)
                    {
                        await Task.Delay(_delay, cancellationToken);
                    }

                    TotalEnumeratedElements++;
                    yield return element;
                    i++;
                }
            }

            public IEnumerator<TElement> GetEnumerator() => throw new InvalidOperationException("Should be serialized as IAsyncEnumerable and not as IEnumerable.");
            IEnumerator IEnumerable.GetEnumerator() => throw new InvalidOperationException("Should be serialized as IAsyncEnumerable and not as IEnumerable.");
        }

        private class Utf8MemoryStream : MemoryStream
        {
            public Utf8MemoryStream() : base()
            {
            }

            public Utf8MemoryStream(string text) : base(Encoding.UTF8.GetBytes(text))
            {
            }

            public override string ToString() => Encoding.UTF8.GetString(ToArray());
        }
    }
}
