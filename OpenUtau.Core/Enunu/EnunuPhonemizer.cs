﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using K4os.Hash.xxHash;
using OpenUtau.Api;
using OpenUtau.Core.Ustx;
using Serilog;

namespace OpenUtau.Core.Enunu {
    [Phonemizer("Enunu Phonemizer", "ENUNU")]
    public class EnunuPhonemizer : Phonemizer {
        EnunuSinger singer;
        Dictionary<Note[], Phoneme[]> partResult = new Dictionary<Note[], Phoneme[]>();

        public override void SetSinger(USinger singer) {
            this.singer = singer as EnunuSinger;
        }

        public override void SetUp(Note[][] notes) {
            partResult.Clear();
            if (notes.Length == 0 || singer == null || !singer.Found) {
                return;
            }
            ulong hash = HashNoteGroups(notes);
            var tmpPath = Path.Join(PathManager.Inst.CachePath, $"lab-{hash:x16}");
            var ustPath = tmpPath + ".tmp";
            var scorePath = Path.Join(tmpPath, $"score.lab");
            var timingPath = Path.Join(tmpPath, $"timing.lab");
            var enunuNotes = NoteGroupsToEnunu(notes);
            if (!File.Exists(scorePath) || !File.Exists(timingPath)) {
                EnunuInit.Init();
                EnunuUtils.WriteUst(enunuNotes, bpm, singer, ustPath);
                string args = $"{EnunuInit.Script} \"phonemize\" \"{ustPath}\"";
                Util.ProcessRunner.Run(EnunuInit.Python, args, Log.Logger, workDir: EnunuInit.WorkDir, timeoutMs: 0);
            }
            var noteIndexes = LabelToNoteIndex(scorePath, enunuNotes);
            var timing = ParseLabel(timingPath);
            timing.Zip(noteIndexes, (phoneme, noteIndex) => Tuple.Create(phoneme, noteIndex))
                .GroupBy(tuple => tuple.Item2)
                .ToList()
                .ForEach(g => {
                    if (g.Key >= 0) {
                        var noteGroup = notes[g.Key];
                        partResult[noteGroup] = g.Select(tu => tu.Item1).ToArray();
                    }
                });
        }

        static ulong HashNoteGroups(Note[][] notes) {
            using (var stream = new MemoryStream()) {
                using (var writer = new BinaryWriter(stream)) {
                    foreach (var ns in notes) {
                        foreach (var n in ns) {
                            writer.Write(n.lyric);
                            writer.Write(n.position);
                            writer.Write(n.duration);
                            writer.Write(n.tone);
                        }
                    }
                    return XXH64.DigestOf(stream.ToArray());
                }
            }
        }

        static EnunuNote[] NoteGroupsToEnunu(Note[][] notes) {
            var result = new List<EnunuNote>();
            int position = 0;
            int index = 0;
            while (index < notes.Length) {
                if (position < notes[index][0].position) {
                    result.Add(new EnunuNote {
                        lyric = "R",
                        length = notes[index][0].position - position,
                        noteNum = 60,
                        noteIndex = -1,
                    });
                    position = notes[index][0].position;
                } else {
                    result.Add(new EnunuNote {
                        lyric = notes[index][0].lyric,
                        length = notes[index].Sum(n => n.duration),
                        noteNum = notes[index][0].tone,
                        noteIndex = index,
                    });
                    position += notes[index++][0].duration;
                }
            }
            return result.ToArray();
        }

        static int[] LabelToNoteIndex(string scorePath, EnunuNote[] enunuNotes) {
            var result = new List<int>();
            int lastPos = 0;
            int index = 0;
            var score = ParseLabel(scorePath);
            foreach (var p in score) {
                if (p.position != lastPos) {
                    index++;
                    lastPos = p.position;
                }
                result.Add(enunuNotes[index].noteIndex);
            }
            return result.ToArray();
        }

        static Phoneme[] ParseLabel(string path) {
            var phonemes = new List<Phoneme>();
            using (var reader = new StreamReader(path, Encoding.UTF8)) {
                while (!reader.EndOfStream) {
                    var line = reader.ReadLine();
                    var parts = line.Split();
                    if (parts.Length == 3 &&
                        int.TryParse(parts[0], out var pos) &&
                        int.TryParse(parts[1], out var end)) {
                        phonemes.Add(new Phoneme {
                            phoneme = parts[2],
                            position = pos,
                        });
                    }
                }
            }
            return phonemes.ToArray();
        }

        public override Result Process(Note[] notes, Note? prev, Note? next, Note? prevNeighbour, Note? nextNeighbour, Note[] prevs) {
            if (partResult.TryGetValue(notes, out var phonemes)) {
                return new Result {
                    phonemes = phonemes.Select(p => {
                        double posMs = p.position * 0.0001;
                        p.position = MsToTick(posMs) - notes[0].position;
                        return p;
                    }).ToArray(),
                };
            }
            return new Result {
                phonemes = new Phoneme[] {
                    new Phoneme {
                        phoneme = "error",
                    }
                },
            };
        }

        public override void CleanUp() {
            partResult.Clear();
        }
    }
}
