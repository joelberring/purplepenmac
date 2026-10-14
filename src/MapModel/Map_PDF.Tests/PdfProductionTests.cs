/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are
 * met:
 *
 * 1. Redistributions of source code must retain the above copyright
 * notice, this list of conditions and the following disclaimer.
 *
 * 2. Redistributions in binary form must reproduce the above copyright
 * notice, this list of conditions and the following disclaimer in the
 * documentation and/or other materials provided with the distribution.
 *
 * 3. Neither the name of Purple Pen, nor the names of its contributors may
 * be used to endorse or promote products derived from this software without
 * specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Drawing;
using System.IO;
using System.Text;
using NUnit.Framework;
using PurplePen.Graphics2D;
using PurplePen.MapModel;

namespace Map_PDF.Tests
{
    /// <summary>Verifies production metadata emitted by the PDF graphics backend.</summary>
    [TestFixture]
    [NonParallelizable]
    public class PdfProductionTests
    {
        /// <summary>Source colour overprint must reach PDF fill and stroke graphics state entries.</summary>
        [Test]
        public void SymColorOverprint_EmitsPdfOverprintEntries()
        {
            string pdfFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");
            try {
                IPdfWriter pdfWriter = new PdfWriter();
                IPdfDocumentWriter documentWriter = pdfWriter.CreateDocument(pdfFileName, "Overprint test", true);
                IGraphicsTarget graphicsTarget = documentWriter.BeginPage(new SizeF(2, 2));
                SymColor color = new SymColor(SymLayer.Normal);
                color.SetCMYK(0.35F, 0.95F, 0, 0);
                color.OverPrint = true;
                object brushKey = color.GetBrushKey(graphicsTarget);
                object penKey = new object();
                graphicsTarget.CreatePen(penKey, brushKey, 2, LineCapMode.Round, LineJoinMode.Round, 2);
                graphicsTarget.FillRectangle(brushKey, new RectangleF(10, 10, 40, 40));
                graphicsTarget.DrawLine(penKey, new PointF(10, 60), new PointF(50, 60));
                documentWriter.EndPage(graphicsTarget);
                documentWriter.Save();

                string pdfContents = Encoding.Latin1.GetString(File.ReadAllBytes(pdfFileName));
                Assert.That(pdfContents, Does.Contain("/OP true"));
                Assert.That(pdfContents, Does.Contain("/op true"));
                Assert.That(pdfContents, Does.Contain("/OPM 1"));
            }
            finally {
                if (File.Exists(pdfFileName))
                    File.Delete(pdfFileName);
            }
        }

        /// <summary>
        /// After an overprinting stroke, a knockout fill and a knockout stroke must really be knockout:
        /// every ExtGState that sets /OP also sets /op explicitly (an absent /op inherits /OP), and
        /// switching overprint off writes an explicit false.
        /// </summary>
        [Test]
        public void OverprintStrokeThenKnockout_WritesExplicitOverprintFlags()
        {
            string pdfFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");
            try {
                IPdfWriter pdfWriter = new PdfWriter();
                IPdfDocumentWriter documentWriter = pdfWriter.CreateDocument(pdfFileName, "Overprint reset test", true);
                IGraphicsTarget graphicsTarget = documentWriter.BeginPage(new SizeF(2, 2));

                SymColor purple = new SymColor(SymLayer.Normal);
                purple.SetCMYK(0.35F, 0.95F, 0, 0);
                purple.OverPrint = true;
                SymColor black = new SymColor(SymLayer.Normal);
                black.SetCMYK(0, 0, 0, 1);
                black.OverPrint = false;

                object purplePen = new object();
                graphicsTarget.CreatePen(purplePen, purple.GetBrushKey(graphicsTarget), 2, LineCapMode.Round, LineJoinMode.Round, 2);
                graphicsTarget.DrawLine(purplePen, new PointF(10, 10), new PointF(50, 10));

                object blackBrush = black.GetBrushKey(graphicsTarget);
                object blackPen = new object();
                graphicsTarget.CreatePen(blackPen, blackBrush, 2, LineCapMode.Round, LineJoinMode.Round, 2);
                graphicsTarget.FillRectangle(blackBrush, new RectangleF(10, 20, 40, 40));
                graphicsTarget.DrawLine(blackPen, new PointF(10, 70), new PointF(50, 70));

                documentWriter.EndPage(graphicsTarget);
                documentWriter.Save();

                string pdfContents = Encoding.Latin1.GetString(File.ReadAllBytes(pdfFileName));
                System.Text.RegularExpressions.MatchCollection dictionaries =
                    System.Text.RegularExpressions.Regex.Matches(pdfContents, @"<<[^<>]*/OP (true|false)[^<>]*>>");
                Assert.That(dictionaries.Count, Is.GreaterThan(0));
                foreach (System.Text.RegularExpressions.Match dictionary in dictionaries)
                    Assert.That(dictionary.Value, Does.Contain("/op "), "Stroke ExtGState without explicit /op: " + dictionary.Value);
                Assert.That(pdfContents, Does.Contain("/OP false"), "Switching stroke overprint off must be explicit.");
            }
            finally {
                if (File.Exists(pdfFileName))
                    File.Delete(pdfFileName);
            }
        }

        /// <summary>
        /// A white colour flagged as overprint in the map must not be written with /OP: with /OPM 1 a
        /// colour without ink leaves every separation untouched, so the white mask would vanish.
        /// </summary>
        [Test]
        public void WhiteOverprintColour_IsWrittenAsKnockout()
        {
            string pdfFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".pdf");
            try {
                IPdfWriter pdfWriter = new PdfWriter();
                IPdfDocumentWriter documentWriter = pdfWriter.CreateDocument(pdfFileName, "White overprint test", true);
                IGraphicsTarget graphicsTarget = documentWriter.BeginPage(new SizeF(2, 2));
                SymColor white = new SymColor(SymLayer.Normal);
                white.SetCMYK(0, 0, 0, 0);
                white.OverPrint = true;
                object brushKey = white.GetBrushKey(graphicsTarget);
                graphicsTarget.FillRectangle(brushKey, new RectangleF(10, 10, 40, 40));
                documentWriter.EndPage(graphicsTarget);
                documentWriter.Save();

                string pdfContents = Encoding.Latin1.GetString(File.ReadAllBytes(pdfFileName));
                Assert.That(pdfContents, Does.Not.Contain("/op true"));
                Assert.That(pdfContents, Does.Not.Contain("/OP true"));
            }
            finally {
                if (File.Exists(pdfFileName))
                    File.Delete(pdfFileName);
            }
        }
    }
}
