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
    }
}
