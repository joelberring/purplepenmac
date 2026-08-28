// PrintProfiles.cs
//
// Defines the non-destructive, versioned print-profile format used when
// preflighting or exporting a map. Profiles never alter the source map.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PurplePen
{
    /// <summary>Identifies the cartographic standard a print profile targets.</summary>
    public enum PrintProfileMapKind
    {
        Forest,
        Sprint
    }

    /// <summary>Identifies whether a profile colour is process or spot colour.</summary>
    public enum PrintProfileColorKind
    {
        Process,
        Spot
    }

    /// <summary>Expresses the intended printing interaction with colours below.</summary>
    public enum PrintProfileOverprintIntent
    {
        Knockout,
        Overprint
    }

    /// <summary>Stores CMYK components as percentages in the inclusive 0–100 range.</summary>
    public sealed class PrintProfileCmyk
    {
        /// <summary>Gets or sets the cyan component as a percentage.</summary>
        public float Cyan { get; set; }

        /// <summary>Gets or sets the magenta component as a percentage.</summary>
        public float Magenta { get; set; }

        /// <summary>Gets or sets the yellow component as a percentage.</summary>
        public float Yellow { get; set; }

        /// <summary>Gets or sets the black component as a percentage.</summary>
        public float Black { get; set; }
    }

    /// <summary>Provides the possible identifiers used to match a map colour to a profile rule.</summary>
    public sealed class PrintProfileColorIdentifier
    {
        /// <summary>Gets or sets exact, case-insensitive colour names accepted by the rule.</summary>
        public List<string> Names { get; set; } = new List<string>();

        /// <summary>Gets or sets OCAD colour identifiers accepted by the rule.</summary>
        public List<short> OcadIds { get; set; } = new List<short>();
    }

    /// <summary>Defines one effective colour and its expected relationship to other profile rules.</summary>
    public sealed class PrintProfileColorRule
    {
        /// <summary>Gets or sets the stable identifier used by JSON and rule relationships.</summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>Gets or sets the user-facing colour name from the profile source.</summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>Gets or sets the identifiers that may match a source-map colour.</summary>
        public PrintProfileColorIdentifier Identifier { get; set; } = new PrintProfileColorIdentifier();

        /// <summary>Gets or sets the CMYK values that apply non-destructively at export.</summary>
        public PrintProfileCmyk EffectiveCmyk { get; set; } = new PrintProfileCmyk();

        /// <summary>Gets or sets whether this is a process or spot colour.</summary>
        public PrintProfileColorKind ColorKind { get; set; } = PrintProfileColorKind.Process;

        /// <summary>Gets or sets the relative source-table order for display and diagnostics.</summary>
        public int RelativeDrawOrder { get; set; }

        /// <summary>Gets or sets the intended overprint or knockout behaviour.</summary>
        public PrintProfileOverprintIntent OverprintIntent { get; set; } = PrintProfileOverprintIntent.Knockout;

        /// <summary>Gets or sets rule identifiers that this colour must render below.</summary>
        public List<string> MustBeBelowRuleIds { get; set; } = new List<string>();

        /// <summary>Gets or sets rule identifiers that this colour must render above.</summary>
        public List<string> MustBeAboveRuleIds { get; set; } = new List<string>();
    }

    /// <summary>Stores an explicit user mapping from a profile rule to one source-map colour.</summary>
    public sealed class PrintProfileColorMapping
    {
        /// <summary>Gets or sets the stable profile rule identifier.</summary>
        public string RuleId { get; set; } = String.Empty;

        /// <summary>Gets or sets the source-map colour name selected by the user.</summary>
        public string MapColorName { get; set; } = String.Empty;

        /// <summary>Gets or sets the source-map OCAD identifier selected by the user.</summary>
        public short MapColorOcadId { get; set; }

        /// <summary>Gets or sets the lowest-to-highest drawing-order index selected by the user.</summary>
        public int MapColorDrawOrder { get; set; }
    }

    /// <summary>Describes production capabilities a selected profile can require.</summary>
    public sealed class PrintProfileRequirements
    {
        /// <summary>Gets or sets whether CMYK output is required.</summary>
        public bool RequireCmykOutput { get; set; } = true;

        /// <summary>Gets or sets whether PDF/X with an OutputIntent is required.</summary>
        public bool RequirePdfXOutputIntent { get; set; }

        /// <summary>Gets or sets whether an ICC profile must be embedded in output.</summary>
        public bool RequireEmbeddedIccProfile { get; set; }

        /// <summary>Gets or sets whether real PDF overprint semantics are required.</summary>
        public bool RequireTrueOverprint { get; set; }
    }

    /// <summary>Defines a versioned and non-destructive print-profile document.</summary>
    public sealed class PrintProfile
    {
        /// <summary>Gets the latest JSON schema version accepted by this application.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Gets or sets the JSON schema version used to serialize this profile.</summary>
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>Gets or sets the stable profile identifier.</summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>Gets or sets the profile display name.</summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>Gets or sets the ISO 8601 version date.</summary>
        public string VersionDate { get; set; } = String.Empty;

        /// <summary>Gets or sets the authoritative source URL for the profile data.</summary>
        public string SourceUrl { get; set; } = String.Empty;

        /// <summary>Gets or sets a short source-date or source-version note.</summary>
        public string SourceDescription { get; set; } = String.Empty;

        /// <summary>Gets or sets the map standard targeted by the profile.</summary>
        public PrintProfileMapKind MapKind { get; set; }

        /// <summary>Gets or sets the rule used for course markings such as control circles.</summary>
        public string CourseColorRuleId { get; set; } = String.Empty;

        /// <summary>Gets or sets the optional intended printer name.</summary>
        public string PrinterName { get; set; } = String.Empty;

        /// <summary>Gets or sets the optional intended paper specification.</summary>
        public string PaperSpecification { get; set; } = String.Empty;

        /// <summary>Gets or sets the optional ICC profile identifier or path.</summary>
        public string IccProfile { get; set; } = String.Empty;

        /// <summary>Gets or sets production requirements that gate export.</summary>
        public PrintProfileRequirements Requirements { get; set; } = new PrintProfileRequirements();

        /// <summary>Gets or sets the effective colour rules.</summary>
        public List<PrintProfileColorRule> ColorRules { get; set; } = new List<PrintProfileColorRule>();
    }

    /// <summary>Serializes and deserializes the public JSON representation of print profiles.</summary>
    public static class PrintProfileSerializer
    {
        /// <summary>Serializes a profile as stable, indented JSON.</summary>
        /// <param name="profile">The profile to serialize.</param>
        /// <returns>The JSON representation.</returns>
        public static string Serialize(PrintProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(profile, options);
        }

        /// <summary>Deserializes a profile from JSON.</summary>
        /// <param name="json">The JSON document to deserialize.</param>
        /// <returns>The deserialized profile.</returns>
        public static PrintProfile Deserialize(string json)
        {
            if (String.IsNullOrWhiteSpace(json))
                throw new ArgumentException("A print-profile JSON document is required.", nameof(json));

            JsonSerializerOptions options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            PrintProfile profile = JsonSerializer.Deserialize<PrintProfile>(json, options);
            if (profile == null)
                throw new ArgumentException("The JSON document did not contain a print profile.", nameof(json));
            if (profile.SchemaVersion != PrintProfile.CurrentSchemaVersion)
                throw new ArgumentException("The print-profile JSON schema version is not supported.", nameof(json));

            return profile;
        }
    }

    /// <summary>Provides the built-in BL Idrottsservice profiles without mutating any source map.</summary>
    public static class BuiltInPrintProfiles
    {
        /// <summary>Creates the complete set of built-in profiles.</summary>
        /// <returns>Fresh profile instances safe for a caller to customize in memory.</returns>
        public static List<PrintProfile> CreateAll()
        {
            return new List<PrintProfile> {
                CreateBlForest(),
                CreateBlSprint(),
            };
        }

        /// <summary>Finds a fresh built-in profile by its stable identifier.</summary>
        /// <param name="profileId">The profile identifier stored in export settings.</param>
        /// <returns>The matching profile, or null if the identifier is not built in.</returns>
        public static PrintProfile FindById(string profileId)
        {
            foreach (PrintProfile profile in CreateAll()) {
                if (String.Equals(profile.Id, profileId, StringComparison.Ordinal))
                    return profile;
            }

            return null;
        }

        /// <summary>Gets the profile rule used to render course markings.</summary>
        /// <param name="profile">The selected print profile.</param>
        /// <returns>The course-colour rule, or null when the profile has none.</returns>
        public static PrintProfileColorRule GetCourseColorRule(PrintProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            return profile.ColorRules.Find(rule => rule.Id == profile.CourseColorRuleId);
        }

        /// <summary>Creates the BL forest-map profile observed on 2026-08-27.</summary>
        /// <returns>A new BL forest profile.</returns>
        public static PrintProfile CreateBlForest()
        {
            PrintProfile profile = CreateProfile("bl-forest-2026-08-27", "BL skog", "2026-08-27",
                "https://www.bl-idrottsservice.se/farginstallningar-kartnormen/",
                "BL Idrottsservice current forest table accessed 2026-08-27.",
                PrintProfileMapKind.Forest, "violet-transparent");

            profile.ColorRules.Add(CreateRule("violet-opaque", "Violett", 1, 35, 95, 0, 0, new string[] { "Violett" }, new short[] { 50 }));
            profile.ColorRules.Add(CreateRule("white", "Vit", 2, 0, 0, 0, 0, new string[] { "Vit" }, new short[0]));
            profile.ColorRules.Add(CreateRule("all-colors", "Alla färger", 3, 100, 100, 100, 100, new string[] { "Alla färger" }, new short[0]));
            profile.ColorRules.Add(CreateRule("ski-green", "Grön för Skid OL", 4, 76, 0, 91, 0, new string[] { "Grön för Skid OL" }, new short[0]));
            profile.ColorRules.Add(CreateRule("black", "Svart", 5, 0, 0, 0, 100, new string[] { "Svart" }, new short[0]));
            profile.ColorRules.Add(CreateRule("road-fill", "Vägfyllning", 6, 0, 28, 45, 12, new string[] { "Vägfyllning" }, new short[0]));
            profile.ColorRules.Add(CreateRule("road-edge", "Väg sidlinje", 7, 0, 0, 0, 100, new string[] { "Väg sidlinje" }, new short[0]));
            profile.ColorRules.Add(CreateRule("blue-line-point", "Blå linje och punkt", 8, 100, 0, 0, 0, new string[] { "Blå linje och punkt" }, new short[0]));
            profile.ColorRules.Add(CreateRule("water-mask", "Mask för vattendrag", 9, 0, 0, 0, 0, new string[] { "Mask för vattendrag" }, new short[0]));
            profile.ColorRules.Add(CreateRule("brown-contours", "Brun för höjdkurvor mm", 10, 0, 75, 100, 33, new string[] { "Brun för höjdkurvor mm" }, new short[0]));
            PrintProfileColorRule transparentViolet = CreateRule("violet-transparent", "Violett transparent", 11, 35, 95, 0, 0, new string[] { "Violett transparent" }, new short[] { 52 });
            transparentViolet.OverprintIntent = PrintProfileOverprintIntent.Overprint;
            transparentViolet.MustBeBelowRuleIds.AddRange(new string[] { "brown-contours", "black", "blue-area" });
            transparentViolet.MustBeAboveRuleIds.AddRange(new string[] { "blue-70", "blue-50" });
            profile.ColorRules.Add(transparentViolet);
            profile.ColorRules.Add(CreateRule("blue-area", "Blå yta", 12, 100, 0, 0, 0, new string[] { "Blå yta" }, new short[0]));
            profile.ColorRules.Add(CreateRule("yellow-green", "Gul 100%/grön 50%", 13, 33, 28, 85, 0, new string[] { "Gul 100%/grön 50%" }, new short[0]));
            profile.ColorRules.Add(CreateRule("blue-70", "Blå 70%", 14, 70, 0, 0, 0, new string[] { "Blå 70%" }, new short[0]));
            profile.ColorRules.Add(CreateRule("blue-50", "Blå 50%", 15, 50, 0, 0, 0, new string[] { "Blå 50%" }, new short[0]));
            profile.ColorRules.Add(CreateRule("white-over-green", "Vit över grön", 16, 0, 0, 0, 0, new string[] { "Vit över grön" }, new short[0]));
            profile.ColorRules.Add(CreateRule("green-100", "Grön 100%", 17, 76, 0, 91, 0, new string[] { "Grön 100%" }, new short[0]));
            profile.ColorRules.Add(CreateRule("green-60", "Grön 60%", 18, 44, 0, 54, 0, new string[] { "Grön 60%" }, new short[0]));
            profile.ColorRules.Add(CreateRule("green-30", "Grön 30%", 19, 20, 0, 26, 0, new string[] { "Grön 30%" }, new short[0]));
            profile.ColorRules.Add(CreateRule("brown-50", "Brun 50%", 20, 0, 28, 45, 12, new string[] { "Brun 50%" }, new short[0]));
            profile.ColorRules.Add(CreateRule("black-35", "Svart 35%", 21, 0, 0, 0, 30, new string[] { "Svart 35%" }, new short[0]));
            profile.ColorRules.Add(CreateRule("white-over-yellow", "Vit över gul", 22, 0, 0, 0, 0, new string[] { "Vit över gul" }, new short[0]));
            profile.ColorRules.Add(CreateRule("yellow-100", "Gul 100%", 23, 0, 27, 94, 0, new string[] { "Gul 100%" }, new short[0]));
            profile.ColorRules.Add(CreateRule("yellow-50", "Gul 50%", 24, 0, 20, 44, 0, new string[] { "Gul 50%" }, new short[0]));
            return profile;
        }

        /// <summary>Creates the BL sprint-map profile effective from 2025-05-01.</summary>
        /// <returns>A new BL sprint profile.</returns>
        public static PrintProfile CreateBlSprint()
        {
            PrintProfile profile = CreateProfile("bl-sprint-2025-05-01", "BL sprint", "2025-05-01",
                "https://www.bl-idrottsservice.se/farginstallning-sprintnormen/",
                "BL Idrottsservice current sprint setting, effective from 2025-05-01.",
                PrintProfileMapKind.Sprint, "course-violet");

            profile.ColorRules.Add(CreateRule("violet-opaque", "Täckande violett", 1, 35, 100, 0, 0, new string[] { "Täckande violett" }, new short[0]));
            profile.ColorRules.Add(CreateRule("white-opaque", "Täckande vitt", 2, 0, 0, 0, 0, new string[] { "Täckande vitt" }, new short[0]));
            profile.ColorRules.Add(CreateRule("black", "Svart", 3, 0, 0, 0, 100, new string[] { "Svart" }, new short[0]));
            profile.ColorRules.Add(CreateRule("blue", "Blå", 4, 100, 0, 0, 0, new string[] { "Blå" }, new short[0]));
            profile.ColorRules.Add(CreateRule("brown", "Brun", 5, 0, 80, 100, 25, new string[] { "Brun" }, new short[0]));
            profile.ColorRules.Add(CreateRule("white-two-level", "Vit för två nivåstruktur", 6, 0, 0, 0, 0, new string[] { "Vit för två nivåstruktur" }, new short[0]));
            PrintProfileColorRule courseViolet = CreateRule("course-violet", "Violett (kontrollringar mm)", 7, 35, 100, 0, 0, new string[] { "Violett (kontrollringar mm)" }, new short[0]);
            courseViolet.OverprintIntent = PrintProfileOverprintIntent.Overprint;
            profile.ColorRules.Add(courseViolet);
            profile.ColorRules.Add(CreateRule("white-number-shadow", "Vit skugga för siffra", 8, 0, 0, 0, 0, new string[] { "Vit skugga för siffra" }, new short[0]));
            profile.ColorRules.Add(CreateRule("violet-50", "Violett 50% (53)", 9, 18, 43, 0, 0, new string[] { "Violett 50% (53)" }, new short[0]));
            profile.ColorRules.Add(CreateRule("black-50-building", "Svart 50% (byggnad)", 10, 0, 0, 0, 44, new string[] { "Svart 50% (byggnad)" }, new short[0]));
            profile.ColorRules.Add(CreateRule("black-20-crossing", "Svart 20% (genomgång)", 11, 0, 0, 0, 15, new string[] { "Svart 20% (genomgång)" }, new short[0]));
            profile.ColorRules.Add(CreateRule("green-black", "Grön 100%/Svart 30%", 12, 76, 0, 91, 30, new string[] { "Grön 100%/Svart 30%" }, new short[0]));
            profile.ColorRules.Add(CreateRule("green-point", "Grön punkt", 13, 76, 0, 91, 0, new string[] { "Grön punkt" }, new short[0]));
            profile.ColorRules.Add(CreateRule("brown-50", "Brun 50% (belagd yta i skog)", 14, 0, 28, 45, 12, new string[] { "Brun 50% (belagd yta i skog)" }, new short[0]));
            profile.ColorRules.Add(CreateRule("brown-30", "Brun 30% (belagd yta i bebyggelse)", 15, 0, 21, 28, 8, new string[] { "Brun 30% (belagd yta i bebyggelse)" }, new short[0]));
            profile.ColorRules.Add(CreateRule("black-contour", "Svart (konturlinje)", 16, 0, 0, 0, 100, new string[] { "Svart (konturlinje)" }, new short[0]));
            profile.ColorRules.Add(CreateRule("blue-contour", "Blå kontur", 17, 100, 0, 0, 0, new string[] { "Blå kontur" }, new short[0]));
            profile.ColorRules.Add(CreateRule("blue-70", "Blå 70%", 18, 70, 0, 0, 0, new string[] { "Blå 70%" }, new short[0]));
            profile.ColorRules.Add(CreateRule("blue-30", "Blå 30%", 19, 30, 0, 0, 0, new string[] { "Blå 30%" }, new short[0]));
            profile.ColorRules.Add(CreateRule("blue-area", "Blå (ytor)", 20, 100, 0, 0, 0, new string[] { "Blå (ytor)" }, new short[0]));
            profile.ColorRules.Add(CreateRule("black-35", "Svart 35% (berg i dagen)", 21, 0, 0, 0, 30, new string[] { "Svart 35% (berg i dagen)" }, new short[0]));
            profile.ColorRules.Add(CreateRule("white-over-green", "Vitt för grön", 22, 0, 0, 0, 0, new string[] { "Vitt för grön" }, new short[0]));
            profile.ColorRules.Add(CreateRule("yellow-green", "Gul 100%/Grön 50% (527.1)", 23, 34, 30, 85, 0, new string[] { "Gul 100%/Grön 50% (527.1)" }, new short[0]));
            profile.ColorRules.Add(CreateRule("green-area", "Grön (ytor)", 24, 74, 0, 91, 0, new string[] { "Grön (ytor)" }, new short[0]));
            profile.ColorRules.Add(CreateRule("green-60", "Grön 60%", 25, 42, 0, 54, 0, new string[] { "Grön 60%" }, new short[0]));
            profile.ColorRules.Add(CreateRule("green-30", "Grön 30%", 26, 21, 0, 26, 0, new string[] { "Grön 30%" }, new short[0]));
            profile.ColorRules.Add(CreateRule("white-over-yellow", "Vit över gul", 27, 0, 0, 0, 0, new string[] { "Vit över gul" }, new short[0]));
            profile.ColorRules.Add(CreateRule("yellow", "Gul", 28, 0, 28, 95, 0, new string[] { "Gul" }, new short[0]));
            profile.ColorRules.Add(CreateRule("yellow-50", "Gul 50%", 29, 0, 21, 44, 0, new string[] { "Gul 50%" }, new short[0]));
            profile.ColorRules.Add(CreateRule("brown-lower-30", "Undre brun 30", 30, 0, 21, 28, 8, new string[] { "Undre brun 30" }, new short[0]));
            return profile;
        }

        /// <summary>Creates the common metadata for a BL profile.</summary>
        /// <param name="id">The stable profile identifier.</param>
        /// <param name="name">The display name.</param>
        /// <param name="versionDate">The profile version date.</param>
        /// <param name="sourceUrl">The authoritative source URL.</param>
        /// <param name="sourceDescription">The source-version description.</param>
        /// <param name="mapKind">The targeted map kind.</param>
        /// <param name="courseColorRuleId">The rule used for course marking colour.</param>
        /// <returns>A profile with default production requirements.</returns>
        private static PrintProfile CreateProfile(string id, string name, string versionDate, string sourceUrl,
                                                  string sourceDescription, PrintProfileMapKind mapKind,
                                                  string courseColorRuleId)
        {
            return new PrintProfile {
                SchemaVersion = PrintProfile.CurrentSchemaVersion,
                Id = id,
                Name = name,
                VersionDate = versionDate,
                SourceUrl = sourceUrl,
                SourceDescription = sourceDescription,
                MapKind = mapKind,
                CourseColorRuleId = courseColorRuleId,
                Requirements = new PrintProfileRequirements {
                    RequireCmykOutput = true,
                    RequirePdfXOutputIntent = false,
                    RequireEmbeddedIccProfile = false,
                    RequireTrueOverprint = false,
                },
            };
        }

        /// <summary>Creates a process-colour rule from a BL source-table row.</summary>
        /// <param name="id">The stable rule identifier.</param>
        /// <param name="name">The source-table name.</param>
        /// <param name="relativeDrawOrder">The source-table order.</param>
        /// <param name="cyan">The cyan percentage.</param>
        /// <param name="magenta">The magenta percentage.</param>
        /// <param name="yellow">The yellow percentage.</param>
        /// <param name="black">The black percentage.</param>
        /// <param name="names">Accepted source-map names.</param>
        /// <param name="ocadIds">Accepted OCAD colour identifiers.</param>
        /// <returns>The populated rule.</returns>
        private static PrintProfileColorRule CreateRule(string id, string name, int relativeDrawOrder,
                                                        float cyan, float magenta, float yellow, float black,
                                                        string[] names, short[] ocadIds)
        {
            return new PrintProfileColorRule {
                Id = id,
                Name = name,
                Identifier = new PrintProfileColorIdentifier {
                    Names = new List<string>(names),
                    OcadIds = new List<short>(ocadIds),
                },
                EffectiveCmyk = new PrintProfileCmyk {
                    Cyan = cyan,
                    Magenta = magenta,
                    Yellow = yellow,
                    Black = black,
                },
                ColorKind = PrintProfileColorKind.Process,
                RelativeDrawOrder = relativeDrawOrder,
                OverprintIntent = PrintProfileOverprintIntent.Knockout,
            };
        }
    }

    /// <summary>Loads and persists user print profiles without changing the source map.</summary>
    public static class PrintProfileCatalog
    {
        /// <summary>Gets every built-in and user-imported print profile.</summary>
        public static List<PrintProfile> CreateAll()
        {
            List<PrintProfile> profiles = BuiltInPrintProfiles.CreateAll();
            foreach (PrintProfile profile in LoadUserProfiles()) {
                if (!profiles.Any(candidate => String.Equals(candidate.Id, profile.Id, StringComparison.Ordinal)))
                    profiles.Add(profile);
            }

            return profiles;
        }

        /// <summary>Finds a built-in or user-imported profile by its stable ID.</summary>
        public static PrintProfile FindById(string profileId)
        {
            return CreateAll().FirstOrDefault(profile => String.Equals(profile.Id, profileId, StringComparison.Ordinal));
        }

        /// <summary>Imports a JSON profile and saves it for future Purple Pen sessions.</summary>
        /// <param name="fileName">The JSON profile to import.</param>
        /// <returns>The validated imported profile.</returns>
        public static PrintProfile Import(string fileName)
        {
            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("A print-profile file is required.", nameof(fileName));

            PrintProfile profile = PrintProfileSerializer.Deserialize(File.ReadAllText(fileName));
            ValidateProfile(profile);
            Directory.CreateDirectory(UserProfileDirectory);
            File.WriteAllText(Path.Combine(UserProfileDirectory, GetSafeFileName(profile.Id) + ".json"), PrintProfileSerializer.Serialize(profile));
            return profile;
        }

        /// <summary>Exports the selected profile as a portable JSON document.</summary>
        /// <param name="profile">The profile to export.</param>
        /// <param name="fileName">The destination JSON file.</param>
        public static void Export(PrintProfile profile, string fileName)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (String.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("A destination file is required.", nameof(fileName));

            File.WriteAllText(fileName, PrintProfileSerializer.Serialize(profile));
        }

        /// <summary>Gets the folder which contains user-imported print profiles.</summary>
        public static string UserProfileDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PurplePen", "PrintProfiles");

        private static IEnumerable<PrintProfile> LoadUserProfiles()
        {
            if (!Directory.Exists(UserProfileDirectory))
                yield break;

            foreach (string fileName in Directory.EnumerateFiles(UserProfileDirectory, "*.json")) {
                PrintProfile profile = null;
                try {
                    profile = PrintProfileSerializer.Deserialize(File.ReadAllText(fileName));
                    ValidateProfile(profile);
                }
                catch (IOException) {
                }
                catch (UnauthorizedAccessException) {
                }
                catch (JsonException) {
                }
                catch (ArgumentException) {
                }

                if (profile != null)
                    yield return profile;
            }
        }

        private static void ValidateProfile(PrintProfile profile)
        {
            if (String.IsNullOrWhiteSpace(profile.Id))
                throw new ArgumentException("A print profile must have an ID.", nameof(profile));
            if (String.IsNullOrWhiteSpace(profile.Name))
                throw new ArgumentException("A print profile must have a name.", nameof(profile));
            if (profile.ColorRules == null)
                throw new ArgumentException("A print profile must contain colour rules.", nameof(profile));
        }

        private static string GetSafeFileName(string profileId)
        {
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            string safeFileName = new string(profileId.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray());
            return String.IsNullOrWhiteSpace(safeFileName) ? "print-profile" : safeFileName;
        }
    }
}
