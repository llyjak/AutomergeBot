using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PerfectGym.AutomergeBot.MergingBranches
{
    public interface IMergeDirections
    {
        string[] GetMergeDestinationBranchNames(string branchName);
        string GetDotGraph();
        string GetMergingConfigurationInfo();
    }

    public interface IMergeDirectionsProvider
    {
        IMergeDirections Get();
    }

    public interface IMergeDirectionsProviderConfigurator
    {
        void Clear();
        MergeDirectionsProvider Add(string from, string to);
        MergeDirectionsProvider UpdateMergeDirections((string from, string to)[] mergeDirections);
    }

    public class MergeDirectionsProvider : IMergeDirectionsProvider, IMergeDirectionsProviderConfigurator
    {
        private const int MaxEntries = 100;
        private readonly HashSet<(string from, string to)> _set = new HashSet<(string from, string to)>();

        public void Clear()
        {
            _set.Clear();
        }

        public MergeDirectionsProvider Add(string from, string to)
        {
            AddImpl(from, to);
            EnsureThereIsNoCycle();
            return this;
        }

        private void AddImpl(string @from, string to)
        {
            if (_set.Count >= MaxEntries)
            {
                throw new InvalidOperationException($"Maximum entries count exceeded. There can be {MaxEntries} entries max.");
            }

            _set.Add((@from, to));
        }

        private MergeDirectionsProvider AddMany(IEnumerable<(string from, string to)> mergeDirections)
        {
            foreach (var mergeDirection in mergeDirections)
            {
                Add(mergeDirection.from, mergeDirection.to);
            }
            EnsureThereIsNoCycle();
            return this;
        }

        public MergeDirectionsProvider UpdateMergeDirections((string from, string to)[] mergeDirections)
        {
            Clear();
            return AddMany(mergeDirections);
        }

        private void EnsureThereIsNoCycle()
        {
            //todo:
        }

        public IMergeDirections Get()
        {
            return new MergeDirections(_set);
        }

        private class MergeDirections : IMergeDirections
        {
            private readonly HashSet<(string from, string to)> _set;

            public MergeDirections(ISet<(string from, string to)> set)
            {
                _set = new HashSet<(string from, string to)>(set);
            }

            public string[] GetMergeDestinationBranchNames(string branchName)
            {
                var destBranch = _set.Where(x => x.@from == branchName).Select(x => x.to).ToArray();
                return destBranch;
            }

            public string GetMergingConfigurationInfo()
            {
                var records = _set.Select(x => $"{x.from}->{x.to}");
                return "[" + string.Join(", ", records) + "]";
            }

            public string GetDotGraph()
            {
                var sb = new StringBuilder();
                foreach (var (from, to) in _set)
                {
                    sb.AppendLine($"{from} -> {to};");
                }

                return "digraph merge_directions { " + Environment.NewLine + sb + "}";
            }
        }
    }
}