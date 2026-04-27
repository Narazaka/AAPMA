using System;
using UnityEditor.Animations;
using UnityEngine;

namespace Narazaka.Unity.AAPMA.Editor.Tests
{
    /// <summary>
    /// AnimatorController を一時 GameObject にアタッチし、Animator.Update でフレーム評価するテストヘルパ。
    /// using で囲むと自動で破棄される。
    /// </summary>
    public class AnimatorEvaluator : IDisposable
    {
        readonly GameObject _go;
        readonly Animator _animator;

        public Animator Animator => _animator;

        public AnimatorEvaluator(AnimatorController controller)
        {
            _go = new GameObject($"AAPMATest_{Guid.NewGuid()}");
            _animator = _go.AddComponent<Animator>();
            _animator.runtimeAnimatorController = controller;
            _animator.Update(0f); // 初期化
        }

        public void SetFloat(string name, float value) => _animator.SetFloat(name, value);
        public void SetBool(string name, bool value) => _animator.SetFloat(name, value ? 1f : 0f);
        public float GetFloat(string name) => _animator.GetFloat(name);

        /// <summary>1 フレーム = 1/60 秒で n フレーム進める。</summary>
        public void Step(int frames = 1)
        {
            for (var i = 0; i < frames; i++) _animator.Update(1f / 60f);
        }

        public void Dispose()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }
    }
}
