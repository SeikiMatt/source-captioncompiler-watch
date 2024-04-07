using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SourceCaptioncompilerWatch;

public static class Util
{
    private static string GenerateMd5Recursive(string? path)
    {
        path = path ?? string.Empty;

        var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
            .OrderBy(p => p).ToList();

        var md5 = MD5.Create();

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];

            var relativePath = file[(path.Length + 1)..];
            var pathBytes = Encoding.UTF8.GetBytes(relativePath.ToLower());
            md5.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

            var contentBytes = File.ReadAllBytes(file);
            if (i == files.Count - 1)
                md5.TransformFinalBlock(contentBytes, 0, contentBytes.Length);
            else
                md5.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
        }

        return md5.Hash != null ? BitConverter.ToString(md5.Hash).Replace("-", "").ToLower() : "";
    }
    
    public static bool CompareCompilerFolders(string? inBuildFolder, string? inResourceFolder)
    {
        var md5Dist = GenerateMd5Recursive(inBuildFolder);
        var md5Resource = GenerateMd5Recursive(inResourceFolder);

        return  md5Dist == md5Resource;
    }
    
    public static bool ValidateCompilerFiles(string resourceFolderPath)
    {
        return Constants.CaptionCompilerFiles.All(path => File.Exists(resourceFolderPath + "\\" + path));
    }

    public static string FilePathFinalEntry(string path)
    {
        var pathSplit = path.Split("\\");
        return pathSplit.Last(v => v.Length > 0);
    }

    public static IEnumerable<string> FilePathFinalEntry(IEnumerable<string> paths)
    {
        string[] fileNames = [];
        fileNames = paths.Aggregate(fileNames,
            (current, path) => current.Append(path.Split("\\").Last(v => v.Length > 0)).ToArray());
        return fileNames;
    }
    
    // https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories?redirectedfrom=MSDN
    public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
    {
        var dir = new DirectoryInfo(sourceDir);
        
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
        
        var dirs = dir.GetDirectories();
        
        Directory.CreateDirectory(destinationDir);
        
        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }
        
        if (!recursive) return;
        foreach (var subDir in dirs)
        {
            var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir, true);
        }
    }
}