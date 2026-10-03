using UnityEngine;

namespace TacticalRPG.Core
{
    /// <summary>
    /// Esc tuşunun BU KAREDE bir sistem tarafından kullanıldığını işaretler.
    ///
    /// Neden gerekli: Esc'yi birden çok sistem dinliyor — menü gezgini (ayarları açar/kapatır) ve
    /// harita girdisi (yürüyüşü durdurur). Oyuncu yürürken Esc'ye basınca yalnız yürüyüş durmalı,
    /// ayar ekranı açılmamalı. Durduran sistem Esc'yi <see cref="Claim"/> ile sahiplenir; menü
    /// <see cref="ClaimedThisFrame"/> true ise o kareyi atlar. Sahiplenen taraf daha erken
    /// çalışmalı (<see cref="DefaultExecutionOrderAttribute"/>).
    /// </summary>
    public static class EscapeKeyClaim
    {
        private static int _frame = -1;

        public static void Claim() => _frame = Time.frameCount;

        public static bool ClaimedThisFrame => _frame == Time.frameCount;
    }
}
