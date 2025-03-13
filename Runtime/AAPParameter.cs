namespace Narazaka.Unity.AAPMA
{
    [System.Serializable]
    public class AAPParameter
    {
        public string Parameter;
        public float Min;
        public float Max;

        public static implicit operator string(AAPParameter parameter) => parameter.Parameter;
        public override string ToString() => Parameter;
    }
}
