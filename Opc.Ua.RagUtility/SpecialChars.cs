using System.Text;
using System.Text.RegularExpressions;

namespace Opc.Ua.RagUtility
{
    public static class SpecialChars
    {
        public const string SectionStart = "/§";
        public const string BoldStart = "/‡";
        public const string BoldEnd = "/†";
        public const string ItalicStart = "/¡";
        public const string ItalicEnd = "/¿";
        public const string TableStart = "/»";
        public const string TableEnd = "/«";
        public const string RowStart = "/°";
        public const string RowEnd = "/¬";
        public const string CellStart = "/·";
        public const string CellEnd = "/¤";
        public const string Bullet = "/•";
        public const string Numbered = "/¢";
        public const string Figure = "/¥";
        public const string FigureTitle = "/®";

        public static bool IsSpecialChar(string word)
        {
            return word == SpecialChars.SectionStart ||
                   word == SpecialChars.BoldStart ||
                   word == SpecialChars.BoldEnd ||
                   word == SpecialChars.ItalicStart ||
                   word == SpecialChars.ItalicEnd ||
                   word == SpecialChars.TableStart ||
                   word == SpecialChars.TableEnd ||
                   word == SpecialChars.RowStart ||
                   word == SpecialChars.RowEnd ||
                   word == SpecialChars.CellStart ||
                   word == SpecialChars.CellEnd ||
                   word == SpecialChars.Bullet ||
                   word == SpecialChars.Numbered;
        }
    }
}
