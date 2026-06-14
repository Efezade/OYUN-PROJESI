using System;
using UnityEngine;

namespace TacticalRPG.Grid
{
    /// <summary>
    /// Axial koordinat sistemi (q, r). Pointy-top hex layout.
    /// Referans: https://www.redblobgames.com/grids/hexagons/
    /// </summary>
    [Serializable]
    public struct HexCoordinate : IEquatable<HexCoordinate>
    {
        public int Q;
        public int R;

        // Cube koordinatında S, Q+R+S=0 kuralından türetilir
        public int S => -Q - R;

        public static readonly HexCoordinate Zero = new(0, 0);

        // Pointy-top altı komşu yönü (axial)
        public static readonly HexCoordinate[] Directions =
        {
            new( 1,  0), new( 1, -1), new( 0, -1),
            new(-1,  0), new(-1,  1), new( 0,  1)
        };

        public HexCoordinate(int q, int r)
        {
            Q = q;
            R = r;
        }

        // Axial koordinatı dünya pozisyonuna çevirir (pointy-top, XZ düzlemi)
        public Vector3 ToWorldPosition(float hexSize)
        {
            float x = hexSize * (Mathf.Sqrt(3f) * Q + Mathf.Sqrt(3f) / 2f * R);
            float z = hexSize * (1.5f * R);
            return new Vector3(x, 0f, z);
        }

        // İki hex arasındaki adım mesafesi
        public int DistanceTo(HexCoordinate other)
        {
            return (Mathf.Abs(Q - other.Q)
                  + Mathf.Abs(R - other.R)
                  + Mathf.Abs(S - other.S)) / 2;
        }

        public HexCoordinate GetNeighbor(int directionIndex)
        {
            HexCoordinate dir = Directions[directionIndex % 6];
            return new HexCoordinate(Q + dir.Q, R + dir.R);
        }

        public bool Equals(HexCoordinate other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is HexCoordinate h && Equals(h);
        public override int GetHashCode() => Q * 397 ^ R;
        public static bool operator ==(HexCoordinate a, HexCoordinate b) => a.Equals(b);
        public static bool operator !=(HexCoordinate a, HexCoordinate b) => !a.Equals(b);
        public override string ToString() => $"Hex({Q},{R})";
    }
}
