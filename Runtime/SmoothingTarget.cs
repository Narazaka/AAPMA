namespace Narazaka.Unity.AAPMA
{
    public enum SmoothingTarget
    {
        [IString("Local & Remote", "ローカル & リモート")]
        Both,
        [IString("Local Only", "ローカルのみ")]
        LocalOnly,
        [IString("Remote Only", "リモートのみ")]
        RemoteOnly,
    }
}
