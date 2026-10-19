using FluentAssertions;
using System.Text.RegularExpressions;
using Xunit;

namespace TransactionAggregation.Tests.Architecture
{
    public partial class DockerfileTests
    {
        public static TheoryData<string, string> Images => new()
        {
            { "TransactionAggregationAPI/TransactionAggregationAPI.csproj", "TransactionAggregationAPI/Dockerfile" },
            { "TransactionAggregation.Worker/TransactionAggregation.Worker.csproj", "TransactionAggregation.Worker/Dockerfile" },
            { "TransactionAggregation.MockAggregator/TransactionAggregation.MockAggregator.csproj", "TransactionAggregation.MockAggregator/Dockerfile" }
        };

        [GeneratedRegex(@"ProjectReference Include=""([^""]+)""")]
        private static partial Regex ProjectReference();

        [GeneratedRegex(@"COPY \[""([^""]+\.csproj)""")]
        private static partial Regex CopiedProject();

        [Theory]
        [MemberData(nameof(Images))]
        public void EveryProjectTheImageBuildsFromIsCopiedBeforeRestore(string project, string dockerfile)
        {
            var root = RepositoryRoot();
            var needed = Closure(root, Path.Combine(root, project));
            var copied = CopiedProject().Matches(File.ReadAllText(Path.Combine(root, dockerfile)))
                .Select(m => m.Groups[1].Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

            needed.Where(p => !copied.Contains(p)).Should().BeEmpty(
                $"{dockerfile} restores from the project files it copies, so a project it does not copy fails the image build");
        }

        private static HashSet<string> Closure(string root, string csproj)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>([Path.GetFullPath(csproj)]);

            while (pending.Count > 0)
            {
                var path = pending.Pop();
                if (!seen.Add(Path.GetRelativePath(root, path).Replace('\\', '/')))
                    continue;

                foreach (Match reference in ProjectReference().Matches(File.ReadAllText(path)))
                    pending.Push(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, reference.Groups[1].Value.Replace('\\', '/'))));
            }

            return seen;
        }

        private static string RepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TransactionAggregationAPI.slnx")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }
}
