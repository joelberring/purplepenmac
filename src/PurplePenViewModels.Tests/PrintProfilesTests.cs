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
            // BL: under brown, black and 100 % blue lines; directly above "Blå yta" and all other area colours.
            Assert.That(transparentViolet.MustBeBelowRuleIds, Is.EquivalentTo(new string[] { "brown-contours", "black", "blue-line-point" }));
            Assert.That(transparentViolet.MustBeAboveRuleIds, Does.Contain("blue-area"));
            Assert.That(transparentViolet.MustBeAboveRuleIds, Does.Contain("blue-70"));
            Assert.That(transparentViolet.MustBeAboveRuleIds, Does.Contain("blue-50"));
            Assert.That(transparentViolet.MustBeBelowRuleIds.Intersect(transparentViolet.MustBeAboveRuleIds), Is.Empty);
            Assert.That(profile.UpperCourseColorRuleId, Is.EqualTo("violet-opaque"));
            Assert.That(profile.Requirements.ForbidRegistrationColor, Is.True);
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
                CreateSourceColor("Blå yta", 2),
                CreateSourceColor("Violett transparent", 3, 52),
                CreateSourceColor("Brun för höjdkurvor mm", 4),
                CreateSourceColor("Svart", 5),
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

        /// <summary>Unmatched source layers keep their original colour, and the unused rule is recorded as not in the map.</summary>
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
            Assert.That(viewModel.SourceLayers.Single().IsAutoKeptOriginal, Is.True);
            Assert.That(viewModel.CanApplyMappings, Is.True);
            PrintProfileColorMapping mapping = viewModel.CreateMappings().Single();
            Assert.That(mapping.RuleId, Is.EqualTo("black"));
            Assert.That(mapping.NotInMap, Is.True);
        }

        /// <summary>
        /// An ambiguous profile colour is suggested for one layer; choosing it for another layer moves it
        /// there instead of blocking Apply.
        /// </summary>
        [Test]
        public void MappingDialog_ChoosingUsedTargetMovesItAndKeepsApplyEnabled()
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

            Assert.That(viewModel.CanApplyMappings, Is.True);
            Assert.That(viewModel.SourceLayers[0].SelectedTarget!.RuleId, Is.EqualTo("brown"));
            Assert.That(viewModel.SourceLayers[0].IsAutoSuggested, Is.True);
            Assert.That(viewModel.SourceLayers[1].SelectedTarget!.IsKeepOriginal, Is.True);

            viewModel.SourceLayers[1].SelectedTarget = viewModel.SourceLayers[1].TargetOptions.Single(option => option.RuleId == "brown");

            Assert.That(viewModel.SourceLayers[0].SelectedTarget!.IsKeepOriginal, Is.True);
            Assert.That(viewModel.SourceLayers[1].IsAutoSuggested, Is.False);
            Assert.That(viewModel.CanApplyMappings, Is.True);
            Assert.That(viewModel.CreateMappings().Single().MapColorName, Is.EqualTo("Brown B"));
        }

        /// <summary>A map with BL's own colour names is paired completely and can then be exported.</summary>
        [Test]
        public void AutoMapper_BlNamedMap_PairsEveryLayerByName()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> map = CreateBlForestMapColors(includeTransparentViolet: true);
            ColorPreflightReport report = ColorPreflightReport.Analyze(profile, map, PdfExportCapabilities.CreateCurrentImplementation());

            List<PrintProfileMappingSuggestion> suggestions = PrintProfileAutoMapper.Suggest(profile, map, report.RuleResults);

            foreach (PrintProfileMappingSuggestion suggestion in suggestions) {
                Assert.That(suggestion.Rule, Is.Not.Null, suggestion.SourceColor.Name);
                Assert.That(suggestion.Rule!.Name, Is.EqualTo(suggestion.SourceColor.Name));
            }
        }

        /// <summary>English names, real CMYK values and ISOM symbols are enough to pair a typical map.</summary>
        [Test]
        public void AutoMapper_EnglishIsomMap_UsesNamesCmykAndSymbols()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> map = new List<SourceMapColor> {
                CreateCmykColor("Yellow 50%", 0, 0, 20, 44, 0, "403.0"),
                CreateCmykColor("Open land", 1, 0, 27, 94, 0, "401.0"),
                CreateCmykColor("Green 30%", 2, 20, 0, 26, 0, "406.0"),
                CreateCmykColor("Brown", 3, 0, 75, 100, 33, "101.0", "102.0"),
                CreateCmykColor("Blue", 4, 100, 0, 0, 0, "304.0", "305.0"),
                CreateCmykColor("Black", 5, 0, 0, 0, 100, "201.0"),
                CreateCmykColor("Company logo red", 6, 0, 100, 100, 0),
            };

            List<PrintProfileMappingSuggestion> suggestions = PrintProfileAutoMapper.Suggest(profile, map, null);
            Dictionary<string, string?> byName = suggestions.ToDictionary(item => item.SourceColor.Name, item => item.Rule?.Id);

            Assert.That(byName["Yellow 50%"], Is.EqualTo("yellow-50"));
            Assert.That(byName["Open land"], Is.EqualTo("yellow-100"));
            Assert.That(byName["Green 30%"], Is.EqualTo("green-30"));
            Assert.That(byName["Brown"], Is.EqualTo("brown-contours"));
            Assert.That(byName["Blue"], Is.EqualTo("blue-line-point"));
            Assert.That(byName["Black"], Is.EqualTo("black"));
            Assert.That(byName["Company logo red"], Is.Null);
            Assert.That(suggestions.Where(item => item.Rule != null).Select(item => item.Rule!.Id).Distinct().Count(),
                        Is.EqualTo(suggestions.Count(item => item.Rule != null)));
        }

        /// <summary>Applying the dialog's choices resolves every ambiguity, including rules no layer uses.</summary>
        [Test]
        public void MappingDialog_AppliedChoicesUnblockExport()
        {
            PrintProfileColorRule rule = new PrintProfileColorRule {
                Id = "brown", Name = "Brun",
                Identifier = new PrintProfileColorIdentifier { Names = new List<string> { "Brun" } },
            };
            PrintProfile profile = new PrintProfile { Name = "Test", ColorRules = new List<PrintProfileColorRule> { rule } };
            List<SourceMapColor> map = new List<SourceMapColor> { CreateSourceColor("Brun", 0, 10), CreateSourceColor("Brun", 1, 11) };
            ColorPreflightReport before = ColorPreflightReport.Analyze(profile, map, PdfExportCapabilities.CreateCurrentImplementation());
            Assert.That(before.CanExport, Is.False);

            // Keep both original colours: the rule is recorded as not in the map instead of staying ambiguous.
            PrintProfileMappingsDialogViewModel viewModel = new PrintProfileMappingsDialogViewModel();
            viewModel.Configure(profile, map, before.RuleResults);
            foreach (PrintProfileSourceLayerViewModel layer in viewModel.SourceLayers)
                layer.SelectedTarget = layer.KeepOriginalOption;
            ColorPreflightReport after = ColorPreflightReport.Analyze(profile, map, PdfExportCapabilities.CreateCurrentImplementation(),
                                                                      null, viewModel.CreateMappings());

            Assert.That(after.RuleResults.Single().MatchStatus, Is.EqualTo(ColorPreflightMatchStatus.NotPresent));
            Assert.That(after.RuleResults.Single().MappingMissing, Is.False);
            Assert.That(after.CanExport, Is.True);
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
        /// <summary>Every built-in profile's layer relationships agree with its own colour-table order.</summary>
        [Test]
        public void BuiltInProfiles_RelationshipsMatchRelativeDrawOrder()
        {
            foreach (PrintProfile profile in BuiltInPrintProfiles.CreateAll()) {
                foreach (PrintProfileColorRule rule in profile.ColorRules) {
                    foreach (string belowId in rule.MustBeBelowRuleIds) {
                        PrintProfileColorRule other = profile.ColorRules.Single(candidate => candidate.Id == belowId);
                        Assert.That(other.RelativeDrawOrder, Is.LessThan(rule.RelativeDrawOrder), profile.Id + ": " + rule.Id + " must be below " + belowId);
                    }
                    foreach (string aboveId in rule.MustBeAboveRuleIds) {
                        PrintProfileColorRule other = profile.ColorRules.Single(candidate => candidate.Id == aboveId);
                        Assert.That(other.RelativeDrawOrder, Is.GreaterThan(rule.RelativeDrawOrder), profile.Id + ": " + rule.Id + " must be above " + aboveId);
                    }
                }
            }
        }

        /// <summary>BL forest: the transparent course violet goes directly under brown and above "Blå yta".</summary>
        [Test]
        public void Layering_BlForestMap_PlacesLowerPurpleBetweenBlueAreaAndBrown()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> map = CreateBlForestMapColors(includeTransparentViolet: false);

            PrintProfileCourseLayering layering = PrintProfileCourseLayering.Create(profile, map, true, null, null, null);

            Assert.That(layering.Status, Is.EqualTo(PrintProfileLayeringStatus.FromProfileColorOrder));
            Assert.That(layering.IsInterleaved, Is.True);
            Assert.That(layering.LowerPurpleMapLayer, Is.EqualTo(map.Single(color => color.Name == "Blå yta").OcadId));
            Assert.That(layering.MapColorBelowLowerPurple, Is.EqualTo("Blå yta"));
            Assert.That(layering.MapColorAboveLowerPurple, Is.EqualTo("Brun för höjdkurvor mm"));
            Assert.That(layering.ConflictingMapColors, Is.Empty);
        }

        /// <summary>If the map already contains BL's "Violett transparent" (52), the course purple goes right there.</summary>
        [Test]
        public void Layering_MapWithOwnTransparentViolet_UsesThatColour()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> map = CreateBlForestMapColors(includeTransparentViolet: true);

            PrintProfileCourseLayering layering = PrintProfileCourseLayering.Create(profile, map, true, null, null, null);

            Assert.That(layering.Status, Is.EqualTo(PrintProfileLayeringStatus.AtMapCoursePurple));
            Assert.That(layering.LowerPurpleMapLayer, Is.EqualTo(52));
            Assert.That(layering.MapColorAboveLowerPurple, Is.EqualTo("Brun för höjdkurvor mm"));
        }

        /// <summary>BL forest: upper violet (50) is opaque, lower "Violett transparent" (52) overprints, both 35/95/0/0.</summary>
        [Test]
        public void Layering_BlForest_UsesOpaqueUpperAndTransparentLowerViolet()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();

            PrintProfileCourseLayering layering = PrintProfileCourseLayering.Create(profile, CreateBlForestMapColors(false), true, null, null, null);

            Assert.That(layering.UpperColor.OcadId, Is.EqualTo(50));
            Assert.That(layering.UpperColor.Overprint, Is.False);
            Assert.That(layering.UpperColor.CmykText, Is.EqualTo("35/95/0/0"));
            Assert.That(layering.LowerColor.OcadId, Is.EqualTo(52));
            Assert.That(layering.LowerColor.Overprint, Is.True);
            Assert.That(layering.LowerColor.Magenta, Is.EqualTo(0.95F).Within(0.0001F));
        }

        /// <summary>BL sprint: upper "Täckande violett" opaque, lower course violet transparent, 35/100/0/0.</summary>
        [Test]
        public void Layering_BlSprint_UsesOpaqueUpperAndTransparentLowerViolet()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlSprint();

            PrintProfileCourseLayering layering = PrintProfileCourseLayering.Create(profile, new List<SourceMapColor>(), false, null, null, null);

            Assert.That(layering.UpperColor.Name, Is.EqualTo("Täckande violett"));
            Assert.That(layering.UpperColor.Overprint, Is.False);
            Assert.That(layering.LowerColor.Overprint, Is.True);
            Assert.That(layering.LowerColor.CmykText, Is.EqualTo("35/100/0/0"));
            Assert.That(layering.UpperColor.OcadId, Is.Not.EqualTo(layering.LowerColor.OcadId));
        }

        /// <summary>PDF and image maps cannot be split by colour, so the course stays on top.</summary>
        [Test]
        public void Layering_NonVectorMap_DrawsCourseOnTop()
        {
            PrintProfileCourseLayering layering = PrintProfileCourseLayering.Create(BuiltInPrintProfiles.CreateBlForest(),
                new List<SourceMapColor>(), false, null, null, null);

            Assert.That(layering.Status, Is.EqualTo(PrintProfileLayeringStatus.NotVectorMap));
            Assert.That(layering.LowerPurpleMapLayer, Is.Null);
        }

        /// <summary>Explicit colour mappings from an English ISOM map are honoured, and unknown colours stay below the purple.</summary>
        [Test]
        public void Layering_MappedIsomMap_PutsPurpleAboveAllAreaColours()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> map = new List<SourceMapColor> {
                CreateSourceColor("Yellow", 0, 9),
                CreateSourceColor("Green", 1, 6),
                CreateSourceColor("White for green", 2, 13),   // not in the profile
                CreateSourceColor("Brown 50%", 3, 5),           // not mapped
                CreateSourceColor("Brown", 4, 4),
                CreateSourceColor("Blue", 5, 3),
                CreateSourceColor("Black", 6, 0),
            };
            List<PrintProfileColorMapping> mappings = new List<PrintProfileColorMapping> {
                Mapping("yellow-100", map[0]), Mapping("green-100", map[1]),
                Mapping("brown-contours", map[4]), Mapping("blue-line-point", map[5]), Mapping("black", map[6]),
            };

            PrintProfileCourseLayering layering = PrintProfileCourseLayering.Create(profile, map, true, null, mappings, null);

            Assert.That(layering.Status, Is.EqualTo(PrintProfileLayeringStatus.FromProfileColorOrder));
            Assert.That(layering.LowerPurpleMapLayer, Is.EqualTo(5), "Directly above Brown 50%, directly under Brown.");
            Assert.That(layering.MapColorAboveLowerPurple, Is.EqualTo("Brown"));
            Assert.That(layering.ConflictingMapColors, Is.Empty);
        }

        /// <summary>A map that draws an area colour above brown is reported; the area colour still never hides the course.</summary>
        [Test]
        public void Layering_MapOrderContradictsProfile_ReportsConflictAndKeepsCourseAboveAreas()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> map = new List<SourceMapColor> {
                CreateSourceColor("Brun för höjdkurvor mm", 0, 4),
                CreateSourceColor("Grön 100%", 1, 6),
                CreateSourceColor("Svart", 2, 0),
            };

            PrintProfileCourseLayering layering = PrintProfileCourseLayering.Create(profile, map, true, null, null, null);

            Assert.That(layering.LowerPurpleMapLayer, Is.EqualTo(6), "Above the green area colour.");
            Assert.That(layering.ConflictingMapColors, Is.EquivalentTo(new string[] { "Brun för höjdkurvor mm" }));
        }

        /// <summary>
        /// A road/area fill colour drawn above brown (e.g. "Street infill" for parking lots) must never
        /// hide the course: the transparent lower purple is raised above it, and brown, now under the
        /// purple, is reported as showing through.
        /// </summary>
        [Test]
        public void Layering_SurfaceColourAboveBrown_IsKeptUnderTheCourse()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> map = new List<SourceMapColor> {
                CreateSourceColor("Yellow", 0, 9),
                CreateSourceColor("Green", 1, 6),
                CreateSourceColor("Brown", 2, 4),
                CreateSourceColor("Street infill", 3, 15),
                CreateSourceColor("Blue", 4, 3),
                CreateSourceColor("Black", 5, 0),
            };
            map[0].FillsSurfaces = true;
            map[1].FillsSurfaces = true;
            map[3].FillsSurfaces = true;
            map[5].FillsSurfaces = true;   // black buildings: never moved under the course
            map[5].Cmyk = new PrintProfileCmyk { Cyan = 0, Magenta = 0, Yellow = 0, Black = 100 };
            List<PrintProfileColorMapping> mappings = new List<PrintProfileColorMapping> {
                Mapping("yellow-100", map[0]), Mapping("green-100", map[1]),
                Mapping("brown-contours", map[2]), Mapping("black", map[5]),
            };

            PrintProfileCourseLayering layering = PrintProfileCourseLayering.Create(profile, map, true, null, mappings, null);

            Assert.That(layering.LowerPurpleMapLayer, Is.EqualTo(15), "Directly above Street infill.");
            Assert.That(layering.SurfaceColorsKeptUnderCourse, Is.EqualTo(new List<string> { "Street infill" }));
            Assert.That(layering.LineColorsUnderTransparentCourse, Is.EqualTo(new List<string> { "Brown" }));
            Assert.That(layering.SurfaceColorsCoveringCourse, Is.Empty);
            Assert.That(layering.MapColorAboveLowerPurple, Is.EqualTo("Blue"));
        }

        /// <summary>Black surfaces (buildings) stay above the course, as in BL's table.</summary>
        [Test]
        public void Layering_BlackSurfaces_AreNotMoved()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> map = CreateBlForestMapColors(includeTransparentViolet: true);
            SourceMapColor black = map.Single(color => color.Name == "Svart");
            black.FillsSurfaces = true;
            black.Cmyk = new PrintProfileCmyk { Cyan = 0, Magenta = 0, Yellow = 0, Black = 100 };

            PrintProfileCourseLayering layering = PrintProfileCourseLayering.Create(profile, map, true, null, null, null);

            Assert.That(layering.LowerPurpleMapLayer, Is.EqualTo(52));
            Assert.That(layering.SurfaceColorsKeptUnderCourse, Is.Empty);
        }

        /// <summary>A white area fill above the course (e.g. white parking markings) would erase it and is kept below.</summary>
        [Test]
        public void Layering_WhiteAreaFillAboveCourse_IsKeptUnderTheCourse()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> map = CreateBlForestMapColors(includeTransparentViolet: true);
            SourceMapColor white = map.Single(color => color.Name == "Mask för vattendrag");
            white.FillsSurfaces = true;
            white.Cmyk = new PrintProfileCmyk();

            PrintProfileCourseLayering layering = PrintProfileCourseLayering.Create(profile, map, true, null, null, null);

            Assert.That(layering.SurfaceColorsKeptUnderCourse, Is.EqualTo(new List<string> { "Mask för vattendrag" }));
            Assert.That(layering.MapColorBelowLowerPurple, Is.EqualTo("Mask för vattendrag"));
            Assert.That(layering.LineColorsUnderTransparentCourse, Is.EqualTo(new List<string> { "Brun för höjdkurvor mm" }));
        }

        /// <summary>An opaque lower purple is not raised over line colours; covering surfaces are reported.</summary>
        [Test]
        public void Layering_OpaqueLowerPurple_ReportsCoveringSurfaces()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            profile.ColorRules.Single(rule => rule.Id == "violet-transparent").OverprintIntent = PrintProfileOverprintIntent.Knockout;
            List<SourceMapColor> map = CreateBlForestMapColors(includeTransparentViolet: false);
            map.Single(color => color.Name == "Vägfyllning").FillsSurfaces = true;

            PrintProfileCourseLayering layering = PrintProfileCourseLayering.Create(profile, map, true, null, null, null);

            Assert.That(layering.MapColorBelowLowerPurple, Is.EqualTo("Blå yta"));
            Assert.That(layering.SurfaceColorsCoveringCourse, Is.EqualTo(new List<string> { "Vägfyllning" }));
        }

        /// <summary>Without any matching colour, the event's own lower-purple layer is used as fallback.</summary>
        [Test]
        public void Layering_NoMatches_UsesFallbackLayerOrDrawsOnTop()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            List<SourceMapColor> map = new List<SourceMapColor> {
                CreateSourceColor("Area", 0, 20),
                CreateSourceColor("Lines", 1, 21),
            };

            PrintProfileCourseLayering withFallback = PrintProfileCourseLayering.Create(profile, map, true, null, null, 20);
            PrintProfileCourseLayering withoutFallback = PrintProfileCourseLayering.Create(profile, map, true, null, null, null);

            Assert.That(withFallback.Status, Is.EqualTo(PrintProfileLayeringStatus.Fallback));
            Assert.That(withFallback.LowerPurpleMapLayer, Is.EqualTo(20));
            Assert.That(withoutFallback.Status, Is.EqualTo(PrintProfileLayeringStatus.CourseOnTop));
            Assert.That(withoutFallback.LowerPurpleMapLayer, Is.Null);
        }

        /// <summary>BL forbids registration colour; preflight warns when the map contains one.</summary>
        [Test]
        public void Analyze_RegistrationColourInMap_WarnsForBl()
        {
            PrintProfile profile = BuiltInPrintProfiles.CreateBlForest();
            SourceMapColor registration = CreateSourceColor("All color separations", 0, 14);
            registration.Cmyk = new PrintProfileCmyk { Cyan = 100, Magenta = 100, Yellow = 100, Black = 100 };

            ColorPreflightReport report = ColorPreflightReport.Analyze(profile, new List<SourceMapColor> { registration },
                PdfExportCapabilities.CreateCurrentImplementation());

            Assert.That(report.Findings.Any(finding => finding.Severity == ColorPreflightSeverity.Warning
                                                      && finding.Message.Contains("registration colour")), Is.True);
        }

        /// <summary>Upper course colour survives JSON round-trips; older JSON without it still loads.</summary>
        [Test]
        public void UpperCourseColorRule_RoundTripsAndIsOptional()
        {
            PrintProfile forest = BuiltInPrintProfiles.CreateBlForest();
            PrintProfile restored = PrintProfileSerializer.Deserialize(PrintProfileSerializer.Serialize(forest));
            Assert.That(restored.UpperCourseColorRuleId, Is.EqualTo("violet-opaque"));
            Assert.That(BuiltInPrintProfiles.GetUpperCourseColorRule(restored).Name, Is.EqualTo("Violett"));

            forest.UpperCourseColorRuleId = "";
            PrintProfileCourseLayering layering = PrintProfileCourseLayering.Create(forest, new List<SourceMapColor>(), false, null, null, null);
            Assert.That(layering.UpperColor.Name, Is.EqualTo("Violett transparent"));
            Assert.That(layering.UpperColor.Overprint, Is.False, "Upper purple is always opaque.");
        }

        /// <summary>Creates a source colour list in BL forest table order, lowest first.</summary>
        private static List<SourceMapColor> CreateBlForestMapColors(bool includeTransparentViolet)
        {
            List<string> names = new List<string> {
                "Gul 50%", "Gul 100%", "Vit över gul", "Svart 35%", "Brun 50%", "Grön 30%", "Grön 60%", "Grön 100%",
                "Vit över grön", "Blå 50%", "Blå 70%", "Gul 100%/grön 50%", "Blå yta",
            };
            if (includeTransparentViolet)
                names.Add("Violett transparent");
            names.AddRange(new string[] { "Brun för höjdkurvor mm", "Mask för vattendrag", "Blå linje och punkt", "Väg sidlinje",
                                          "Vägfyllning", "Svart", "Vit", "Violett" });

            List<SourceMapColor> colors = new List<SourceMapColor>();
            for (int index = 0; index < names.Count; ++index) {
                short ocadId = (short)(100 + index);
                if (names[index] == "Violett transparent")
                    ocadId = 52;
                else if (names[index] == "Violett")
                    ocadId = 50;
                colors.Add(CreateSourceColor(names[index], index, ocadId));
            }
            return colors;
        }

        /// <summary>Creates an explicit user mapping from a profile rule to a map colour.</summary>
        private static PrintProfileColorMapping Mapping(string ruleId, SourceMapColor color)
        {
            return new PrintProfileColorMapping {
                RuleId = ruleId,
                MapColorName = color.Name,
                MapColorOcadId = color.OcadId,
                MapColorDrawOrder = color.DrawOrder,
            };
        }

        /// <summary>Creates a source colour with real CMYK percentages and the ISOM symbols that use it.</summary>
        private static SourceMapColor CreateCmykColor(string name, int drawOrder, float cyan, float magenta, float yellow, float black,
                                                      params string[] symbolIds)
        {
            return new SourceMapColor {
                Name = name,
                OcadId = (short)(drawOrder + 1),
                DrawOrder = drawOrder,
                Cmyk = new PrintProfileCmyk { Cyan = cyan, Magenta = magenta, Yellow = yellow, Black = black },
                Symbols = symbolIds.Select(id => new SourceMapSymbol { SymbolId = id, Name = id }).ToList(),
            };
        }

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
