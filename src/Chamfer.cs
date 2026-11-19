using UnityEngine;
using UnityEngine.UI;

namespace Operations.Client
{
    /// <summary>Cuts the top-left and bottom-right corners off a plain (sprite-less) Image: the "modern" plate look.</summary>
    internal sealed class Chamfer : BaseMeshEffect
    {
        public float Size = 10f;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount != 4) return; // only simple quads
            UIVertex v = default(UIVertex);
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                x0 = Mathf.Min(x0, v.position.x); x1 = Mathf.Max(x1, v.position.x);
                y0 = Mathf.Min(y0, v.position.y); y1 = Mathf.Max(y1, v.position.y);
            }
            float c = Mathf.Min(Size, (x1 - x0) * 0.5f, (y1 - y0) * 0.5f);
            if (c < 1f) return;
            vh.PopulateUIVertex(ref v, 0);
            UIVertex t = v; t.uv0 = Vector2.zero; t.uv1 = Vector2.zero;
            Vector3[] p =
            {
                new Vector3(x0, y0, 0), new Vector3(x0, y1 - c, 0), new Vector3(x0 + c, y1, 0),
                new Vector3(x1, y1, 0), new Vector3(x1, y0 + c, 0), new Vector3(x1 - c, y0, 0)
            };
            vh.Clear();
            for (int i = 0; i < p.Length; i++) { t.position = p[i]; vh.AddVert(t); }
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(0, 2, 3); vh.AddTriangle(0, 3, 4); vh.AddTriangle(0, 4, 5);
        }
    }
}
