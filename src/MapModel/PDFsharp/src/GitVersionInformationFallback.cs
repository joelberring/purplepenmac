// GitVersionInformationFallback.cs
//
// Used only when the GitVersion MSBuild task is disabled
// (-p:DisableGitVersionTask=true), for example in CI on branches that the
// bundled gitversion.yml does not describe. Supplies the members that
// PdfSharp.Shared reads from the generated GitVersionInformation class.

/// <summary>Static version information used when GitVersion does not run.</summary>
internal static class GitVersionInformation
{
    public static string Major = "6";
    public static string Minor = "2";
    public static string Patch = "0";
    public static string PreReleaseLabel = "";
    public static string MajorMinorPatch = "6.2.0";
    public static string SemVer = "6.2.0";
    public static string InformationalVersion = "6.2.0";
    public static string BranchName = "";
    public static string CommitDate = "";
}
