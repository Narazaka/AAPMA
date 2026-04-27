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

        [Test]
        public void Remap_NonUse1D_ScalesInputByOutputMax()
        {
            var setting = new Narazaka.Unity.AAPMA.AAPSetting
            {
                Type = Narazaka.Unity.AAPMA.LogicType.Remap,
                Use1D = false,
                Input1 = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "In", Min = 0, Max = 1 },
                Output = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "Out", Min = 0, Max = 5 },
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("In", 0.4f);
            ev.Step(2);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(0.4f * 5f).Within(0.001f));
        }

        [Test]
        public void Remap_Use1D_LinearInterpolatesBetweenOutputMinAndMax()
        {
            var setting = new Narazaka.Unity.AAPMA.AAPSetting
            {
                Type = Narazaka.Unity.AAPMA.LogicType.Remap,
                Use1D = true,
                Input1 = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "In", Min = 0, Max = 10 },
                Output = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "Out", Min = 100, Max = 200 },
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("In", 5f); // mid-range
            ev.Step(2);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(150f).Within(0.001f));
        }

        [Test]
        public void Addition_Use1D_SumOfClampedInputs()
        {
            var setting = new Narazaka.Unity.AAPMA.AAPSetting
            {
                Type = Narazaka.Unity.AAPMA.LogicType.Addition,
                Use1D = true,
                Input1 = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "A", Min = -1, Max = 1 },
                Input2 = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "B", Min = -1, Max = 1 },
                Output = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "Out", Min = -2, Max = 2 },
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("A", 0.5f);
            ev.SetFloat("B", -0.3f);
            ev.Step(2);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(0.2f).Within(0.001f));
        }

        [Test]
        public void Subtraction_NonUse1D_DifferenceOfInputs()
        {
            var setting = new Narazaka.Unity.AAPMA.AAPSetting
            {
                Type = Narazaka.Unity.AAPMA.LogicType.Subtraction,
                Use1D = false,
                Input1 = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "A", Min = 0, Max = 1 },
                Input2 = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "B", Min = 0, Max = 1 },
                Output = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "Out", Min = -1, Max = 1 },
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("A", 0.7f);
            ev.SetFloat("B", 0.4f);
            ev.Step(2);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(0.3f).Within(0.001f));
        }

        [Test]
        public void Subtraction_Use1D_DifferenceOfClampedInputs()
        {
            var setting = new Narazaka.Unity.AAPMA.AAPSetting
            {
                Type = Narazaka.Unity.AAPMA.LogicType.Subtraction,
                Use1D = true,
                Input1 = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "A", Min = -1, Max = 1 },
                Input2 = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "B", Min = -1, Max = 1 },
                Output = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "Out", Min = -2, Max = 2 },
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("A", 0.5f);
            ev.SetFloat("B", 0.3f);
            ev.Step(2);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(0.2f).Within(0.001f));
        }

        [Test]
        public void Multiplication_NonUse1D_ProductOfInputs()
        {
            var setting = new Narazaka.Unity.AAPMA.AAPSetting
            {
                Type = Narazaka.Unity.AAPMA.LogicType.Multiplication,
                Use1D = false,
                Input1 = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "A", Min = 0, Max = 1 },
                Input2 = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "B", Min = 0, Max = 1 },
                Output = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "Out", Min = 0, Max = 1 },
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("A", 0.5f);
            ev.SetFloat("B", 0.4f);
            ev.Step(2);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(0.2f).Within(0.001f));
        }

        [Test]
        public void Multiplication_Use1D_ProductOfInputs()
        {
            var setting = new Narazaka.Unity.AAPMA.AAPSetting
            {
                Type = Narazaka.Unity.AAPMA.LogicType.Multiplication,
                Use1D = true,
                Input1 = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "A", Min = 0, Max = 2 },
                Input2 = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "B", Min = 0, Max = 2 },
                Output = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "Out", Min = 0, Max = 4 },
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("A", 1.5f);
            ev.SetFloat("B", 1.0f);
            ev.Step(2);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(1.5f).Within(0.01f));
        }

        [Test]
        public void Division_OutputMaxOverOnePlusInput()
        {
            // Output = Output.Max / (1 + Input1)
            // 例: In=4, Out.Max=100 → Out = 100 / (1 + 4) = 20
            var setting = new Narazaka.Unity.AAPMA.AAPSetting
            {
                Type = Narazaka.Unity.AAPMA.LogicType.Division,
                Input1 = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "In", Min = 0, Max = 10 },
                Output = new Narazaka.Unity.AAPMA.AAPParameter { Parameter = "Out", Min = 0, Max = 100 },
            };
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { setting });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("In", 4f);
            ev.Step(2);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(20f).Within(0.01f));
        }
    }
}
