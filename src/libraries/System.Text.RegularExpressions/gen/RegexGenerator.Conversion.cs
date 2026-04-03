// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SourceGenerators;

namespace System.Text.RegularExpressions.Generator
{
    public partial class RegexGenerator
    {
        /// <summary>
        /// Converts mutable <see cref="RegexTree"/> and <see cref="AnalysisResults"/>
        /// into an immutable, structurally equatable <see cref="RegexTreeSpec"/> snapshot suitable for
        /// incremental caching in the Roslyn source generator pipeline.
        /// </summary>
        private static RegexTreeSpec CreateRegexTreeSpec(RegexTree tree, AnalysisResults analysis, string? cultureName)
        {
            RegexNodeSpec rootSpec = ConvertNode(tree.Root, analysis);

            return new RegexTreeSpec(
                Root: rootSpec,
                Options: tree.Options,
                CaptureCount: tree.CaptureCount,
                CultureName: cultureName,
                CaptureNames: tree.CaptureNames?.ToImmutableEquatableArray(),
                CaptureNameToNumberMapping: tree.CaptureNameToNumberMapping?.ToImmutableEquatableDictionary<string, int>(),
                CaptureNumberSparseMapping: tree.CaptureNumberSparseMapping?.ToImmutableEquatableDictionary<int, int>(),
                FindOptimizations: ConvertFindOptimizations(tree.FindOptimizations),
                HasIgnoreCase: analysis.HasIgnoreCase,
                HasRightToLeft: analysis.HasRightToLeft);
        }

        /// <summary>Converts a <see cref="RegexNode"/> tree to a <see cref="RegexNodeSpec"/> tree using an explicit stack.</summary>
        private static RegexNodeSpec ConvertNode(RegexNode node, AnalysisResults analysis)
        {
            RegexNodeSpec? rootSpec = null;
            Stack<RegexNodeConversionFrame> stack = new();
            stack.Push(new RegexNodeConversionFrame(node));

            while (stack.Count != 0)
            {
                RegexNodeConversionFrame frame = stack.Pop();
                int childCount = frame.Node.ChildCount();

                if (frame.NextChildIndex < childCount)
                {
                    int childIndex = frame.NextChildIndex++;
                    stack.Push(frame);
                    stack.Push(new RegexNodeConversionFrame(frame.Node.Child(childIndex)));
                    continue;
                }

                RegexNodeSpec currentSpec = new(
                    Kind: frame.Node.Kind,
                    Options: frame.Node.Options,
                    Ch: frame.Node.Ch,
                    Str: frame.Node.Str,
                    M: frame.Node.M,
                    N: frame.Node.N,
                    Children: frame.Children is null ? ImmutableEquatableArray<RegexNodeSpec>.Empty : new ImmutableEquatableArray<RegexNodeSpec>(frame.Children),
                    IsAtomicByAncestor: analysis.IsAtomicByAncestor(frame.Node),
                    MayBacktrack: analysis.MayBacktrack(frame.Node),
                    MayContainCapture: analysis.MayContainCapture(frame.Node),
                    IsInLoop: analysis.IsInLoop(frame.Node));

                if (stack.Count == 0)
                {
                    rootSpec = currentSpec;
                    break;
                }

                RegexNodeConversionFrame parent = stack.Pop();
                int completedChildIndex = parent.NextChildIndex - 1;
                (parent.Children ??= new RegexNodeSpec[parent.Node.ChildCount()])[completedChildIndex] = currentSpec;
                stack.Push(parent);
            }

            Debug.Assert(rootSpec is not null);
            return rootSpec!;
        }

        /// <summary>Converts <see cref="RegexFindOptimizations"/> to <see cref="FindOptimizationsSpec"/>.</summary>
        private static FindOptimizationsSpec ConvertFindOptimizations(RegexFindOptimizations opts)
        {
            ImmutableEquatableArray<FixedDistanceSetSpec>? fixedDistanceSets = null;
            if (opts.FixedDistanceSets is { } sets)
            {
                fixedDistanceSets = sets.Select(s => new FixedDistanceSetSpec(
                    Set: s.Set,
                    Chars: s.Chars?.ToImmutableEquatableArray(),
                    Negated: s.Negated,
                    Distance: s.Distance,
                    Range: s.Range)).ToImmutableEquatableArray();
            }

            LiteralAfterLoopSpec? literalAfterLoop = null;
            if (opts.LiteralAfterLoop is { } lal)
            {
                literalAfterLoop = new LiteralAfterLoopSpec(
                    LiteralChar: lal.Literal.Char,
                    LiteralString: lal.Literal.String,
                    LiteralStringComparison: lal.Literal.StringComparison,
                    LiteralChars: lal.Literal.Chars?.ToImmutableEquatableArray());
            }

            return new FindOptimizationsSpec(
                FindMode: opts.FindMode,
                LeadingAnchor: opts.LeadingAnchor,
                TrailingAnchor: opts.TrailingAnchor,
                MinRequiredLength: opts.MinRequiredLength,
                MaxPossibleLength: opts.MaxPossibleLength,
                LeadingPrefix: opts.LeadingPrefix,
                LeadingPrefixes: opts.LeadingPrefixes.ToImmutableEquatableArray(),
                FixedDistanceLiteral: opts.FixedDistanceLiteral,
                FixedDistanceSets: fixedDistanceSets,
                LiteralAfterLoop: literalAfterLoop);
        }

        private struct RegexNodeConversionFrame
        {
            public RegexNodeConversionFrame(RegexNode node)
            {
                Node = node;
                NextChildIndex = 0;
                Children = null;
            }

            public readonly RegexNode Node;
            public int NextChildIndex;
            public RegexNodeSpec[]? Children;
        }
    }
}
