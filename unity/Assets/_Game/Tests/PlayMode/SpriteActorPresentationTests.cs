using System.Collections;
using System.Collections.Generic;
using Abbey.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abbey.Tests.PlayMode
{
    public sealed class SpriteActorPresentationTests
    {
        [UnityTest]
        public IEnumerator MovingRoot_SelectsDirectionalWalkFrames_WithoutWritingGameplayTransform()
        {
            var created = new List<Object>();
            try
            {
                Texture2D texture = Track(created, new Texture2D(12, 2));
                Sprite south = MakeSprite(created, texture, 0);
                Sprite north = MakeSprite(created, texture, 2);
                Sprite eastIdle = MakeSprite(created, texture, 4);
                Sprite west = MakeSprite(created, texture, 6);
                Sprite eastWalkA = MakeSprite(created, texture, 8);
                Sprite eastWalkB = MakeSprite(created, texture, 10);

                SpriteProjectionCatalog catalog = Track(
                    created, ScriptableObject.CreateInstance<SpriteProjectionCatalog>());
                catalog.entries.Add(new SpriteProjectionEntry
                {
                    assetId = "actor",
                    sprite = south,
                    southSprite = south,
                    northSprite = north,
                    eastSprite = eastIdle,
                    westSprite = west,
                    southWalk = new[] { south },
                    northWalk = new[] { north },
                    eastWalk = new[] { eastWalkA, eastWalkB },
                    westWalk = new[] { west },
                    walkFrameSeconds = 0.01f
                });

                GameObject cameraObject = Track(created, new GameObject("Camera"));
                Camera camera = cameraObject.AddComponent<Camera>();
                GameObject bootstrapObject = Track(created, new GameObject("Projection"));
                SpriteProjectionBootstrap bootstrap =
                    bootstrapObject.AddComponent<SpriteProjectionBootstrap>();
                bootstrap.Configure(catalog, camera);
                GameObject root = Track(created, GameObject.CreatePrimitive(PrimitiveType.Cube));
                root.name = "MovingActor";
                Assert.IsTrue(bootstrap.Register(root, "actor", stableId: "moving-actor"));

                yield return null;
                root.transform.position += Vector3.right;
                Vector3 authoredPosition = root.transform.position;
                yield return null;

                SpriteRenderer renderer = SpriteProjectionFactory.GetSpriteRenderer(root);
                Assert.That(renderer.sprite, Is.EqualTo(eastWalkA).Or.EqualTo(eastWalkB));
                Assert.AreEqual(authoredPosition, root.transform.position,
                    "presentation must never write the gameplay root transform");

                yield return null;
                Assert.AreSame(eastIdle, renderer.sprite,
                    "a stationary actor keeps its last facing but returns to the idle frame");
                Assert.AreEqual(authoredPosition, root.transform.position);
            }
            finally
            {
                for (int i = created.Count - 1; i >= 0; i--)
                {
                    if (created[i] != null)
                    {
                        Object.Destroy(created[i]);
                    }
                }
            }
        }

        static Sprite MakeSprite(List<Object> created, Texture2D texture, int x)
        {
            return Track(created, Sprite.Create(
                texture, new Rect(x, 0f, 2f, 2f), new Vector2(0.5f, 0f), 16f));
        }

        static T Track<T>(List<Object> created, T value) where T : Object
        {
            created.Add(value);
            return value;
        }
    }
}
