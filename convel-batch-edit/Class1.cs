using OpenUtau.Core;
using OpenUtau.Core.Editing;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Format;

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

