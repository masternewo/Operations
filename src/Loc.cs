using EFT.UI;

namespace Operations.Client
{
    /// <summary>Isolated so the EFT.UI namespace import cannot clash with UnityEngine.UI names elsewhere.</summary>
    internal static class Loc
    {
        public static string Get(string key) { return key.Localized(null); }
    }
}
