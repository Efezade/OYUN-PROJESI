using UnityEngine;

namespace TacticalRPG.Grid
{
    /// <summary>
    /// Pointy-top hex geometrisi için sabitler ve prosedürel mesh üretici.
    /// OuterRadius = hexSize ile birebir örtüşür; HexCoordinate.ToWorldPosition ile uyumludur.
    /// </summary>
    public static class HexMetrics
    {
        // Dış yarıçap (merkez → köşe). HexGridManager._hexSize ile eşleşmeli.
        public const float OuterRadius = 1f;

        // İç yarıçap (merkez → kenar ortası) = OuterRadius * sqrt(3)/2
        public const float InnerRadius = OuterRadius * 0.866025404f;

        // Pointy-top köşe noktaları — XZ düzleminde, merkez orijin
        // Sıra: sağ-üst → üst → sol-üst → sol-alt → alt → sağ-alt
        public static readonly Vector3[] Corners =
        {
            new( InnerRadius,  0f,  OuterRadius * 0.5f),   // 0: sağ-üst
            new( 0f,           0f,  OuterRadius),            // 1: üst
            new(-InnerRadius,  0f,  OuterRadius * 0.5f),   // 2: sol-üst
            new(-InnerRadius,  0f, -OuterRadius * 0.5f),   // 3: sol-alt
            new( 0f,           0f, -OuterRadius),            // 4: alt
            new( InnerRadius,  0f, -OuterRadius * 0.5f),   // 5: sağ-alt
        };

        /// <summary>
        /// Düz, altı kenarlı hex Mesh'i prosedürel olarak üretir.
        /// scale: 0.95 → karolar arası görünür boşluk bırakır.
        /// </summary>
        public static Mesh CreateHexMesh(float scale = 0.95f)
        {
            var mesh = new Mesh { name = "HexMesh" };

            // 7 vertex: merkez (0) + 6 köşe (1-6)
            var vertices = new Vector3[7];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < 6; i++)
                vertices[i + 1] = Corners[i] * scale;

            // 6 üçgen — merkez etrafında fan; normal +Y olacak şekilde CW sıra
            var triangles = new int[18];
            for (int i = 0; i < 6; i++)
            {
                triangles[i * 3 + 0] = 0;
                triangles[i * 3 + 1] = (i + 1) % 6 + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            // Basit planar UV (gelecekte doku için)
            var uvs = new Vector2[7];
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < 6; i++)
                uvs[i + 1] = new Vector2(
                    (Corners[i].x / (OuterRadius * 2f) + 0.5f) * scale,
                    (Corners[i].z / (OuterRadius * 2f) + 0.5f) * scale
                );

            mesh.vertices  = vertices;
            mesh.triangles = triangles;
            mesh.uv        = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
