using TMPro;
using UnityEngine;

namespace ParallaxVisions
{
    public static class GameUIFont
    {
        private static TMP_FontAsset arcade;
        private static TMP_FontAsset japanese;

        public static TMP_FontAsset Arcade
        {
            get
            {
                if (arcade == null)
                    arcade = Resources.Load<TMP_FontAsset>("Fonts/Arcade SDF");
                return arcade != null ? arcade : TMP_Settings.defaultFontAsset;
            }
        }

        public static TMP_FontAsset Japanese
        {
            get
            {
                if (japanese == null)
                    japanese = Resources.Load<TMP_FontAsset>("Fonts/Japanese Tagline SDF");
                return japanese != null ? japanese : Arcade;
            }
        }
    }
}
