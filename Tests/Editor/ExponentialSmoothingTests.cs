using NUnit.Framework;
using Narazaka.Unity.AAPMA;
using Narazaka.Unity.AAPMA.Editor;

namespace Narazaka.Unity.AAPMA.Editor.Tests
{
    public class ExponentialSmoothingTests
    {
        static AAPSetting Make(float smoothAmount, bool asParam = false, string paramName = null) =>
            new AAPSetting
            {
                Type = LogicType.ExponentialSmoothing,
                Input1 = new AAPParameter { Parameter = "In", Min = 0, Max = 1 },
                Output = new AAPParameter { Parameter = "Out", Min = 0, Max = 1 },
                CoefficientUseParameter = asParam,
                ExpSmoothAmount = smoothAmount,
                CoefficientParameter = paramName,
            };

        [Test]
        public void Constant_OneStep_HalfSmoothing_OutputApproachesHalfway()
        {
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { Make(0.5f) });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("In", 1f);
            ev.Step(1);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void Constant_ManyFrames_ConvergesToInput()
        {
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { Make(0.5f) });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("In", 1f);
            ev.Step(50);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Parametric_RuntimeDriven_ConvergesToInput()
        {
            var setting = Make(0f, asParam: true, paramName: "Sm");
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("Sm", 0.5f);
            ev.SetFloat("In", 1f);
            ev.Step(50);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void NegativeRange_ConvergesCorrectly()
        {
            var setting = new AAPSetting
            {
                Type = LogicType.ExponentialSmoothing,
                Input1 = new AAPParameter { Parameter = "In", Min = -10, Max = 10 },
                Output = new AAPParameter { Parameter = "Out", Min = -10, Max = 10 },
                CoefficientUseParameter = false,
                ExpSmoothAmount = 0.5f,
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("In", -7f);
            ev.Step(50);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(-7f).Within(0.01f));
        }

        [Test]
        public void AsymmetricRange_ConvergesToInput()
        {
            // Range [0.2, 0.8] is asymmetric (not centered on 0). Verify the smoother
            // still converges correctly when Input.Min/Max are not symmetric around 0.
            var setting = new AAPSetting
            {
                Type = LogicType.ExponentialSmoothing,
                Input1 = new AAPParameter { Parameter = "In", Min = 0.2f, Max = 0.8f },
                Output = new AAPParameter { Parameter = "Out", Min = 0.2f, Max = 0.8f },
                CoefficientUseParameter = false,
                ExpSmoothAmount = 0.5f,
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("In", 0.5f);
            ev.Step(50);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void InputAtMaxBoundary_ConvergesToMax()
        {
            // When Input is exactly at Max, Output should converge to Max.
            var setting = new AAPSetting
            {
                Type = LogicType.ExponentialSmoothing,
                Input1 = new AAPParameter { Parameter = "In", Min = 0f, Max = 10f },
                Output = new AAPParameter { Parameter = "Out", Min = 0f, Max = 10f },
                CoefficientUseParameter = false,
                ExpSmoothAmount = 0.5f,
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("In", 10f);
            ev.Step(50);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(10f).Within(0.01f));
        }

        [Test]
        public void InputBeyondMax_ClampsToMax()
        {
            // When Input exceeds Max, BlendTree clamps the input to the threshold edge,
            // so the smoother converges to Max (not the out-of-range Input value).
            var setting = new AAPSetting
            {
                Type = LogicType.ExponentialSmoothing,
                Input1 = new AAPParameter { Parameter = "In", Min = 0f, Max = 10f },
                Output = new AAPParameter { Parameter = "Out", Min = 0f, Max = 10f },
                CoefficientUseParameter = false,
                ExpSmoothAmount = 0.5f,
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("In", 15f); // 範囲外
            ev.Step(50);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(10f).Within(0.01f));
        }
    }
}
