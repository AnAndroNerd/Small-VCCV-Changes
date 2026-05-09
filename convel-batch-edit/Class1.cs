using System.Text.RegularExpressions;
using OpenUtau.Core;
using OpenUtau.Core.Editing;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Format;
using OpenUtau.Plugin.Builtin;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace convel_batch_edit;

public class VccvConvel : BatchEdit {
    public virtual string Name => name;

    private string name;

    public VccvConvel() {
        name = "VCCV Convel";
    }

    public void Run(UProject project, UVoicePart part, List<UNote> selectedNotes, DocManager docManager) {
        TimeAxis timeAxis = project.timeAxis;
        var notes = selectedNotes.Count > 0 ? selectedNotes : part.notes.ToList();
        docManager.StartUndoGroup("command.batch.plugin", true);
        foreach (var note in notes) {
            float baseConvel = 100 * ((float)timeAxis.GetBpmAtTick(note.position) / 120);
            int vel;
            if (note.duration >= 480) {
                vel = (int)(baseConvel + (50 - 100 * ((float)note.duration / 960)));
            } else {
                vel = (int)(baseConvel + (100 - (100 * ((float)note.duration / 480))));
            }
            docManager.ExecuteCmd(new SetNoteExpressionCommand(
                project, project.tracks[part.trackNo], part,
                note, OpenUtau.Core.Format.Ustx.VEL, [vel]));
        }
        docManager.EndUndoGroup();
    }
}

    public class NextConvel : BatchEdit {
        public virtual string Name => name;

        private string name;

        public NextConvel() {
            name = "Next Convel";
        }

        public void Run(UProject project, UVoicePart part, List<UNote> selectedNotes, DocManager docManager) {
            docManager.StartUndoGroup("command.batch.plugin", true);
            var notes = selectedNotes.Count > 0 ? selectedNotes : part.notes.ToList();
            foreach (var note in notes) {
                if (note.Next == null) continue;
                var vel = note.Next.GetExpressionNoteHas(project, project.tracks[part.trackNo], Ustx.VEL);
                docManager.ExecuteCmd(new SetNoteExpressionCommand(
                    project, project.tracks[part.trackNo], part,
                    note, Ustx.VEL, vel));
            }
            docManager.EndUndoGroup();
        }
    }
    public class PrevConvel : BatchEdit {
        public virtual string Name => name;

        private string name;

        public PrevConvel() {
            name = "Prev Convel";
        }

        public void Run(UProject project, UVoicePart part, List<UNote> selectedNotes, DocManager docManager) {
            docManager.StartUndoGroup("command.batch.plugin", true);
            var notes = selectedNotes.Count > 0 ? selectedNotes : part.notes.ToList();
            foreach (var note in notes) {
                if (note.Prev == null) continue;
                var vel = note.Prev.GetExpressionNoteHas(project, project.tracks[part.trackNo], Ustx.VEL);
                docManager.ExecuteCmd(new SetNoteExpressionCommand(
                    project, project.tracks[part.trackNo], part,
                    note, Ustx.VEL, vel));
            }
            docManager.EndUndoGroup();
        }
    }

    public class VCCVPhonemizerConvel : BatchEdit {
        public virtual string Name => name;

        private string name;

        public VCCVPhonemizerConvel() {
            name = "VCCV Phonemizer Convel";
        }

        private EnglishVCCVPhonemizer.CZSampaYAMLData LoadConfig(string path) {
            if (!File.Exists(path)) return null;
            try {
                var yaml = File.ReadAllText(path);
                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();
                return deserializer.Deserialize<EnglishVCCVPhonemizer.CZSampaYAMLData>(yaml);
            } catch (Exception ex) {
                Log.Error(ex, $"[VCCV Plugins] YAML Parsing Error in file: {path}");
                return null;
            }
        }
        public class VccvAliasClassifier {
            private readonly Regex _vowel;
            private readonly Regex _consonant;
            private readonly Regex[] _patterns;
            private readonly string[] _types;

            public VccvAliasClassifier(EnglishVCCVPhonemizer.CZSampaYAMLData config) {
                string V  = Alt(config.symbols.Where(s => s.type == "vowel").Select(s => s.symbol));
                string C  = Alt(config.symbols.Where(s => s.type != "vowel").Select(s => s.symbol));
                string C2 = Alt(["r", "l", "y", "w", "f"]);

                _vowel    = new Regex($@"^{V}$", RegexOptions.Compiled);
                _consonant = new Regex($@"^{C}$", RegexOptions.Compiled);

                (_patterns, _types) = Build([
                    ($@"^-{V}$",       "-V"),
                    ($@"^_{V}$",       "_V"),
                    ($@"^{V}-$",       "V-"),
                    ($@"^-{C}{V}$",    "-CV"),
                    ($@"^-{C}{C2}$",   "-CC"),
                    ($@"^_{C}{V}$",    "_CV"),
                    ($@"^{V}{C}{C}-$", "VCC-"),
                    ($@"^{V}{C}{C}$",  "VCC"),
                    ($@"^{V}{C}-$",    "VC-"),
                    ($@"^{C}{C}-$",    "CC-"),
                    ($@"^{V} {C}$",    "V C"),
                    ($@"^{C} {C}$",    "C C"),
                    ($@"^{V}{C} {C}$",    "VC C"),
                    ($@"^{V}{C}$",     "VC"),
                    ($@"^{C}{C2}$",    "onsetCC"),
                    ($@"^{C}{C}$",     "codaCC"),
                    ($@"^{C}{V}$",     "CV"),
                    ($@"^{V}$",        "V"),
                ]);
            }

            private static string Alt(IEnumerable<string> symbols) =>
                $"({string.Join("|", symbols.Select(Regex.Escape).OrderByDescending(s => s.Length))})";

            private static (Regex[], string[]) Build(IEnumerable<(string pattern, string type)> pairs) {
                var list = pairs.ToArray();
                return (
                    list.Select(p => new Regex(p.pattern, RegexOptions.Compiled)).ToArray(),
                    list.Select(p => p.type).ToArray()
                );
            }

            public string Classify(string alias) {
                for (int i = 0; i < _patterns.Length; i++)
                    if (_patterns[i].IsMatch(alias)) return _types[i];
                return "Unknown";
            }
            
        }
        
        public void Run(UProject project, UVoicePart part, List<UNote> selectedNotes, DocManager docManager) {
            
            EnglishVCCVPhonemizer.CZSampaYAMLData currentConfig = null;

            var track = project.tracks[part.trackNo];
            var singer = track.Singer;
            
            var vccvConvel = new VccvConvel();
            TimeAxis timeAxis = project.timeAxis;

            if (singer != null && !string.IsNullOrEmpty(singer.Location)) {
                string singerConfigPath = Path.Combine(singer.Location, "envccv.yaml");
                currentConfig = LoadConfig(singerConfigPath);
            }
            if (currentConfig == null) return;
            
            var aliasClassifier = new VccvAliasClassifier(currentConfig);
            var notes = selectedNotes.Count > 0 ? selectedNotes : part.notes.ToList();
            if (notes.Count == 0) return;

            UNote GetNoteForPhoneme(UPhoneme phoneme) {
                return part.notes.FirstOrDefault(n =>
                           n.PositionMs <= phoneme.PositionMs && phoneme.PositionMs < n.EndMs)
                       ?? part.notes.First();
            }
            
            docManager.StartUndoGroup("command.batch.plugin", true);
            Log.Debug($"[VCCVPhonemizerConvel] part.phonemes count: {part.phonemes.Count}");
            foreach (var note in notes) {
                float CalcConvel(UNote note, TimeAxis timeAxis) {
                    float baseConvel = 100 * ((float)timeAxis.GetBpmAtTick(note.position) / 120);
                    if (note.duration >= 480)
                        return baseConvel + (50 - 100 * ((float)note.duration / 960));
                    else
                        return baseConvel + (100 - (100 * ((float)note.duration / 480)));
                }
                var notePhonemes = part.phonemes.Where(p => p.Parent == note).ToList();
                foreach (var phoneme in notePhonemes) {
                    var type = aliasClassifier.Classify(phoneme.phoneme);
                    float? prevVel = phoneme.Prev?.GetExpression(project,track, Ustx.VEL).Item1;
                    float? nextVel = phoneme.Next?.GetExpression(project,track, Ustx.VEL).Item1;
                    var targetNote = GetNoteForPhoneme(phoneme);

                    switch (type) {
                        case "V C": case "VC": case "VC-":
                        case "VCC": case "VCC-": case "codaCC": case "C C":
                        case "VC C":
                            if (prevVel != null)
                                docManager.ExecuteCmd(new SetPhonemeExpressionCommand(
                                    project, track, part, phoneme, Ustx.VEL, prevVel));
                            break;
                        case "onsetCC":
                            if (nextVel != null)
                                docManager.ExecuteCmd(new SetPhonemeExpressionCommand(
                                    project, track, part, phoneme, Ustx.VEL, nextVel));
                            break;
                        default:
                                docManager.ExecuteCmd(new SetPhonemeExpressionCommand(
                                    project, track, part, phoneme, Ustx.VEL, CalcConvel(targetNote, timeAxis)));
                            break;
                    }
                }
            }
            docManager.EndUndoGroup();
        }
    }

