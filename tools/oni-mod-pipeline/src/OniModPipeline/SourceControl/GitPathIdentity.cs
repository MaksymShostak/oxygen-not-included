namespace MaksymShostak.OniModPipeline.SourceControl;

internal static class GitPathIdentity
{
    // Git separates components with '/'. On POSIX a backslash is part of the filename.
    internal static string ToNative(string gitPath, char directorySeparator)
    {
        if (directorySeparator == '\\' && gitPath.Contains('\\'))
            throw new InvalidDataException($"Git pathname '{gitPath}' contains a literal backslash that Windows cannot represent.");
        return gitPath.Replace('/', directorySeparator);
    }

    internal static string ToPortableKey(string nativePath, char directorySeparator)
    {
        string key = nativePath.Replace(directorySeparator, '/');
        if (key.Contains('\\'))
            throw new InvalidDataException($"Contributing path '{nativePath}' contains a literal backslash and cannot be represented unambiguously in portable release evidence.");
        return key;
    }
}
