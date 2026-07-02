using System.Collections;
using System.Collections.Generic;
using Abbey.CameraRig;
using Abbey.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    public class IsoCameraPlayModeTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawned.Clear();
            EventBus.ResetAll();
            GameEventLog.Clear();
        }

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        IsoCameraController SpawnRig()
        {
            var go = Spawn("TestIsoCamera");
            go.AddComponent<Camera>();
            return go.AddComponent<IsoCameraController>();
        }

        static void AssertRotationLocked(Transform t)
        {
            var expected = Quaternion.Euler(IsoCameraController.Pitch, IsoCameraController.Yaw, 0f);
            Assert.Less(Quaternion.Angle(t.rotation, expected), 0.01f,
                $"Camera rotation must stay locked at pitch {IsoCameraController.Pitch} / yaw {IsoCameraController.Yaw}");
        }

        [UnityTest]
        public IEnumerator Camera_IsOrthographic_AndLockedToIsoAngle()
        {
            var rig = SpawnRig();
            yield return null;

            Assert.IsTrue(rig.TargetCamera.orthographic, "Camera must be orthographic");
            AssertRotationLocked(rig.transform);
        }

        [UnityTest]
        public IEnumerator ExternalRotation_IsReLockedNextFrame()
        {
            var rig = SpawnRig();
            yield return null;

            rig.transform.rotation = Quaternion.Euler(10f, 200f, 45f);
            yield return null;

            AssertRotationLocked(rig.transform);
        }

        [UnityTest]
        public IEnumerator PerspectiveSwitch_IsRevertedNextFrame()
        {
            var rig = SpawnRig();
            yield return null;

            rig.TargetCamera.orthographic = false;
            yield return null;

            Assert.IsTrue(rig.TargetCamera.orthographic);
        }

        [UnityTest]
        public IEnumerator Zoom_ClampsOrthographicSizeOnly()
        {
            var rig = SpawnRig();
            yield return null;

            var config = rig.Config;
            rig.SetZoom(config.cameraMaxOrthoSize + 100f);
            Assert.AreEqual(config.cameraMaxOrthoSize, rig.TargetCamera.orthographicSize, 1e-4f);

            rig.SetZoom(0.0001f);
            Assert.AreEqual(config.cameraMinOrthoSize, rig.TargetCamera.orthographicSize, 1e-4f);

            yield return null;
            Assert.IsTrue(rig.TargetCamera.orthographic);
            AssertRotationLocked(rig.transform);
        }

        [UnityTest]
        public IEnumerator FollowTarget_MovesFocusToTargetXZ()
        {
            var rig = SpawnRig();
            var target = Spawn("FollowMe");
            target.transform.position = new Vector3(12f, 3f, -7f);

            rig.SetFollowTarget(target.transform);
            yield return null;

            Assert.AreEqual(12f, rig.FocusPoint.x, 1e-3f);
            Assert.AreEqual(0f, rig.FocusPoint.y, 1e-3f);
            Assert.AreEqual(-7f, rig.FocusPoint.z, 1e-3f);
            AssertRotationLocked(rig.transform);
        }

        [UnityTest]
        public IEnumerator WorldBounds_ClampFocusPoint()
        {
            var rig = SpawnRig();
            yield return null;

            rig.SetWorldBounds(new Vector2(-5f, -5f), new Vector2(5f, 5f));
            rig.FocusOn(new Vector3(100f, 0f, -100f));
            yield return null;

            Assert.AreEqual(5f, rig.FocusPoint.x, 1e-3f);
            Assert.AreEqual(-5f, rig.FocusPoint.z, 1e-3f);
        }
    }
}
