using NUnit.Framework;
using Narazaka.Unity.AAPMA.Editor;

namespace Narazaka.Unity.AAPMA.Editor.Tests
{
    public class LayerPassBaselineTests
    {
        [Test]
        public void Addition_TwoFrames_OutputIsSum()
        {
            var setting = new Narazaka.Unity.AAPMA.AAPSetting
            {
                Type = Narazaka.Unity.AAPMA.LogicType.Addition,
                Use1D = false,
                Input1 = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "A", Min = 0, Max = 1 },
                Input2 = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "B", Min = 0, Max = 1 },
                Output = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "Out", Min = 0, Max = 1 },
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("A", 0.3f);
            ev.SetFloat("B", 0.5f);
            ev.Step(2); // AAP は 1-frame lag があるので 2 フレーム流す

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(0.8f).Within(0.001f));
        }
    }
}
