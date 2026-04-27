using UnityEngine;

namespace Narazaka.Unity.AAPMA
{
#pragma warning disable IDE1006
    public class istring
#pragma warning restore IDE1006
    {
        public string en;
        public string ja;
        public string tooltipEn;
        public string tooltipJa;
        public istring(string en, string ja)
        {
            this.en = en;
            this.ja = ja;
        }
        public istring(string en, string ja, string tooltipEn, string tooltipJa)
        {
            this.en = en;
            this.ja = ja;
            this.tooltipEn = tooltipEn;
            this.tooltipJa = tooltipJa;
        }
        public string Tooltip => IsJa ? tooltipJa : tooltipEn;
        public GUIContent GUIContent => new GUIContent(this, Tooltip);

        public static implicit operator string(istring data) => IsJa ? data.ja : data.en;

        static bool IsJa =>
#if UNITY_EDITOR
            nadena.dev.ndmf.localization.LanguagePrefs.Language == "ja-jp";
#else
            false;
#endif
    }
}
