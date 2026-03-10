using System.Text.RegularExpressions;
using Xunit;

namespace BankReporting.Tests;

public class SecurityTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    [Fact]
    public void SourceCode_ShouldNotContainObviousHardcodedSecrets()
    {
        var disallowedPatterns = new[]
        {
            "AKIA[0-9A-Z]{16}",
            "-----BEGIN (RSA|EC|DSA|OPENSSH) PRIVATE KEY-----",
            "ghp_[A-Za-z0-9]{30,}",
            "(?i)api[_-]?key\\s*[:=]\\s*['\"][A-Za-z0-9_\\-]{20,}['\"]",
            "(?i)secret\\s*[:=]\\s*['\"][^'\"]{8,}['\"]",
            "(?i)password\\s*[:=]\\s*['\"][^'\"]{8,}['\"]"
        };

        var ignoreDirs = new[] { "bin", "obj", ".git", "node_modules" };
        var files = Directory
            .EnumerateFiles(RepoRoot, "*.*", SearchOption.AllDirectories)
            .Where(file => !ignoreDirs.Any(dir => file.Split(Path.DirectorySeparatorChar).Contains(dir)))
            .Where(file => file.EndsWith(".cs") || file.EndsWith(".json") || file.EndsWith(".md"));

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (var pattern in disallowedPatterns)
            {
                var regex = new Regex(pattern);
                Assert.False(regex.IsMatch(content), $"疑似硬編碼 secrets: {Path.GetRelativePath(RepoRoot, file)} 命中規則 {pattern}");
            }
        }
    }

    [Fact]
    public void RequestLogging_ShouldAvoidSensitiveFields()
    {
        var middlewarePath = Path.Combine(RepoRoot, "backend", "Middleware", "RequestMonitoringMiddleware.cs");
        var content = File.ReadAllText(middlewarePath);

        var requestLogLine = "_logger.LogInformation(\"HTTP {Method} {Route} => {StatusCode} ({DurationMs}ms)\"";
        Assert.Contains(requestLogLine, content);

        var sensitiveFields = new[] { "Authorization", "Token", "KeyA", "KeyB" };
        foreach (var field in sensitiveFields)
        {
            Assert.DoesNotContain($"{{{field}}}", content, StringComparison.OrdinalIgnoreCase);
        }
    }
}
