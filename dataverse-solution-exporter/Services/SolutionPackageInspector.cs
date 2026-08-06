using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace dataverse_solution_exporter.Services;

public sealed class SolutionPackageInspector
{
    public SolutionPackageInspection Inspect(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var solutionEntry = archive.GetEntry("solution.xml")
                            ?? throw new InvalidDataException("The exported ZIP does not contain solution.xml.");
        using var stream = solutionEntry.Open();
        var document = XDocument.Load(stream, LoadOptions.None);
        var manifest = document.Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "SolutionManifest", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("solution.xml does not contain SolutionManifest.");

        string ReadRequired(string name) => manifest.Elements()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
            ?.Value.Trim() is { Length: > 0 } value
                ? value
                : throw new InvalidDataException($"solution.xml does not contain {name}.");

        var rootComponents = manifest.Descendants()
            .Count(element => string.Equals(element.Name.LocalName, "RootComponent", StringComparison.OrdinalIgnoreCase));

        return new SolutionPackageInspection
        {
            UniqueName = ReadRequired("UniqueName"),
            Version = ReadRequired("Version"),
            IsManaged = ReadRequired("Managed") switch
            {
                "0" => false,
                "1" => true,
                var value => throw new InvalidDataException($"Unexpected Managed value '{value}' in solution.xml."),
            },
            RootComponentCount = rootComponents,
            Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(packagePath))),
        };
    }
}

public sealed class SolutionPackageInspection
{
    public required string UniqueName { get; init; }
    public required string Version { get; init; }
    public bool IsManaged { get; init; }
    public int RootComponentCount { get; init; }
    public required string Sha256 { get; init; }
}
