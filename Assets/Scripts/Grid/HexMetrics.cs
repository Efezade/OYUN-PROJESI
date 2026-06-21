using UnityEngine;

namespace TacticalRPG.Grid
{
    public static class HexMetrics
    {
        public const float OuterRadius = 1f;
        public const float InnerRadius = OuterRadius * 0.866025404f;

        // Karo kalınlığı — kendi assetlerinle değiştirene kadar placeholder yüksekliği
        public const float TileHeight = 0.3f;

        // Pointy-top köşe noktaları — XZ düzleminde, merkez orijin
        public static readonly Vector3[] Corners =
        {
            new( InnerRadius,  0f,  OuterRadius * 0.5f),
            new( 0f,           0f,  OuterRadius),
            new(-InnerRadius,  0f,  OuterRadius * 0.5f),
            new(-InnerRadius,  0f, -OuterRadius * 0.5f),
            new( 0f,           0f, -OuterRadius),
            new( InnerRadius,  0f, -OuterRadius * 0.5f),
        };

        /// <summary>
        /// İzometrik 3D için hex prism (altılı silindir dilimi).
        /// Üst yüz + 6 yan yüz; her yüz için ayrı vertex → doğru normal hesabı.
        /// scale=0.95 → karolar arası boşluk; height=TileHeight.
        /// </summary>
        public static Mesh CreateHexMesh(float scale = 0.95f)
        {
            float h = TileHeight;
            var mesh = new Mesh { name = "HexPrismMesh" };

            // Vertex düzeni:
            //  [0]     : üst merkez
            //  [1..6]  : üst halka (y=h)
            //  [7..30] : 6 yan yüz × 4 benzersiz vertex = 24 vertex (doğru normal için ayrı)
            // Toplam: 31 vertex

            var verts = new Vector3[31];
            verts[0] = new Vector3(0f, h, 0f);

            for (int i = 0; i < 6; i++)
            {
                Vector3 c = Corners[i] * scale;
                verts[i + 1] = new Vector3(c.x, h,  c.z);
            }

            for (int i = 0; i < 6; i++)
            {
                Vector3 ca = Corners[i]           * scale;
                Vector3 cb = Corners[(i + 1) % 6] * scale;
                int b = 7 + i * 4;
                verts[b + 0] = new Vector3(ca.x, h,  ca.z); // üst-a
                verts[b + 1] = new Vector3(cb.x, h,  cb.z); // üst-b
                verts[b + 2] = new Vector3(cb.x, 0f, cb.z); // alt-b
                verts[b + 3] = new Vector3(ca.x, 0f, ca.z); // alt-a
            }

            // Toplam: 6 üst üçgen (18) + 6×2 yan üçgen (36) = 54 index
            var tris = new int[54];
            int idx = 0;

            // Üst yüz — normal +Y (CCW yukarıdan)
            for (int i = 0; i < 6; i++)
            {
                tris[idx++] = 0;
                tris[idx++] = (i + 1) % 6 + 1;
                tris[idx++] = i + 1;
            }

            // Yan yüzler — dışa bakan normal (doğrulanmış winding)
            for (int i = 0; i < 6; i++)
            {
                int b = 7 + i * 4;
                tris[idx++] = b;     tris[idx++] = b + 2; tris[idx++] = b + 3;
                tris[idx++] = b;     tris[idx++] = b + 1; tris[idx++] = b + 2;
            }

            mesh.vertices  = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
