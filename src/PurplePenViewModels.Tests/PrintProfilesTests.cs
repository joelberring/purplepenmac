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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PurplePen;
using PurplePen.ViewModels;

namespace PurplePenViewModels.Tests
{
    /// <summary>Tests the versioned built-in BL print profile definitions.</summary>
    [TestFixture]
    public class PrintProfilesTests
    {
        /// <summary>On-screen colour swatches provide a stable, bounded CMYK approximation for mapping review.</summary>
        [Test]
        public void MappingPreview_ApproximatesCmykWithoutChangingProfileData()
        {
            PrintProfileCmyk black = new PrintProfileCmyk { Cyan = 0, Magenta = 0, Yellow = 0, Black = 100 };
            PrintProfileCmyk white = new PrintProfileCmyk { Cyan = -10, Magenta = 0, Yellow = 0, Black = 0 };

            Assert.That(PrintProfileMappingsDialogViewModel.ToPreviewColor(black), Is.EqualTo("#000000"));
            Assert.That(PrintProfileMappingsDialogViewModel.ToPreviewColor(white), Is.EqualTo("#FFFFFF"));
        }

        /// <summary>Forest profile preserves BL's transparent violet requirements.</summary>
        [Test]
        public void BlForest_HasTransparentVioletValuesAndLayerRelationships()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            PrintProfileColorRule transparentViolet = profile.ColorRules.Single(rule => rule.Id == "violet-transparent");

            Assert.That(profile.Id, Is.EqualTo("bl-forest-2026-08-27"));
            Assert.That(profile.SchemaVersion, Is.EqualTo(PrintProfile.CurrentSchemaVersion));
            Assert.That(profile.MapKind, Is.EqualTo(PrintProfileMapKind.Forest));
            Assert.That(profile.SourceUrl, Is.EqualTo("https://www.bl-idrottsservice.se/farginstallningar-kartnormen/"));
            Assert.That(transparentViolet.EffectiveCmyk.Cyan, Is.EqualTo(35));
            Assert.That(transparentViolet.EffectiveCmyk.Magenta, Is.EqualTo(95));
            Assert.That(transparentViolet.OverprintIntent, Is.EqualTo(PrintProfileOverprintIntent.Overprint));
            Assert.That(transparentViolet.MustBeBelowRuleIds, Is.EquivalentTo(new string[] { "brown-contours", "black", "blue-area" }));
            Assert.That(transparentViolet.MustBeAboveRuleIds, Is.EquivalentTo(new string[] { "blue-70", "blue-50" }));
        }

        /// <summary>Sprint profile preserves the current BL colour table values.</summary>
        [Test]
        public void BlSprint_HasExpectedCourseVioletAndYellowValues()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlSprint();
            PrintProfileColorRule courseViolet = profile.ColorRules.Single(rule => rule.Id == "course-violet");
            PrintProfileColorRule yellow = profile.ColorRules.Single(rule => rule.Id == "yellow");

            Assert.That(profile.Id, Is.EqualTo("bl-sprint-2025-05-01"));
            Assert.That(profile.MapKind, Is.EqualTo(PrintProfileMapKind.Sprint));
            Assert.That(courseViolet.EffectiveCmyk.Magenta, Is.EqualTo(100));
            Assert.That(courseViolet.OverprintIntent, Is.EqualTo(PrintProfileOverprintIntent.Overprint));
            Assert.That(yellow.EffectiveCmyk.Cyan, Is.EqualTo(0));
            Assert.That(yellow.EffectiveCmyk.Magenta, Is.EqualTo(28));
            Assert.That(yellow.EffectiveCmyk.Yellow, Is.EqualTo(95));
        }

        /// <summary>Each profile exposes the explicit rule used for export-only course markings.</summary>
        [Test]
        public void GetCourseColorRule_ReturnsProfileSpecificCourseRule()
        {
            PrintProfile forest = BuiltInPrintProfiles.CreateBlForest();
            PrintProfile sprint = BuiltInPrintProfiles.CreateBlSprint();
            PrintProfileColorRule forestRule = BuiltInPrintProfiles.GetCourseColorRule(forest);
            PrintProfileColorRule sprintRule = BuiltInPrintProfiles.GetCourseColorRule(sprint);

            Assert.That(forestRule.Id, Is.EqualTo("violet-transparent"));
            Assert.That(forestRule.Identifier.OcadIds, Is.EqualTo(new List<short> { 52 }));
            Assert.That(sprintRule.Id, Is.EqualTo("course-violet"));
            Assert.That(sprintRule.EffectiveCmyk.Magenta, Is.EqualTo(100));
        }

        /// <summary>Profile JSON is portable without losing metadata or profile rules.</summary>
        [Test]
        public void SerializeDeserialize_PreservesProfileMetadataAndRules()
        {
            PrintProfile original = BuiltInPrintProfiles.CreateBlForest();
            string json = PrintProfileSerializer.Serialize(original);
            PrintProfile restored = PrintProfileSerializer.Deserialize(json);
            PrintProfileColorRule restoredRule = restored.ColorRules.Single(rule => rule.Id == "violet-transparent");

            Assert.That(restored.Id, Is.EqualTo(original.Id));
            Assert.That(restored.SchemaVersion, Is.EqualTo(PrintProfile.CurrentSchemaVersion));
            Assert.That(restored.SourceDescription, Is.EqualTo(original.SourceDescription));
            Assert.That(restored.Requirements.RequireCmykOutput, Is.EqualTo(original.Requirements.RequireCmykOutput));
            Assert.That(restored.Requirements.RequireTrueOverprint, Is.EqualTo(original.Requirements.RequireTrueOverprint));
            Assert.That(restored.ColorRules.Count, Is.EqualTo(original.ColorRules.Count));
            Assert.That(restoredRule.Identifier.OcadIds, Is.EqualTo(new List<short> { 52 }));
            Assert.That(restoredRule.MustBeBelowRuleIds, Does.Contain("brown-contours"));
        }

        /// <summary>Unknown profile schema versions must not be interpreted with current rules.</summary>
        [Test]
        public void Deserialize_UnknownSchemaVersion_Throws()
        {
            string json = "{\"SchemaVersion\":99,\"Id\":\"future-profile\"}";

            Assert.That(() => PrintProfileSerializer.Deserialize(json), Throws.ArgumentException);
        }

        /// <summary>Confirmed forest-colour matches respect BL's transparent violet layer requirements.</summary>
        [Test]
        public void Analyze_ConfirmedForestColoursInExpectedOrder_CanExport()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> sourceColors = new List<SourceMapColor> {
                CreateSourceColor("Blå 70%", 0),
                CreateSourceColor("Blå 50%", 1),
                CreateSourceColor("Violett transparent", 2, 52),
                CreateSourceColor("Brun för höjdkurvor mm", 3),
                CreateSourceColor("Svart", 4),
                CreateSourceColor("Blå yta", 5),
            };

            ColorPreflightReport report = ColorPreflightReport.Analyze(profile, sourceColors, PdfExportCapabilities.CreateCurrentImplementation());
            ColorPreflightRuleResult violetResult = report.RuleResults.Single(result => result.Rule.Id == "violet-transparent");

            Assert.That(violetResult.MatchStatus, Is.EqualTo(ColorPreflightMatchStatus.Matched));
            Assert.That(report.CanExport, Is.True);
            Assert.That(report.Findings.Any(finding => finding.RuleId == "violet-transparent" && finding.Message.Contains("should be")), Is.False);
        }

        /// <summary>Partial name-only matches require an explicit confirmation before export.</summary>
        [Test]
        public void Analyze_PartialIdentifierMatch_BlocksExport()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> sourceColors = new List<SourceMapColor> {
                CreateSourceColor("Violett transparent", 0, 999),
            };

            ColorPreflightReport report = ColorPreflightReport.Analyze(profile, sourceColors, PdfExportCapabilities.CreateCurrentImplementation());
            ColorPreflightRuleResult violetResult = report.RuleResults.Single(result => result.Rule.Id == "violet-transparent");

            Assert.That(violetResult.MatchStatus, Is.EqualTo(ColorPreflightMatchStatus.NeedsConfirmation));
            Assert.That(report.CanExport, Is.False);
            Assert.That(report.Findings.Any(finding => finding.RuleId == "violet-transparent" && finding.Severity == ColorPreflightSeverity.Blocking), Is.True);
        }

        /// <summary>An explicit user confirmation permits a single partial colour match without guessing.</summary>
        [Test]
        public void Analyze_UserConfirmedPartialIdentifierMatch_AllowsExport()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> sourceColors = new List<SourceMapColor> {
                CreateSourceColor("Violett transparent", 0, 999),
            };
            List<string> confirmedRules = new List<string> { "violet-transparent" };

            ColorPreflightReport report = ColorPreflightReport.Analyze(profile, sourceColors,
                PdfExportCapabilities.CreateCurrentImplementation(), confirmedRules);
            ColorPreflightRuleResult violetResult = report.RuleResults.Single(result => result.Rule.Id == "violet-transparent");

            Assert.That(violetResult.UserConfirmed, Is.True);
            Assert.That(violetResult.HasConfirmedMatch, Is.True);
            Assert.That(report.CanExport, Is.True);
            Assert.That(report.Findings.Any(finding => finding.RuleId == "violet-transparent" && finding.Severity == ColorPreflightSeverity.Warning), Is.True);
        }

        /// <summary>An explicit mapping resolves profile rules with multiple identically named source colours.</summary>
        [Test]
        public void Analyze_ExplicitColorMapping_ResolvesAmbiguousRule()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> sourceColors = new List<SourceMapColor> {
                CreateSourceColor("Svart", 3, 201),
                CreateSourceColor("Svart", 4, 202),
            };
            List<PrintProfileColorMapping> mappings = new List<PrintProfileColorMapping> {
                new PrintProfileColorMapping { RuleId = "black", MapColorName = "Svart", MapColorOcadId = 202, MapColorDrawOrder = 4 },
            };

            ColorPreflightReport report = ColorPreflightReport.Analyze(profile, sourceColors,
                PdfExportCapabilities.CreateCurrentImplementation(), null, mappings);
            ColorPreflightRuleResult blackResult = report.RuleResults.Single(result => result.Rule.Id == "black");

            Assert.That(blackResult.UserMapped, Is.True);
            Assert.That(blackResult.MatchStatus, Is.EqualTo(ColorPreflightMatchStatus.Matched));
            Assert.That(blackResult.CandidateColors.Single().OcadId, Is.EqualTo(202));
            Assert.That(report.CanExport, Is.True);
        }

        /// <summary>A stale saved mapping stops export rather than silently selecting another map colour.</summary>
        [Test]
        public void Analyze_StaleExplicitColorMapping_BlocksExport()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<PrintProfileColorMapping> mappings = new List<PrintProfileColorMapping> {
                new PrintProfileColorMapping { RuleId = "black", MapColorName = "Svart", MapColorOcadId = 202, MapColorDrawOrder = 4 },
            };

            ColorPreflightReport report = ColorPreflightReport.Analyze(profile, new List<SourceMapColor>(),
                PdfExportCapabilities.CreateCurrentImplementation(), null, mappings);
            ColorPreflightRuleResult blackResult = report.RuleResults.Single(result => result.Rule.Id == "black");

            Assert.That(blackResult.MappingMissing, Is.True);
            Assert.That(report.CanExport, Is.False);
        }

        /// <summary>Unlabelled zero-ink colours are not guessed as a profile's white rule.</summary>
        [Test]
        public void Analyze_UnlabelledWhiteColor_IsReportedAsNotPresent()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> sourceColors = new List<SourceMapColor> {
                new SourceMapColor {
                    Name = "Bakgrund",
                    DrawOrder = 0,
                    Cmyk = new PrintProfileCmyk(),
                },
            };

            ColorPreflightReport report = ColorPreflightReport.Analyze(profile, sourceColors,
                PdfExportCapabilities.CreateCurrentImplementation());
            ColorPreflightRuleResult whiteResult = report.RuleResults.Single(result => result.Rule.Id == "white");

            Assert.That(whiteResult.MatchStatus, Is.EqualTo(ColorPreflightMatchStatus.NotPresent));
            Assert.That(report.Findings.Any(finding => finding.RuleId == "white" && finding.Severity == ColorPreflightSeverity.Information), Is.True);
        }

        /// <summary>CMYK deviations are reported while leaving the source map untouched.</summary>
        [Test]
        public void Analyze_MatchingColourWithDifferentCmyk_ReportsWarning()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> sourceColors = new List<SourceMapColor> {
                new SourceMapColor {
                    Name = "Violett transparent",
                    OcadId = 52,
                    DrawOrder = 0,
                    Cmyk = new PrintProfileCmyk { Cyan = 1, Magenta = 2, Yellow = 3, Black = 4 },
                },
            };

            ColorPreflightReport report = ColorPreflightReport.Analyze(profile, sourceColors, PdfExportCapabilities.CreateCurrentImplementation());

            Assert.That(report.Findings.Any(finding => finding.RuleId == "violet-transparent" && finding.Message.Contains("CMYK")), Is.True);
        }

        /// <summary>A missing true-overprint capability remains blocking for a custom profile that requires it.</summary>
        [Test]
        public void Analyze_RequiredTrueOverprintWithoutCapability_BlocksExport()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlSprint();
            profile.Requirements.RequireTrueOverprint = true;

            PdfExportCapabilities missingOverprint = new PdfExportCapabilities {
                SupportsCmykOutput = true,
            };
            ColorPreflightReport report = ColorPreflightReport.Analyze(profile, new List<SourceMapColor>(), missingOverprint);

            Assert.That(report.CanExport, Is.False);
            Assert.That(report.Findings.Any(finding => finding.Message.Contains("True PDF overprint")), Is.True);
        }

        /// <summary>The current PDF implementation advertises only the structurally verified overprint feature.</summary>
        [Test]
        public void CurrentPdfCapabilities_ReportsVerifiedOverprintButNotPdfXOrIcc()
        {
            PdfExportCapabilities capabilities = PdfExportCapabilities.CreateCurrentImplementation();

            Assert.That(capabilities.SupportsCmykOutput, Is.True);
            Assert.That(capabilities.SupportsTrueOverprint, Is.True);
            Assert.That(capabilities.SupportsPdfXOutputIntent, Is.False);
            Assert.That(capabilities.SupportsEmbeddedIccProfile, Is.False);
        }

        /// <summary>Selecting a print profile uses its stable ID and forces CMYK PDF export.</summary>
        [Test]
        public void PdfDialog_PrintProfileSelection_StoresProfileIdAndUsesCmyk()
        {
            CreatePdfCoursesDialogViewModel viewModel = new CreatePdfCoursesDialogViewModel();
            viewModel.ColorModelIndex = 0;
            viewModel.PrintProfileIndex = 1;

            CoursePdfSettings settings = viewModel.Settings;

            Assert.That(settings.PrintProfileId, Is.EqualTo("bl-forest-2026-08-27"));
            Assert.That(settings.ColorModel, Is.EqualTo(ColorModel.CMYK));
        }

        /// <summary>Selecting a profile exposes its colour rules for the visual export review.</summary>
        [Test]
        public void PdfDialog_PrintProfileSelection_BuildsColourPreview()
        {
            CreatePdfCoursesDialogViewModel viewModel = new CreatePdfCoursesDialogViewModel();
            viewModel.PrintProfileIndex = 1;

            Assert.That(viewModel.HasSelectedPrintProfile, Is.True);
            Assert.That(viewModel.SelectedPrintProfileIsForest, Is.True);
            Assert.That(viewModel.SelectedPrintProfileRules.Count, Is.GreaterThan(0));
            Assert.That(viewModel.SelectedPrintProfileRules.Any(rule => rule.UsesOverprint), Is.True);
        }

        /// <summary>A copied profile retains the source profile's course-marking colour rule.</summary>
        [Test]
        public void PrintProfileEditor_CopyPreservesCourseColourRule()
        {
            PrintProfileEditorDialogViewModel viewModel = new PrintProfileEditorDialogViewModel();
            PrintProfile source = BuiltInPrintProfiles.CreateBlForest();

            viewModel.LoadCopy(source);
            PrintProfile copy = viewModel.CreateProfile();

            Assert.That(copy.CourseColorRuleId, Is.EqualTo("violet-transparent"));
            Assert.That(BuiltInPrintProfiles.GetCourseColorRule(copy).Name, Is.EqualTo("Violett transparent"));
        }

        /// <summary>A user-owned copy retains the source provenance and output requirements.</summary>
        [Test]
        public void PrintProfileEditor_CopyPreservesSourceMetadataAndRequirements()
        {
            PrintProfileEditorDialogViewModel viewModel = new PrintProfileEditorDialogViewModel();
            PrintProfile source = BuiltInPrintProfiles.CreateBlForest();
            source.Requirements.RequirePdfXOutputIntent = true;
            source.Requirements.RequireEmbeddedIccProfile = true;

            viewModel.LoadCopy(source);
            PrintProfile copy = viewModel.CreateProfile();

            Assert.That(copy.SourceUrl, Is.EqualTo(source.SourceUrl));
            Assert.That(copy.SourceDescription, Is.EqualTo(source.SourceDescription));
            Assert.That(copy.Requirements.RequirePdfXOutputIntent, Is.True);
            Assert.That(copy.Requirements.RequireEmbeddedIccProfile, Is.True);
            Assert.That(copy.Requirements, Is.Not.SameAs(source.Requirements));
        }

        /// <summary>Malformed profile JSON with missing nested requirements is rejected before it is persisted.</summary>
        [Test]
        public void PrintProfileCatalog_RejectsNullRequirements()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            profile.Requirements = null!;
            string fileName = Path.Combine(Path.GetTempPath(), "purplepen-invalid-profile-" + System.Guid.NewGuid().ToString("N") + ".json");
            try {
                File.WriteAllText(fileName, PrintProfileSerializer.Serialize(profile));

                Assert.That(() => PrintProfileCatalog.Import(fileName), Throws.TypeOf<System.ArgumentException>());
            }
            finally {
                if (File.Exists(fileName))
                    File.Delete(fileName);
            }
        }

        /// <summary>The editor exposes all profiles and loads the selected source rather than always using the first one.</summary>
        [Test]
        public void PrintProfileEditor_CanChooseSourceProfile()
        {
            PrintProfileEditorDialogViewModel viewModel = new PrintProfileEditorDialogViewModel();
            viewModel.LoadProfiles(PrintProfileCatalog.CreateAll(), "bl-sprint-2025-05-01");

            Assert.That(viewModel.SelectedSourceProfileId, Is.EqualTo("bl-sprint-2025-05-01"));
            Assert.That(viewModel.Name, Does.StartWith("BL sprint"));
            Assert.That(viewModel.SourceProfiles.Any(profile => profile.Id == "bl-forest-2026-08-27"), Is.True);
        }

        /// <summary>A saved copy has a unique ID and is discoverable without shadowing a built-in profile.</summary>
        [Test]
        public void PrintProfileEditor_SavedCopyIsDiscoverable()
        {
            PrintProfileEditorDialogViewModel viewModel = new PrintProfileEditorDialogViewModel();
            viewModel.LoadCopy(BuiltInPrintProfiles.CreateBlForest());
            PrintProfile copy = viewModel.CreateProfile();
            string fileName = Path.Combine(PrintProfileCatalog.UserProfileDirectory, copy.Id + ".json");
            try {
                PrintProfileCatalog.SaveUserProfile(copy);
                Assert.That(copy.Id, Is.Not.EqualTo("bl-forest-2026-08-27"));
                Assert.That(PrintProfileCatalog.FindById(copy.Id)?.Name, Is.EqualTo(copy.Name));
                Assert.That(PrintProfileCatalog.FindById("bl-forest-2026-08-27")?.Name, Is.EqualTo("BL skog"));
            }
            finally {
                if (File.Exists(fileName))
                    File.Delete(fileName);
            }
        }

        /// <summary>The editor lets a user choose another retained rule for course markings.</summary>
        [Test]
        public void PrintProfileEditor_ChangesCourseColourRuleWhenSelected()
        {
            PrintProfileEditorDialogViewModel viewModel = new PrintProfileEditorDialogViewModel();
            viewModel.LoadCopy(BuiltInPrintProfiles.CreateBlForest());
            viewModel.CourseColorRuleIndex = 0;

            PrintProfile copy = viewModel.CreateProfile();

            Assert.That(copy.CourseColorRuleId, Is.EqualTo("violet-opaque"));
        }

        /// <summary>The editor adds a custom rule with multiple map-colour names for non-destructive matching.</summary>
        [Test]
        public void PrintProfileEditor_AddRule_PersistsMultipleMapColourNames()
        {
            PrintProfileEditorDialogViewModel viewModel = new PrintProfileEditorDialogViewModel();
            viewModel.LoadCopy(BuiltInPrintProfiles.CreateBlForest());

            viewModel.AddRuleCommand.Execute(null);
            PrintProfileRuleEditorItem addedRule = viewModel.SelectedRule!;
            addedRule.Name = "Custom purple";
            addedRule.MatchNames = "Custom purple; Course purple; custom purple";
            addedRule.Cyan = 12;

            PrintProfile copy = viewModel.CreateProfile();
            PrintProfileColorRule copiedRule = copy.ColorRules.Single(rule => rule.Id == addedRule.Id);

            Assert.That(copy.ColorRules.Count, Is.EqualTo(BuiltInPrintProfiles.CreateBlForest().ColorRules.Count + 1));
            Assert.That(copiedRule.Name, Is.EqualTo("Custom purple"));
            Assert.That(copiedRule.Identifier.Names, Is.EqualTo(new string[] { "Custom purple", "Course purple" }));
            Assert.That(copiedRule.EffectiveCmyk.Cyan, Is.EqualTo(12));
        }

        /// <summary>Removing the active course rule selects a retained rule and leaves no broken layer references.</summary>
        [Test]
        public void PrintProfileEditor_RemoveRule_RetainsValidCourseRuleAndRelationships()
        {
            PrintProfileEditorDialogViewModel viewModel = new PrintProfileEditorDialogViewModel();
            PrintProfile source = BuiltInPrintProfiles.CreateBlForest();
            source.ColorRules.First().MustBeAboveRuleIds.Add("violet-transparent");
            viewModel.LoadCopy(source);
            viewModel.SelectedRule = viewModel.Rules.Single(rule => rule.Id == "violet-transparent");

            viewModel.RemoveSelectedRuleCommand.Execute(null);
            PrintProfile copy = viewModel.CreateProfile();

            Assert.That(copy.ColorRules.Any(rule => rule.Id == "violet-transparent"), Is.False);
            Assert.That(copy.CourseColorRuleId, Is.Not.EqualTo("violet-transparent"));
            Assert.That(copy.ColorRules.SelectMany(rule => rule.MustBeAboveRuleIds.Concat(rule.MustBeBelowRuleIds)), Does.Not.Contain("violet-transparent"));
        }

        /// <summary>IOF XML start lists provide class and competitor data for production planning.</summary>
        [Test]
        public void IofStartList_ImportsClassAndCompetitor()
        {
            const string xml = "<?xml version=\"1.0\"?><StartList xmlns=\"http://www.orienteering.org/datastandard/3.0\" iofVersion=\"3.0\"><ClassStart><Class><Name>D21</Name></Class><PersonStart><Person><Name><Family>Andersson</Family><Given>Anna</Given></Name></Person><Start><BibNumber>42</BibNumber></Start></PersonStart></ClassStart></StartList>";

            List<BacksideInfoRecord> records = BacksideInfoIofXml.Import(new StringReader(xml));

            Assert.That(records.Count, Is.EqualTo(1));
            Assert.That(records[0].ClassName, Is.EqualTo("D21"));
            Assert.That(records[0].Name, Is.EqualTo("Anna Andersson"));
            Assert.That(records[0].Course, Is.Empty);
        }

        /// <summary>Stored profile IDs restore the corresponding PDF-dialog selection.</summary>
        [Test]
        public void PdfDialog_SettingsWithProfileId_RestoresProfileSelection()
        {
            CreatePdfCoursesDialogViewModel viewModel = new CreatePdfCoursesDialogViewModel();
            CoursePdfSettings settings = new CoursePdfSettings {
                PrintProfileId = "bl-sprint-2025-05-01",
                ColorModel = ColorModel.RGB,
            };

            viewModel.Settings = settings;

            Assert.That(viewModel.PrintProfileIndex, Is.EqualTo(2));
            Assert.That(viewModel.ColorModelIndex, Is.EqualTo(1));
        }

        /// <summary>Confirmed mappings survive the dialog's settings conversion.</summary>
        [Test]
        public void PdfDialog_ConfirmedProfileRules_RoundTripThroughSettings()
        {
            CreatePdfCoursesDialogViewModel viewModel = new CreatePdfCoursesDialogViewModel {
                ConfirmedPrintProfileRuleIds = new List<string> { "violet-transparent" },
            };

            CoursePdfSettings settings = viewModel.Settings;

            Assert.That(settings.ConfirmedPrintProfileRuleIds, Is.EqualTo(new List<string> { "violet-transparent" }));
        }

        /// <summary>The mapping dialog creates a stable identity record for a selected source-map colour.</summary>
        [Test]
        public void MappingDialog_CreateMappings_StoresSelectedSourceColorIdentity()
        {
            SourceMapColor sourceColor = CreateSourceColor("Svart", 4, 202);
            PrintProfileColorRule rule = new PrintProfileColorRule { Id = "black", Name = "Svart" };
            PrintProfile profile = new PrintProfile {
                Name = "Test",
                ColorRules = new List<PrintProfileColorRule> { rule },
            };
            ColorPreflightRuleResult result = new ColorPreflightRuleResult {
                Rule = rule,
                CandidateColors = new List<SourceMapColor> { sourceColor },
                MatchStatus = ColorPreflightMatchStatus.Matched,
            };
            PrintProfileMappingsDialogViewModel viewModel = new PrintProfileMappingsDialogViewModel();
            viewModel.Configure(profile, new[] { sourceColor }, new[] { result });

            List<PrintProfileColorMapping> mappings = viewModel.CreateMappings();

            Assert.That(viewModel.CanApplyMappings, Is.True);
            Assert.That(mappings.Single().MapColorOcadId, Is.EqualTo(202));
            Assert.That(mappings.Single().MapColorDrawOrder, Is.EqualTo(4));
        }

        /// <summary>Unmatched source layers explicitly keep their original colour instead of appearing blank.</summary>
        [Test]
        public void MappingDialog_UnpairedLayerDefaultsToKeepOriginal()
        {
            SourceMapColor sourceColor = CreateSourceColor("Extra green", 6, 306);
            PrintProfile profile = new PrintProfile {
                Name = "Test",
                ColorRules = new List<PrintProfileColorRule> { new PrintProfileColorRule { Id = "black", Name = "Black" } },
            };
            PrintProfileMappingsDialogViewModel viewModel = new PrintProfileMappingsDialogViewModel();

            viewModel.Configure(profile, new[] { sourceColor }, Array.Empty<ColorPreflightRuleResult>());

            Assert.That(viewModel.SourceLayers.Single().SelectedTarget!.IsKeepOriginal, Is.True);
            Assert.That(viewModel.CanApplyMappings, Is.True);
            Assert.That(viewModel.CreateMappings(), Is.Empty);
        }

        /// <summary>An ambiguous profile colour requires one source-first choice and cannot be assigned twice.</summary>
        [Test]
        public void MappingDialog_AmbiguousLayerRequiresExplicitUniqueTarget()
        {
            SourceMapColor first = CreateSourceColor("Brown A", 2, 101);
            SourceMapColor second = CreateSourceColor("Brown B", 3, 102);
            PrintProfileColorRule rule = new PrintProfileColorRule { Id = "brown", Name = "Brown contours" };
            PrintProfile profile = new PrintProfile { Name = "Test", ColorRules = new List<PrintProfileColorRule> { rule } };
            ColorPreflightRuleResult result = new ColorPreflightRuleResult {
                Rule = rule,
                CandidateColors = new List<SourceMapColor> { first, second },
                MatchStatus = ColorPreflightMatchStatus.Ambiguous,
            };
            PrintProfileMappingsDialogViewModel viewModel = new PrintProfileMappingsDialogViewModel();
            viewModel.Configure(profile, new[] { first, second }, new[] { result });

            Assert.That(viewModel.CanApplyMappings, Is.False);
            PrintProfileTargetOptionViewModel target = viewModel.SourceLayers[0].TargetOptions.Single(option => option.RuleId == "brown");
            viewModel.SourceLayers[0].SelectedTarget = target;
            viewModel.SourceLayers[1].SelectedTarget = viewModel.SourceLayers[1].TargetOptions.Single(option => option.IsKeepOriginal);

            Assert.That(viewModel.CanApplyMappings, Is.True);
            Assert.That(viewModel.CreateMappings().Single().MapColorName, Is.EqualTo("Brown A"));
        }

        /// <summary>Visual editor pairings replace fragile source names with the selected map colour identity.</summary>
        [Test]
        public void PrintProfileEditor_ApplyMapPairing_UsesSelectedNameAndOcadId()
        {
            SourceMapColor mapColor = CreateSourceColor("Kartans bruna", 5, 417);
            PrintProfileEditorDialogViewModel viewModel = new PrintProfileEditorDialogViewModel();
            viewModel.LoadCurrentMap(new[] { mapColor });
            viewModel.LoadCopy(BuiltInPrintProfiles.CreateBlForest());

            viewModel.ApplyMapPairings(new[] {
                new PrintProfileColorMapping {
                    RuleId = "brown-contours",
                    MapColorName = mapColor.Name,
                    MapColorOcadId = mapColor.OcadId,
                    MapColorDrawOrder = mapColor.DrawOrder,
                },
            });

            PrintProfileColorRule pairedRule = viewModel.CreateProfile().ColorRules.Single(rule => rule.Id == "brown-contours");
            Assert.That(pairedRule.Identifier.Names, Is.EqualTo(new[] { "Kartans bruna" }));
            Assert.That(pairedRule.Identifier.OcadIds, Is.EqualTo(new short[] { 417 }));
        }

        [Test]
        public void Editor_CurrentMapSeedAndLayerMove_PreserveConcreteValues()
        {
            PrintProfileEditorDialogViewModel viewModel = new PrintProfileEditorDialogViewModel();
            viewModel.LoadCurrentMap(new[] { CreateSourceColor("Brown", 2, 101), CreateSourceColor("Black", 3, 102) });

            Assert.That(viewModel.SourceProfiles[0].IsCurrentMap, Is.True);
            Assert.That(viewModel.Rules[0].Cyan, Is.EqualTo(1));
            viewModel.SelectedRule = viewModel.Rules[1];
            viewModel.MoveRuleUpCommand.Execute(null);
            PrintProfile profile = viewModel.CreateProfile();

            Assert.That(profile.ColorRules.Select(rule => rule.Name), Is.EqualTo(new[] { "Black", "Brown" }));
            Assert.That(profile.ColorRules.Select(rule => rule.RelativeDrawOrder), Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void Editor_CurrentMapSeedRequiresNameBeforeSave()
        {
            PrintProfileEditorDialogViewModel viewModel = new PrintProfileEditorDialogViewModel();
            viewModel.LoadCurrentMap(new[] { CreateSourceColor("Brown", 2, 101) });

            Assert.That(viewModel.ValidateForSave(), Is.False);
            Assert.That(viewModel.ValidationMessageKey, Is.EqualTo("PrintProfileEditorDialog_ErrorNameRequired"));
            viewModel.Name = "Current map copy";
            Assert.That(viewModel.ValidateForSave(), Is.True);
        }

        /// <summary>Selecting Current map again discards metadata inherited from a previously selected profile.</summary>
        [Test]
        public void Editor_SelectingCurrentMapAgain_ResetsProfileOnlyState()
        {
            PrintProfile source = BuiltInPrintProfiles.CreateBlSprint();
            source.PrinterName = "Production printer";
            source.PaperSpecification = "A3";
            source.IccProfile = "printing.icc";
            source.SourceUrl = "https://example.invalid/profile";
            source.SourceDescription = "Imported requirements";
            source.Requirements.RequireCmykOutput = true;
            source.Requirements.RequireEmbeddedIccProfile = true;
            source.Requirements.RequireTrueOverprint = true;
            source.History.Add(new PrintProfileHistoryEntry { VersionDate = "2026-01-01", Summary = "Imported" });
            PrintProfileEditorDialogViewModel viewModel = new PrintProfileEditorDialogViewModel();

            viewModel.LoadProfiles(new[] { source }, source.Id, true);
            viewModel.LoadCurrentMap(new[] { CreateSourceColor("Brown", 2, 101) });
            viewModel.SelectedSourceProfileId = source.Id;
            viewModel.SelectedSourceProfileId = "__current-map__";
            PrintProfile mapProfile = viewModel.CreateProfile();

            Assert.That(mapProfile.PrinterName, Is.Empty);
            Assert.That(mapProfile.PaperSpecification, Is.Empty);
            Assert.That(mapProfile.IccProfile, Is.Empty);
            Assert.That(mapProfile.SourceUrl, Is.Empty);
            Assert.That(mapProfile.SourceDescription, Is.Empty);
            Assert.That(mapProfile.Requirements.RequireCmykOutput, Is.True, "Current map retains the neutral default requirement.");
            Assert.That(mapProfile.Requirements.RequireEmbeddedIccProfile, Is.False);
            Assert.That(mapProfile.Requirements.RequireTrueOverprint, Is.False);
            Assert.That(mapProfile.History, Is.Empty);
            Assert.That(mapProfile.ColorRules.Single().Name, Is.EqualTo("Brown"));
        }

        [Test]
        public void ProfileHistory_RoundTripsDiffFields()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlSprint();
            profile.History.Add(new PrintProfileHistoryEntry { VersionDate = "2026-01-01", Summary = "Rules +1/-0/~1", AddedRules = new List<string> { "New" }, ChangedRules = new List<string> { "Blue" }, MetadataChanged = true, LayerOrderChanged = true });
            PrintProfile restored = PrintProfileSerializer.Deserialize(PrintProfileSerializer.Serialize(profile));

            Assert.That(restored.History[0].AddedRules, Is.EqualTo(new[] { "New" }));
            Assert.That(restored.History[0].ChangedRules, Is.EqualTo(new[] { "Blue" }));
            Assert.That(restored.History[0].MetadataChanged, Is.True);
            Assert.That(restored.History[0].LayerOrderChanged, Is.True);
        }

        /// <summary>Creates a minimal, read-only source-map colour snapshot for preflight tests.</summary>
        private static SourceMapColor CreateSourceColor(string name, int drawOrder, short ocadId = 0)
        {
            return new SourceMapColor {
                Name = name,
                OcadId = ocadId,
                DrawOrder = drawOrder,
                Cmyk = new PrintProfileCmyk { Cyan = 1, Magenta = 2, Yellow = 3, Black = 4 },
            };
        }
    }
}
