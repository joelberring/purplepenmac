// Exercise PDF compression using only the runtime and libraries in a released app.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Filters;
using PdfSharp.Pdf.IO;

internal static class Program
{
    // Run without UI, installed .NET or Homebrew; fail on any broken dependency.
    private static void Main()
    {
        byte[] original = Encoding.UTF8.GetBytes(new string('x', 8192));
        FlateDecode filter = new FlateDecode();
        if (!original.SequenceEqual(filter.Decode(filter.Encode(original))))
            throw new InvalidDataException("PDF Flate round trip failed.");

        using MemoryStream compressed = new MemoryStream();
        using (BrotliStream encoder = new BrotliStream(compressed, CompressionLevel.Optimal, true))
            encoder.Write(original);
        compressed.Position = 0;
        using BrotliStream decoder = new BrotliStream(compressed, CompressionMode.Decompress);
        using MemoryStream decoded = new MemoryStream();
        decoder.CopyTo(decoded);
        if (!original.SequenceEqual(decoded.ToArray()))
            throw new InvalidDataException("Brotli round trip failed.");

        CreateAndReadPdf("single.pdf", 1);
        CreateAndReadPdf("multiple.pdf", 4);
        Console.WriteLine("PASS: Flate, Brotli, compressed one-page and four-page PDFs.");
    }

    // Save compressed vector content and reopen it through the shipped PDF library.
    private static void CreateAndReadPdf(string path, int pageCount)
    {
        using (PdfDocument document = new PdfDocument()) {
            document.Options.CompressContentStreams = true;
            for (int index = 0; index < pageCount; ++index) {
                PdfPage page = document.AddPage();
                page.Size = PageSize.A4;
                using XGraphics graphics = XGraphics.FromPdfPage(page);
                for (int line = 0; line < 100; ++line)
                    graphics.DrawLine(XPens.Purple, 20, 20 + line, 200, 30 + line);
            }
            document.Save(path);
        }
        if (!Encoding.ASCII.GetString(File.ReadAllBytes(path)).Contains("/FlateDecode"))
            throw new InvalidDataException("PDF content was not compressed.");
        using PdfDocument reopened = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        if (reopened.PageCount != pageCount)
            throw new InvalidDataException("PDF page count changed.");
        // Decoding content, not just the page tree, also exercises decompression.
        for (int index = 0; index < pageCount; ++index) {
            if (reopened.Pages[index].Contents.CreateSingleContent().Stream.UnfilteredValue.Length == 0)
                throw new InvalidDataException("Reopened PDF content is empty.");
        }
    }
}
