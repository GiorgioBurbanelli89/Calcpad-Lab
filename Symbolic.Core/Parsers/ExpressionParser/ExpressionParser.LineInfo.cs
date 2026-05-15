using System.Collections.Generic;
namespace Calcpad.Core
{
    public partial class ExpressionParser
    {
        private readonly struct LineInfo
        {
            internal readonly Keyword Keyword;
            internal readonly List<Token> Tokens;
            // MATLAB suppression: línea terminó en ';'. Suprime output en cached re-entries
            // (ej. iteraciones de for-loop después de la primera).
            internal readonly bool IsSuppressed;
            internal bool IsCached => Tokens is not null;

            internal LineInfo(List<Token> tokens, Keyword keyword, bool isSuppressed = false)
            {
                Tokens = tokens;
                Keyword = keyword;
                IsSuppressed = isSuppressed;
            }
        }
    }
}
