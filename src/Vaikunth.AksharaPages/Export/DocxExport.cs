using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlToOpenXml;
using Vaikunth.DtoLibrary;

namespace Vaikunth.AksharaPages.Export;

/// <summary>Akshara Pages → Microsoft Word (.docx). The page's HTML becomes Word paragraphs, headings, lists, tables and pictures.</summary>
public static class DocxExport
{
    public static async Task<byte[]> BuildAsync(PagesContent page, string title, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        using (var word = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = word.AddMainDocumentPart();
            main.Document = new Document(new Body());
            // pictures pasted into the page are data: URIs; never fetch anything from the network
            var converter = new HtmlConverter(main) { ImageProcessing = ImageProcessingMode.EmbedDataUriOnly };
            await converter.ParseBody(page.Html ?? "", ct);

            // A4 (default) or US Letter, 2.54 cm margins, like the page in the editor
            var letter = string.Equals(page.PageSize, "Letter", StringComparison.OrdinalIgnoreCase);
            var body = main.Document.Body!;
            body.RemoveAllChildren<SectionProperties>();
            body.Append(new SectionProperties(
                new PageSize { Width = (UInt32Value)(letter ? 12240U : 11906U), Height = (UInt32Value)(letter ? 15840U : 16838U) },
                new PageMargin { Top = 1440, Bottom = 1440, Left = (UInt32Value)1440U, Right = (UInt32Value)1440U, Header = (UInt32Value)720U, Footer = (UInt32Value)720U }));

            word.PackageProperties.Title = title;
            word.PackageProperties.Creator = "Vaikunth Desk · Akshara Pages";
            main.Document.Save();
        }
        return ms.ToArray();
    }
}
