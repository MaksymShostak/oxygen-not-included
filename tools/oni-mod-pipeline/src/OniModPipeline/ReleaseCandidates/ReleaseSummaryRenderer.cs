using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.ModTest;
using MaksymShostak.OniModPipeline.WorkshopListing;
using System.Globalization;
using System.Text;

namespace MaksymShostak.OniModPipeline.ReleaseCandidates;

internal sealed record ReleaseDocumentContext(
    OniMetadata Metadata,
    CandidateLayout Layout,
    ReleaseContentManifest ContentManifest,
    BuildProvenance Provenance,
    WorkshopListingAssembly Listing,
    IReadOnlyList<AutomatedTestResult> AutomatedTests,
    ReleaseCandidateState State,
    IReadOnlyList<string> Warnings)
{
    internal string PreviewPath => Path.Combine(
        Layout.WorkshopListingDirectory,
        Path.GetFileName(Listing.PreviewPath));
}

internal static class ReleaseSummaryRenderer
{
    internal static string Render(ReleaseDocumentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var builder = new StringBuilder();
        builder.AppendLine("# ONI Mod Pipeline release summary");
        builder.AppendLine();
        builder.AppendLine($"- Candidate state: `{context.State.ToCanonicalName()}`");
        builder.AppendLine($"- Static ID: `{context.Metadata.StaticId}`");
        builder.AppendLine($"- Title: {context.Metadata.Title}");
        builder.AppendLine($"- Version: `{context.Metadata.Version}`");
        builder.AppendLine($"- Repository commit: `{context.Provenance.RepositoryCommit}`");
        builder.AppendLine($"- Release-content digest: `{context.ContentManifest.ContentDigest}`");
        builder.AppendLine(
            $"- Prepared at (UTC): `{context.Provenance.PreparedAtUtc.ToString("O", CultureInfo.InvariantCulture)}`");
        builder.AppendLine($"- Build status: `{(context.Provenance.SourceBytesUnchanged ? "passed" : "failed")}`");
        builder.AppendLine($"- Build configuration: `{context.Provenance.Configuration}`");
        builder.AppendLine($"- Target framework: `{context.Provenance.TargetFramework}`");
        builder.AppendLine($"- .NET SDK: `{context.Provenance.DotnetSdkVersion}`");
        builder.AppendLine(
            $"- ONI game build metadata: `{context.Provenance.GameBuildMetadata ?? "unavailable"}`");
        builder.AppendLine();
        builder.AppendLine("## Automated tests");
        builder.AppendLine();
        if (context.AutomatedTests.Count == 0)
        {
            builder.AppendLine("No automated test projects were declared.");
        }
        else
        {
            foreach (var test in context.AutomatedTests)
            {
                builder.AppendLine(
                    $"- `{test.TestProjectId}`: `{(test.Passed ? "passed" : "failed")}` " +
                    $"(TRX: `{MapToFinalCandidatePath(context, test.TrxPath)}`)");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Human acceptance");
        builder.AppendLine();
        builder.AppendLine(
            $"- Acceptance checks declared: `{context.Provenance.AcceptanceCheckCount}`");
        builder.AppendLine("- Result: `not recorded`");
        builder.AppendLine("- Candidate remains `awaiting-acceptance` until exact installed bytes pass the immutable acceptance plan and verification.");
        builder.AppendLine();
        builder.AppendLine("## ONI Uploader handoff");
        builder.AppendLine();
        builder.AppendLine($"- Update Data directory: `{context.Layout.WorkshopContentDirectory}`");
        builder.AppendLine($"- Listing directory: `{context.Layout.WorkshopListingDirectory}`");
        builder.AppendLine($"- Description: `{context.Layout.DescriptionPath}`");
        builder.AppendLine($"- Change notes: `{context.Layout.ChangeNotesPath}`");
        builder.AppendLine($"- Preview: `{context.PreviewPath}`");
        builder.AppendLine(
            $"- Preview format and size: `{context.Listing.Preview.Format}`, `{context.Listing.Preview.ByteLength.ToString(CultureInfo.InvariantCulture)} bytes`");
        builder.AppendLine(
            $"- Mod types / tags: {RenderSelection(context.Listing.ModTypeLabels)}");
        builder.AppendLine(
            $"- DLC compatibility: {RenderSelection(context.Listing.DlcLabels)}");

        if (context.Warnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Warnings");
            builder.AppendLine();
            foreach (var warning in context.Warnings)
            {
                builder.AppendLine($"- {warning}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Steam publication has not occurred. Publication is an external authenticated human action.");
        return builder.ToString();
    }

    private static string RenderSelection(IReadOnlyList<string> values) =>
        values.Count == 0
            ? "`none`"
            : string.Join(", ", values.Select(value => $"`{value}`"));

    private static string MapToFinalCandidatePath(
        ReleaseDocumentContext context,
        string stagedPath)
    {
        var fileName = Path.GetFileName(stagedPath);
        return Path.Combine(context.Layout.AutomatedTestResultsDirectory, fileName);
    }
}
