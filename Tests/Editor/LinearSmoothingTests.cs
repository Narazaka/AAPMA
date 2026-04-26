using NUnit.Framework;
using Narazaka.Unity.AAPMA;
using Narazaka.Unity.AAPMA.Editor;

namespace Narazaka.Unity.AAPMA.Editor.Tests
{
    public class LinearSmoothingTests
    {
        static AAPSetting Make(float coef, bool asParam = false, string paramName = null) =>
            new AAPSetting
            {
                Type = LogicType.LinearSmoothing,
                Input1 = new AAPParameter { Parameter = "In", Min = 0, Max = 1 },
                Output = new AAPParameter { Parameter = "Out", Min = 0, Max = 1 },
                CoefficientUseParameter = asParam,
                CoefficientValue = coef,
                CoefficientParameter = paramName,
            };

        [Test]
        public void Constant_StepSizeBelowDeadband_ConvergesCleanly()
        {
            // CoefficientValue=0.05 で StepSize も 0.05（定数モードでは値=ステップ=deadband）。
            // 1-frame lag のため境界で振動する可能性があるが |Out-1| < 0.05 で収束していること。
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { Make(0.05f) });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("In", 1f);
            ev.Step(200);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(1f).Within(0.05f));
        }

        [Test]
        public void Parametric_StepSizeWellBelowMax_ConvergesPrecisely()
        {
            // Max(=CoefficientValue)=0.1, 実 StepSize=0.02 → 比 1:5 で十分減衰
            var setting = Make(0.1f, asParam: true, paramName: "Step");
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("Step", 0.02f);
            ev.SetFloat("In", 1f);
            ev.Step(500);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void NoMovementWhenInputEqualsOutput()
        {
            // 1-frame Delta lag のため、StepSize=deadband だと target 付近で振動する。
            // 「収束後に動かない」を担保するには StepSize < deadband が必要なので、
            // parametric モードで Max=0.1 / 実 StepSize=0.02 (比 1:5) に設定する。
            var setting = Make(0.1f, asParam: true, paramName: "Step");
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("Step", 0.02f);
            ev.SetFloat("In", 0.3f);
            ev.Step(300); // converge first
            var converged = ev.GetFloat("Out");

            ev.Step(50); // hold
            Assert.That(ev.GetFloat("Out"), Is.EqualTo(converged).Within(0.001f));
        }

        [Test]
        public void TwoSmoothers_SameLayerType_DoNotInterfere()
        {
            var s1 = new AAPSetting
            {
                Type = LogicType.LinearSmoothing,
                Input1 = new AAPParameter { Parameter = "InA", Min = 0, Max = 1 },
                Output = new AAPParameter { Parameter = "OutA", Min = 0, Max = 1 },
                CoefficientUseParameter = false,
                CoefficientValue = 0.05f,
            };
            var s2 = new AAPSetting
            {
                Type = LogicType.LinearSmoothing,
                Input1 = new AAPParameter { Parameter = "InB", Min = 0, Max = 1 },
                Output = new AAPParameter { Parameter = "OutB", Min = 0, Max = 1 },
                CoefficientUseParameter = false,
                CoefficientValue = 0.05f,
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { s1, s2 });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("InA", 1f);
            ev.SetFloat("InB", 0f); // OutB は動かない
            ev.Step(200);

            Assert.That(ev.GetFloat("OutA"), Is.EqualTo(1f).Within(0.05f));
            Assert.That(ev.GetFloat("OutB"), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void AsymmetricRange_ConvergesCorrectly()
        {
            // Range [0.2, 0.8] 非対称。
            var setting = new AAPSetting
            {
                Type = LogicType.LinearSmoothing,
                Input1 = new AAPParameter { Parameter = "In", Min = 0.2f, Max = 0.8f },
                Output = new AAPParameter { Parameter = "Out", Min = 0.2f, Max = 0.8f },
                CoefficientUseParameter = false,
                CoefficientValue = 0.02f,
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("In", 0.7f);
            ev.Step(300);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(0.7f).Within(0.05f));
        }

        [Test]
        public void InputBeyondMax_ClampsToMax()
        {
            // 範囲外入力は BlendTree が端の閾値でクランプするので Max に収束する
            var setting = new AAPSetting
            {
                Type = LogicType.LinearSmoothing,
                Input1 = new AAPParameter { Parameter = "In", Min = 0f, Max = 1f },
                Output = new AAPParameter { Parameter = "Out", Min = 0f, Max = 1f },
                CoefficientUseParameter = false,
                CoefficientValue = 0.05f,
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("In", 5f); // 範囲外
            ev.Step(200);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(1f).Within(0.05f));
        }
    }
}
