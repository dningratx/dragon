using System.Text.RegularExpressions;

namespace DraGon.Core.Services;

public sealed record ValidationResult(bool IsValid, string ErrorMessage = "")
{
    public static ValidationResult Success() => new(true);
    public static ValidationResult Fail(string message) => new(false, message);
}

public static partial class ValidationService
{
    // Matches valid local domains with any TLD suffix (e.g. tokoku.dra, iklas.test, api.dralogy.local)
    [GeneratedRegex(@"^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)+$", RegexOptions.Compiled)]
    private static partial Regex DomainRegex();

    [GeneratedRegex(@"^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$", RegexOptions.Compiled)]
    private static partial Regex SuffixLabelRegex();

    [GeneratedRegex(@"[^a-z0-9.-]+", RegexOptions.Compiled)]
    private static partial Regex NonSlugCharsRegex();

    public static string NormalizeHostnameSuffix(string? suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return ".dra";
        }

        var trimmed = suffix.Trim().ToLowerInvariant();
        if (!trimmed.StartsWith('.'))
        {
            trimmed = "." + trimmed;
        }

        return trimmed;
    }

    public static ValidationResult ValidateHostnameSuffix(string? suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return ValidationResult.Fail("Akhiran hostname tidak boleh kosong.");
        }

        var normalized = NormalizeHostnameSuffix(suffix);
        var label = normalized[1..];
        if (label.Length < 2 || label.Length > 30)
        {
            return ValidationResult.Fail("Akhiran hostname harus terdiri dari 2 hingga 30 karakter (contoh: .dra, .test, .local).");
        }

        if (label.Contains('.'))
        {
            var segments = label.Split('.');
            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment) || !SuffixLabelRegex().IsMatch(segment))
                {
                    return ValidationResult.Fail("Format akhiran hostname tidak valid. Gunakan huruf kecil, angka, atau tanda hubung (contoh: .dra atau .test).");
                }
            }
        }
        else if (!SuffixLabelRegex().IsMatch(label))
        {
            return ValidationResult.Fail("Format akhiran hostname tidak valid. Gunakan huruf kecil, angka, atau tanda hubung (contoh: .dra atau .test).");
        }

        return ValidationResult.Success();
    }

    public static ValidationResult ValidateProjectName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ValidationResult.Fail("Nama proyek tidak boleh kosong.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length < 2 || trimmed.Length > 80)
        {
            return ValidationResult.Fail("Nama proyek harus terdiri dari 2 hingga 80 karakter.");
        }

        if (trimmed.IndexOfAny(['<', '>', '"', '|', '\0', '\r', '\n']) >= 0)
        {
            return ValidationResult.Fail("Nama proyek mengandung karakter yang tidak diizinkan.");
        }

        return ValidationResult.Success();
    }

    public static ValidationResult ValidateDomain(string? domain, string expectedSuffix = ".dra")
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return ValidationResult.Fail("Domain lokal tidak boleh kosong.");
        }

        var normalizedSuffix = NormalizeHostnameSuffix(expectedSuffix);
        var normalized = domain.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(normalizedSuffix) && !normalized.EndsWith(normalizedSuffix, StringComparison.Ordinal))
        {
            return ValidationResult.Fail($"Domain lokal wajib menggunakan akhiran '{normalizedSuffix}' (contoh: tokoku{normalizedSuffix}).");
        }

        var minLength = string.IsNullOrWhiteSpace(normalizedSuffix) ? 4 : 2 + normalizedSuffix.Length;
        if (normalized.Length < minLength || normalized.Length > 253)
        {
            return ValidationResult.Fail($"Panjang domain '{normalizedSuffix}' harus antara {minLength} hingga 253 karakter.");
        }

        if (!DomainRegex().IsMatch(normalized))
        {
            return ValidationResult.Fail(
                $"Format domain tidak valid. Gunakan huruf kecil (a-z), angka (0-9), tanda hubung (-), atau subdomain titik (contoh: api.proyek{normalizedSuffix}).");
        }

        return ValidationResult.Success();
    }

    public static string SuggestDomainFromFolderOrName(string input, string suffix = ".dra")
    {
        var normalizedSuffix = NormalizeHostnameSuffix(suffix);
        if (string.IsNullOrWhiteSpace(input))
        {
            return $"proyek{normalizedSuffix}";
        }

        var slug = input.Trim().ToLowerInvariant()
            .Replace('_', '-')
            .Replace(' ', '-');

        slug = NonSlugCharsRegex().Replace(slug, "-");
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-', '.');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "proyek";
        }

        if (!slug.EndsWith(normalizedSuffix, StringComparison.Ordinal))
        {
            if (slug.EndsWith(".dra", StringComparison.Ordinal) && !normalizedSuffix.Equals(".dra", StringComparison.OrdinalIgnoreCase))
            {
                slug = slug[..^4];
            }
            slug += normalizedSuffix;
        }

        return slug;
    }

    public static ValidationResult ValidatePort(int port, string portName)
    {
        if (port is < 1 or > 65535)
        {
            return ValidationResult.Fail($"Port {portName} harus berada di rentang 1 hingga 65535.");
        }

        return ValidationResult.Success();
    }

    public static ValidationResult ValidateDirectoryPath(string? path, string fieldLabel, bool mustExist)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Fail($"{fieldLabel} tidak boleh kosong.");
        }

        var trimmed = path.Trim();
        if (trimmed.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return ValidationResult.Fail($"{fieldLabel} mengandung karakter path yang tidak valid.");
        }

        try
        {
            var fullPath = Path.GetFullPath(trimmed);
            if (!Path.IsPathRooted(fullPath))
            {
                return ValidationResult.Fail($"{fieldLabel} harus berupa path absolut.");
            }

            if (mustExist && !Directory.Exists(fullPath))
            {
                return ValidationResult.Fail($"Folder tidak ditemukan: {fullPath}");
            }
        }
        catch (Exception ex)
        {
            return ValidationResult.Fail($"{fieldLabel} tidak valid: {ex.Message}");
        }

        return ValidationResult.Success();
    }
}
