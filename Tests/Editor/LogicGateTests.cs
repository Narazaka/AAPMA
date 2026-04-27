using NUnit.Framework;
using UnityEditor.Animations;
using Narazaka.Unity.AAPMA;
using Narazaka.Unity.AAPMA.Editor;

namespace Narazaka.Unity.AAPMA.Editor.Tests
{
    public class LogicGateTests
    {
        static AAPSetting MakeBinaryGate(LogicType type) => new AAPSetting
        {
            Type = type,
            Input1 = new AAPParameter { Parameter = "A" },
            Input2 = new AAPParameter { Parameter = "B" },
            Output = new AAPParameter { Parameter = "Out" },
        };

        static AAPSetting MakeUnaryGate(LogicType type) => new AAPSetting
        {
            Type = type,
            Input1 = new AAPParameter { Parameter = "In" },
            Output = new AAPParameter { Parameter = "Out" },
        };

        static void AssertGateTruthTable(AnimatorController controller, (float a, float b, float expected)[] table)
        {
            foreach (var (a, b, expected) in table)
            {
                using var ev = new AnimatorEvaluator(controller);
                ev.SetFloat("A", a);
                ev.SetFloat("B", b);
                ev.Step(2);
                Assert.That(ev.GetFloat("Out"), Is.EqualTo(expected).Within(0.001f),
                    $"gate truth table mismatch: A={a}, B={b}");
            }
        }

        [Test]
        public void AndGate_TruthTable()
        {
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { MakeBinaryGate(LogicType.And) });
            AssertGateTruthTable(controller, new[]
            {
                (0f, 0f, 0f),
                (0f, 1f, 0f),
                (1f, 0f, 0f),
                (1f, 1f, 1f),
            });
        }

        [Test]
        public void OrGate_TruthTable()
        {
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { MakeBinaryGate(LogicType.Or) });
            AssertGateTruthTable(controller, new[]
            {
                (0f, 0f, 0f),
                (0f, 1f, 1f),
                (1f, 0f, 1f),
                (1f, 1f, 1f),
            });
        }

        [Test]
        public void NotGate_TruthTable()
        {
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { MakeUnaryGate(LogicType.Not) });

            using (var ev = new AnimatorEvaluator(controller))
            {
                ev.SetFloat("In", 0f);
                ev.Step(2);
                Assert.That(ev.GetFloat("Out"), Is.EqualTo(1f).Within(0.001f));
            }
            using (var ev = new AnimatorEvaluator(controller))
            {
                ev.SetFloat("In", 1f);
                ev.Step(2);
                Assert.That(ev.GetFloat("Out"), Is.EqualTo(0f).Within(0.001f));
            }
        }

        [Test]
        public void AndGate_HalfInput_LinearlyInterpolates()
        {
            // Input が 0.5 のとき BlendTree は線形補間する。
            // AND: A=0.5 で outer は (clip0=0) と (inner) を 50:50 で blend。
            // B=1 で inner は clip1=1。最終 Output = 0.5 * 0 + 0.5 * 1 = 0.5。
            var controller = new AAPMAPlugin.LayerPass().Build(new[] { MakeBinaryGate(LogicType.And) });

            using var ev = new AnimatorEvaluator(controller);
            ev.SetFloat("A", 0.5f);
            ev.SetFloat("B", 1f);
            ev.Step(2);

            Assert.That(ev.GetFloat("Out"), Is.EqualTo(0.5f).Within(0.01f));
        }
        static AAPSetting MakeArbitrary2Bit(float t00, float t01, float t10, float t11) => new AAPSetting
        {
            Type = LogicType.Arbitrary2Bit,
            Input1 = new AAPParameter { Parameter = "A" },
            Input2 = new AAPParameter { Parameter = "B" },
            Output = new AAPParameter { Parameter = "Out" },
            LogicTruth00 = t00,
            LogicTruth01 = t01,
            LogicTruth10 = t10,
            LogicTruth11 = t11,
        };

        [Test]
        public void Arbitrary2Bit_XorTruthTable()
        {
            // XOR: (0,0)→0, (0,1)→1, (1,0)→1, (1,1)→0
            var controller = new AAPMAPlugin.LayerPass().Build(new[]
            {
                MakeArbitrary2Bit(0f, 1f, 1f, 0f),
            });
            AssertGateTruthTable(controller, new[]
            {
                (0f, 0f, 0f),
                (0f, 1f, 1f),
                (1f, 0f, 1f),
                (1f, 1f, 0f),
            });
        }

        [Test]
        public void Arbitrary2Bit_NandTruthTable()
        {
            // NAND: (0,0)→1, (0,1)→1, (1,0)→1, (1,1)→0
            var controller = new AAPMAPlugin.LayerPass().Build(new[]
            {
                MakeArbitrary2Bit(1f, 1f, 1f, 0f),
            });
            AssertGateTruthTable(controller, new[]
            {
                (0f, 0f, 1f),
                (0f, 1f, 1f),
                (1f, 0f, 1f),
                (1f, 1f, 0f),
            });
        }

        [Test]
        public void Arbitrary2Bit_NonBinaryOutputs()
        {
            // 2-bit DAC のような非バイナリ出力でも動作する
            var controller = new AAPMAPlugin.LayerPass().Build(new[]
            {
                MakeArbitrary2Bit(0f, 0.33f, 0.66f, 1f),
            });
            AssertGateTruthTable(controller, new[]
            {
                (0f, 0f, 0f),
                (0f, 1f, 0.33f),
                (1f, 0f, 0.66f),
                (1f, 1f, 1f),
            });
        }
    }
}
