using System.Text;
using System.Text.Json;
using DraGon.Core.Models;

namespace DraGon.Core.Services;

public static class ProjectDetectorService
{
    public static void AnalyzeProjectFolder(ProjectItem project)
    {
        if (string.IsNullOrWhiteSpace(project.FolderPath) || !Directory.Exists(project.FolderPath))
        {
            project.FolderExists = false;
            return;
        }

        project.FolderExists = true;
        var root = Path.GetFullPath(project.FolderPath);

        var artisanPath = Path.Combine(root, "artisan");
        var composerPath = Path.Combine(root, "composer.json");
        var packageJsonPath = Path.Combine(root, "package.json");
        var publicFolder = Path.Combine(root, "public");

        project.HasArtisan = File.Exists(artisanPath);
        project.HasComposerJson = File.Exists(composerPath);
        project.HasPackageJson = File.Exists(packageJsonPath);
        project.HasNpmDevScript = project.HasPackageJson && HasDevScriptInPackageJson(packageJsonPath);

        if (Directory.Exists(publicFolder) &&
            (File.Exists(Path.Combine(publicFolder, "index.php")) ||
             File.Exists(Path.Combine(publicFolder, "index.html"))))
        {
            project.DocumentRoot = publicFolder;
        }
        else
        {
            project.DocumentRoot = root;
        }

        project.DetectedLaravelVersion = null;
        project.DetectedPhpConstraint = null;

        if (project.HasComposerJson)
        {
            TryReadComposerMetadata(composerPath, out var laravelVer, out var phpConstraint);
            project.DetectedLaravelVersion = laravelVer;
            project.DetectedPhpConstraint = phpConstraint;
        }

        if (project.HasArtisan && project.HasComposerJson)
        {
            project.FrameworkType = ProjectFramework.Laravel;
        }
        else if (File.Exists(Path.Combine(root, "wp-config.php")) ||
                 File.Exists(Path.Combine(root, "wp-config-sample.php")) ||
                 File.Exists(Path.Combine(root, "wp-login.php")) ||
                 Directory.Exists(Path.Combine(root, "wp-content")))
        {
            project.FrameworkType = ProjectFramework.WordPress;
        }
        else if (project.HasComposerJson ||
                 File.Exists(Path.Combine(root, "index.php")) ||
                 File.Exists(Path.Combine(project.DocumentRoot, "index.php")))
        {
            project.FrameworkType = ProjectFramework.PhpGeneric;
        }
        else if (project.HasPackageJson)
        {
            project.FrameworkType = ProjectFramework.NodeApp;
        }
        else
        {
            project.FrameworkType = ProjectFramework.StaticOrOther;
        }
    }

    private static bool HasDevScriptInPackageJson(string packageJsonPath)
    {
        try
        {
            var json = File.ReadAllText(packageJsonPath, Encoding.UTF8);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("scripts", out var scripts) &&
                scripts.ValueKind == JsonValueKind.Object)
            {
                return scripts.TryGetProperty("dev", out _);
            }
        }
        catch
        {
            // Ignore malformed package.json
        }

        return false;
    }

    private static void TryReadComposerMetadata(string composerJsonPath, out string? laravelVersion, out string? phpConstraint)
    {
        laravelVersion = null;
        phpConstraint = null;

        try
        {
            var json = File.ReadAllText(composerJsonPath, Encoding.UTF8);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("require", out var requireObj) &&
                requireObj.ValueKind == JsonValueKind.Object)
            {
                if (requireObj.TryGetProperty("laravel/framework", out var lfProp) &&
                    lfProp.ValueKind == JsonValueKind.String)
                {
                    laravelVersion = lfProp.GetString();
                }

                if (requireObj.TryGetProperty("php", out var phpProp) &&
                    phpProp.ValueKind == JsonValueKind.String)
                {
                    phpConstraint = phpProp.GetString();
                }
            }
        }
        catch
        {
            // Ignore malformed composer.json
        }
    }
}
