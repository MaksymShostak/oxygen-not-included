using System.Text;

namespace MaksymShostak.OniModPipeline.ReleaseCandidates;

internal static class UploaderChecklistRenderer
{
    internal static string Render(ReleaseDocumentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var builder = new StringBuilder();
        builder.AppendLine("# ONI Uploader checklist");
        builder.AppendLine();
        builder.AppendLine($"Candidate: `{context.Layout.CandidateDirectory}`");
        builder.AppendLine($"Content digest: `{context.ContentManifest.ContentDigest}`");
        builder.AppendLine($"Current state: `{context.State.ToCanonicalName()}`");
        builder.AppendLine();
        if (context.State != ReleaseCandidateState.ReadyForUpload)
        {
            builder.AppendLine(
                "Publication remains blocked until candidate state is ready-for-upload.");
            builder.AppendLine();
        }

        builder.AppendLine("[ ] Candidate state is ready-for-upload.");
        builder.AppendLine(
            $"[ ] Update Data points exactly to `{context.Layout.WorkshopContentDirectory}`.");
        builder.AppendLine(
            "[ ] The displayed data path is not the mutable Dev/Local test directory.");
        builder.AppendLine(
            $"[ ] Description comes from `{context.Layout.DescriptionPath}`.");
        builder.AppendLine(
            "[ ] Paragraphs, blank lines, ---, headings, and [list] blocks remain separate after paste.");
        builder.AppendLine(
            $"[ ] Change notes come from `{context.Layout.ChangeNotesPath}`.");
        builder.AppendLine($"[ ] Preview comes from `{context.PreviewPath}`.");
        builder.AppendLine(
            "[ ] Title, mod types, tags, and DLC compatibility match release-summary.md.");
        builder.AppendLine(
            "[ ] The final form has been reviewed immediately before Publish.");
        builder.AppendLine();
        builder.AppendLine(
            "Publish is a deliberate authenticated human action. ONI Mod Pipeline does not perform or record it.");
        return builder.ToString();
    }
}
