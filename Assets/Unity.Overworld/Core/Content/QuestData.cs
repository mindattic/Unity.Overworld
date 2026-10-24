using System.Collections.Generic;

namespace MindAttic.Overworld.Core
{
    /// <summary>What an objective asks (the original's anObjectiveType; only MoveTo is ever evaluated).</summary>
    public enum ObjectiveType
    {
        MoveTo,
        TalkTo,
        GiveTo,
        GetItem,
    }

    /// <summary>A quest's state (the original's qstat).</summary>
    public enum QuestStatus
    {
        Inactive,
        Active,
        Failed,
        Success,
    }

    public sealed class ObjectiveDefinition
    {
        public ObjectiveType Type { get; internal set; }
        public float X { get; internal set; }
        public float Y { get; internal set; }

        /// <summary>Completion is the hero's reference point within this distance of (X, Y) on both axes.</summary>
        public float Range { get; internal set; }

        public int Exp { get; internal set; }
        public string Text { get; internal set; }
        public string StartScript { get; internal set; }
        public string EndScript { get; internal set; }
    }

    /// <summary>A main quest or a subquest: experience, text, starting status, scripts and objectives.</summary>
    public sealed class QuestDefinition
    {
        public int Id { get; internal set; }
        public string Text { get; internal set; }
        public int Exp { get; internal set; }
        public QuestStatus Status { get; internal set; }
        public string StartScript { get; internal set; }
        public string EndScript { get; internal set; }
        public IReadOnlyList<ObjectiveDefinition> Objectives { get; internal set; }
    }

    /// <summary>The four main quests and the two subquests (RECONSTRUCTION 10).</summary>
    public sealed class QuestData
    {
        internal QuestData(List<QuestDefinition> quests, List<QuestDefinition> subquests)
        {
            Quests = quests;
            Subquests = subquests;
        }

        public IReadOnlyList<QuestDefinition> Quests { get; }
        public IReadOnlyList<QuestDefinition> Subquests { get; }

        internal static QuestData Read(IContentSource source, string file, List<string> errors)
        {
            var root = ContentFiles.Open(source, file, errors);
            if (root == null) return null;
            var quests = ReadList(root, "quests", errors);
            var subquests = ReadList(root, "subquests", errors);
            root.Done();

            if (quests.Count == 0) errors.Add(file + ".quests: there must be at least one main quest");
            for (var i = 0; i < quests.Count; i++)
            {
                var want = i == 0 ? QuestStatus.Active : QuestStatus.Inactive;
                if (quests[i].Status != want)
                    errors.Add(file + ".quests[" + i + "]: the main story starts with quest 0 Active and the rest Inactive (this is " + quests[i].Status + ")");
            }

            return new QuestData(quests, subquests);
        }

        static List<QuestDefinition> ReadList(ContentReader root, string key, List<string> errors)
        {
            var list = new List<QuestDefinition>();
            foreach (var r in root.Objects(key))
            {
                var quest = new QuestDefinition
                {
                    Id = r.Integer("id", 0),
                    Text = r.Text("text"),
                    Exp = r.Integer("exp", 0),
                    Status = r.Choice<QuestStatus>("status"),
                    StartScript = r.Text("startScript", allowEmpty: true),
                    EndScript = r.Text("endScript", allowEmpty: true),
                };
                if (quest.Id != list.Count) r.Fail("id", "must be " + list.Count + ", the quest's place in the list (is " + quest.Id + ")");
                CheckOptionalScript(r, "startScript", quest.StartScript);
                CheckOptionalScript(r, "endScript", quest.EndScript);

                var objectives = new List<ObjectiveDefinition>();
                foreach (var o in r.Objects("objectives"))
                {
                    var at = o.Pair("at");
                    var objective = new ObjectiveDefinition
                    {
                        Type = o.Choice<ObjectiveType>("type"),
                        X = at.X,
                        Y = at.Y,
                        Range = o.Positive("range"),
                        Exp = o.Integer("exp", 0),
                        Text = o.Text("text"),
                        StartScript = o.Text("startScript", allowEmpty: true),
                        EndScript = o.Text("endScript", allowEmpty: true),
                    };
                    CheckOptionalScript(o, "startScript", objective.StartScript);
                    CheckOptionalScript(o, "endScript", objective.EndScript);
                    o.Done();
                    objectives.Add(objective);
                }

                if (objectives.Count == 0) r.Fail("objectives", "a quest needs at least one objective");
                r.Done();
                quest.Objectives = objectives;
                list.Add(quest);
            }

            return list;
        }

        static void CheckOptionalScript(ContentReader r, string key, string script)
        {
            if (script.Length == 0) return;
            var problem = ScriptSyntax.Check(script);
            if (problem != null) r.Fail(key, problem);
        }
    }
}
