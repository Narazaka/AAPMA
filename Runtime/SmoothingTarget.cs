namespace Narazaka.Unity.AAPMA
{
    public enum SmoothingTarget
    {
        [IString("Always", "常時")]
        Both,
        [IString("Local", "ローカル")]
        LocalOnly,
        [IString("Remote", "リモート")]
        RemoteOnly,
    }
}
