using System;
using System.Diagnostics;

namespace PerfectGym.AutomergeBot.Models
{
    [DebuggerDisplay("Branch:{" + nameof(Name) + "}")]
    public struct BranchName
    {
        public BranchName(string name)
        {
            Name = name;
        }

        public static BranchName CreateFromRef(string gitRef)
        {
            if (!gitRef.StartsWith(Consts.RefsHeads))
                throw new ArgumentException("Wrong gitRef format");

            return new BranchName(gitRef.Substring(Consts.RefsHeads.Length));
        }

        public string Name { get; set; }
        public string GitRef => Consts.RefsHeads + Name;

        public string SanitizedName => Sanitize(Name);

        public static string Sanitize(string name)
        {
            return name.Replace('/', '-');
        }

        public override string ToString()
        {
            return Name;
        }
    }
}