using System.Collections.Generic;
using System.Reflection;
using CapstoneDesign.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CapstoneDesign.Tests
{
    public sealed class PrototypeUiIconTests
    {
        [TestCase(PrototypeUiIcon.Symbol.Lock)]
        [TestCase(PrototypeUiIcon.Symbol.Leaf)]
        [TestCase(PrototypeUiIcon.Symbol.Journal)]
        [TestCase(PrototypeUiIcon.Symbol.Check)]
        [TestCase(PrototypeUiIcon.Symbol.Arrow)]
        [TestCase(PrototypeUiIcon.Symbol.Gear)]
        public void TallAndWideCellsPreserveTheSquareIconGeometry(PrototypeUiIcon.Symbol symbol)
        {
            var go = new GameObject("Synthetic icon", typeof(RectTransform));
            try
            {
                var icon = go.AddComponent<PrototypeUiIcon>();
                icon.symbol = symbol;
                var square = Vertices(icon, new Vector2(48, 48));
                foreach (var size in new[] { new Vector2(48, 98), new Vector2(120, 48) })
                {
                    var actual = Vertices(icon, size);
                    Assert.That(actual.Count, Is.EqualTo(square.Count));
                    for (int i = 0; i < square.Count; i++)
                        Assert.That(Vector3.Distance(actual[i].position, square[i].position), Is.LessThan(.001f),
                            "Non-square containers must preserve icon shape and center.");
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        static List<UIVertex> Vertices(PrototypeUiIcon icon, Vector2 size)
        {
            icon.rectTransform.pivot = new Vector2(.5f, .5f);
            icon.rectTransform.sizeDelta = size;
            using (var mesh = new VertexHelper())
            {
                typeof(PrototypeUiIcon).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(icon, new object[] { mesh });
                var vertices = new List<UIVertex>();
                mesh.GetUIVertexStream(vertices);
                return vertices;
            }
        }
    }
}
