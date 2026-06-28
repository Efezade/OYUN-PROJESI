using UnityEngine;
using TacticalRPG.Grid;
using TacticalRPG.Data;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Bölüm 1 = bir KÜP (6 yüz). Her yüz kendi TileMapSO'su (ada). Oyuncu hep üst yüzdedir;
    /// SwitchToFace(n) grid'i o yüzün haritasıyla yeniden üretir + oyuncuyu yüzeye oturtur.
    ///
    /// Bu parça: 6 yüz verisi + küp komşuluğu + GEÇİCİ manuel alt çubuk (önizleme/test).
    /// Sonraki parça (buraya eklenecek): kenar SİYAH ÇERÇEVE (otomatik) + çerçeveye yürüyünce
    /// yöne göre komşu yüze otomatik geçiş + 90° küp dönüş hissi.
    /// </summary>
    public class CubeFaceManager : MonoBehaviour
    {
        [SerializeField] private HexGridManager   _grid;
        [SerializeField] private PlayerController _player;
        [Tooltip("Çubuk yalnız Overworld'de görünsün (opsiyonel).")]
        [SerializeField] private GameStateManager _state;
        [Tooltip("6 küp yüzü: index 0=Yüz1(Ön) … 5=Yüz6(Alt). SceneSetupTool doldurur.")]
        [SerializeField] private TileMapSO[]       _faces = new TileMapSO[6];

        public int CurrentFace { get; private set; } = 1;

        // Küp komşuluğu — [yüz-1][yön]; yön: 0=K(Kuzey/N) 1=D(Doğu/E) 2=G(Güney/S) 3=B(Batı/W).
        // (Otomatik kenar geçişi bu tabloyu kullanacak.)
        private static readonly int[,] Adj =
        {
            { 5, 2, 6, 4 }, // 1 Ön
            { 5, 3, 6, 1 }, // 2 Sağ
            { 5, 4, 6, 2 }, // 3 Arka
            { 5, 1, 6, 3 }, // 4 Sol
            { 3, 2, 1, 4 }, // 5 Üst
            { 1, 2, 3, 4 }, // 6 Alt
        };

        /// <summary>face'in dir yönündeki komşu yüzü (dir: 0=N,1=E,2=S,3=W).</summary>
        public int Neighbor(int face, int dir) =>
            (face >= 1 && face <= 6 && dir >= 0 && dir <= 3) ? Adj[face - 1, dir] : face;

        /// <summary>Yüz n'in (1-6) TileMapSO haritası (yoksa null).</summary>
        public TileMapSO GetFace(int n) =>
            (n >= 1 && n <= 6 && _faces != null && _faces.Length >= 6) ? _faces[n - 1] : null;

        /// <summary>Grid'i verilen yüzün haritasıyla yeniden üretir + oyuncuyu yüzeye oturtur.</summary>
        public void SwitchToFace(int face)
        {
            if (face < 1 || face > 6 || _faces == null || _faces.Length < 6) return;
            TileMapSO map = _faces[face - 1];
            if (map == null || _grid == null) return;

            CurrentFace = face;
            _grid.SetTileMap(map);                                          // yeni yüzü üret
            if (_player != null) _player.Initialize(_player.CurrentCoord);  // yeni yüzeye otur + sis
        }

        // (Manuel geçiş çubuğu KALDIRILDI — kullanıcı istemedi. Geçiş, kenar çerçevesine
        //  yürüyünce otomatik olacak; küp dönüş animasyonu ile. SwitchToFace o akışta çağrılır.)
    }
}
