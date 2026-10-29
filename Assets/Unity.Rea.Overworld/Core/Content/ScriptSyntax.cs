using System;

namespace MindAttic.Overworld.Core
{
    /// <summary>
    /// The shallowest check of the original's scripting language (RECONSTRUCTION 9): a script is one or more commands
    /// joined by " | ", each starting with CHANGE, MOVE or RUN and carrying at least one argument. The interpreter
    /// (story A14) checks the rest; this is what lets data validation reject a script that is plainly not one.
    /// </summary>
    public static class ScriptSyntax
    {
        static readonly string[] Verbs = { "CHANGE", "MOVE", "RUN" };

        /// <summary>Null when <paramref name="script"/> is shaped like a script, otherwise what is wrong with it.</summary>
        public static string Check(string script)
        {
            if (string.IsNullOrWhiteSpace(script)) return "is empty";
            var commands = script.Split('|');
            for (var i = 0; i < commands.Length; i++)
            {
                var words = commands[i].Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0) return "has an empty command " + (i + 1);
                if (Array.IndexOf(Verbs, words[0].ToUpperInvariant()) < 0)
                    return "has the command \"" + commands[i].Trim() + "\", which does not start with CHANGE, MOVE or RUN";
                if (words.Length < 2) return "has the command \"" + commands[i].Trim() + "\" with nothing after its verb";
            }

            return null;
        }
    }
}
