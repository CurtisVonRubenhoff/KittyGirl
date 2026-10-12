//
// Klak - Utilities for creative coding with Unity
//
// Copyright (C) 2016 Keijiro Takahashi
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
using UnityEngine;
using Klak.Math;

namespace Klak.Motion
{
    [AddComponentMenu("Klak/Motion/Brownian Motion")]
    public class BrownianMotion : MonoBehaviour
    {
        #region Editable Properties

        [SerializeField] bool _enablePositionNoise = true;
        [SerializeField] bool _enableRotationNoise = true;

        [SerializeField] float _positionFrequency = 0.2f;
        [SerializeField] float _rotationFrequency = 0.2f;

        [SerializeField] float _positionAmplitude = 0.5f;
        [SerializeField] float _rotationAmplitude = 10.0f;

        [SerializeField] Vector3 _positionScale = Vector3.one;
        [SerializeField] Vector3 _rotationScale = new Vector3(1, 1, 0);

        [SerializeField, Range(0, 8)] int _positionFractalLevel = 3;
        [SerializeField, Range(0, 8)] int _rotationFractalLevel = 3;

        #endregion

        #region Public Properties And Methods

        public bool enablePositionNoise {
            get { return _enablePositionNoise; }
            set { _enablePositionNoise = value; }
        }

        public bool enableRotationNoise {
            get { return _enableRotationNoise; }
            set { _enableRotationNoise = value; }
        }

        public float positionFrequency {
            get { return _positionFrequency; }
            set { _positionFrequency = value; }
        }

        public float rotationFrequency {
            get { return _rotationFrequency; }
            set { _rotationFrequency = value; }
        }

        public float positionAmplitude {
            get { return _positionAmplitude; }
            set { _positionAmplitude = value; }
        }

        public float rotationAmplitude {
            get { return _rotationAmplitude; }
            set { _rotationAmplitude = value; }
        }

        public Vector3 positionScale {
            get { return _positionScale; }
            set { _positionScale = value; }
        }

        public Vector3 rotationScale {
            get { return _rotationScale; }
            set { _rotationScale = value; }
        }

        public int positionFractalLevel {
            get { return _positionFractalLevel; }
            set { _positionFractalLevel = value; }
        }

        public int rotationFractalLevel {
            get { return _rotationFractalLevel; }
            set { _rotationFractalLevel = value; }
        }

        public void Rehash()
        {
            for (var i = 0; i < 6; i++)
                _time[i] = Random.Range(-10000.0f, 0.0f);
        }

        /// <summary>
        /// Moves the center of the position noise toward a world-space target.
        /// This lets gameplay motion and Brownian position noise coexist without
        /// the noise snapping the object back to the point where it was enabled.
        /// </summary>
        public void MovePositionAnchorTowards(Vector3 worldTarget, float maxDistanceDelta)
        {
            if (!IsFinite(worldTarget) || !IsFinite(maxDistanceDelta) || maxDistanceDelta < 0f)
                return;

            var parent = transform.parent;
            if (parent != null && !HasInvertibleScale(parent.lossyScale))
                return;

            var worldAnchor = parent == null
                ? _initialPosition
                : parent.TransformPoint(_initialPosition);
            if (!IsFinite(worldAnchor))
                return;

            worldAnchor = Vector3.MoveTowards(worldAnchor, worldTarget, maxDistanceDelta);
            var localAnchor = parent == null
                ? worldAnchor
                : parent.InverseTransformPoint(worldAnchor);
            if (IsFinite(localAnchor))
                _initialPosition = localAnchor;
        }

        static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        static bool HasInvertibleScale(Vector3 scale)
        {
            const float epsilon = 0.000001f;
            return IsFinite(scale) &&
                Mathf.Abs(scale.x) > epsilon &&
                Mathf.Abs(scale.y) > epsilon &&
                Mathf.Abs(scale.z) > epsilon;
        }

        #endregion

        #region Private Members

        const float _fbmNorm = 1 / 0.75f;

        Vector3 _initialPosition;
        Quaternion _initialRotation;
        float[] _time;

        #endregion

        #region MonoBehaviour Functions

        void Start()
        {
            _time = new float[6];
            Rehash();
        }

        void OnEnable()
        {
            _initialPosition = transform.localPosition;
            _initialRotation = transform.localRotation;
        }

        void Update()
        {
            var dt = Time.deltaTime;

            if (_enablePositionNoise)
            {
                for (var i = 0; i < 3; i++)
                    _time[i] += _positionFrequency * dt;

                var n = new Vector3(
                    Perlin.Fbm(_time[0], _positionFractalLevel),
                    Perlin.Fbm(_time[1], _positionFractalLevel),
                    Perlin.Fbm(_time[2], _positionFractalLevel));

                n = Vector3.Scale(n, _positionScale);
                n *= _positionAmplitude * _fbmNorm;

                transform.localPosition = _initialPosition + n;
            }

            if (_enableRotationNoise)
            {
                for (var i = 0; i < 3; i++)
                    _time[i + 3] += _rotationFrequency * dt;

                var n = new Vector3(
                    Perlin.Fbm(_time[3], _rotationFractalLevel),
                    Perlin.Fbm(_time[4], _rotationFractalLevel),
                    Perlin.Fbm(_time[5], _rotationFractalLevel));

                n = Vector3.Scale(n, _rotationScale);
                n *= _rotationAmplitude * _fbmNorm;

                transform.localRotation =
                    Quaternion.Euler(n) * _initialRotation;
            }
        }

        #endregion
    }
}
