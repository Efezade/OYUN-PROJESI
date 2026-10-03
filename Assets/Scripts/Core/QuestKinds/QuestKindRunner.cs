using System;
using UnityEngine;
using TacticalRPG.Data;
using TacticalRPG.Grid;

namespace TacticalRPG.Core
{
    /// <summary>
    /// SAVAŞ DIŞI bir görev tipini işleyen bileşenin sözleşmesi (Faz 1, madde 5.2).
    /// <see cref="ChapterNodeManager"/> görev karosuna girilince tipi işleyen runner'ı bulur,
    /// <see cref="Begin"/>'i çağırır ve sonucu bekler: başarı → düğüm tamamlanır (ödül + mühür +
    /// zincir ilerler), başarısızlık → düğüm açık kalır, oyuncu yeniden deneyebilir.
    ///
    /// Yeni bir tip (bulmaca, yeni mekanik) = yeni bir <see cref="QuestKindSO"/> + bunu işleyen
    /// yeni bir runner. Düğüm yöneticisine dokunulmaz.
    ///
    /// Neden arayüz değil soyut MonoBehaviour: Unity arayüz alanını serileştirmez; runner listesi
    /// Inspector'dan bağlanabilsin diye somut taban sınıf gerekiyor.
    /// </summary>
    public abstract class QuestKindRunner : MonoBehaviour
    {
        /// <summary>Bu runner verilen tipi işler mi?</summary>
        public abstract bool Handles(QuestKindSO kind);

        /// <summary>
        /// Göreve ŞU AN girilebilir mi? AP harcanmadan ÖNCE sorulur — girilemeyen bir göreve
        /// AP ödetilmesin. <paramref name="reason"/> düğüm panelinde gösterilir.
        /// </summary>
        public abstract bool CanBegin(QuestKindSO kind, out string reason);

        /// <summary>Görevi başlatır. Sonuç anında da, bir ekran kapanınca da dönebilir.</summary>
        public abstract void Begin(QuestKindSO kind, HexCoordinate coord, Action<bool> onFinished);
    }
}
