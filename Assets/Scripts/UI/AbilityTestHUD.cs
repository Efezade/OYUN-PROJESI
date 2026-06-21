using UnityEngine;
using TacticalRPG.Core;

namespace TacticalRPG.UI
{
    /// <summary>
    /// Yetenek dikey dilimi için sade IMGUI test paneli (sadece gösterim, buton yok).
    /// Kam'ın yetenekleri (1/2/3), hazır yetenek, mana ve düşman HP'sini gösterir.
    /// Gerçek savaş UI'si Faz 2.3'te uGUI ile yapılacak.
    /// </summary>
    public class AbilityTestHUD : MonoBehaviour
    {
        [Header("Bağımlılıklar")]
        [SerializeField] private AbilityCaster  _caster;
        [SerializeField] private KamManaManager _kamMana;
        [SerializeField] private UnitManager    _unitManager;

        private string _lastMessage = "";
        private float  _lastMessageTime = -10f;

        private void OnEnable()
        {
            if (_caster != null) _caster.OnCastMessage += HandleMessage;
        }

        private void OnDisable()
        {
            if (_caster != null) _caster.OnCastMessage -= HandleMessage;
        }

        private void HandleMessage(string text)
        {
            _lastMessage     = text;
            _lastMessageTime = Time.time;
        }

        private void OnGUI()
        {
            const float w = 380f;
            var rect = new Rect(Screen.width - w - 12f, 12f, w, 230f);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("YETENEK TESTI  (Kam)");
            GUILayout.Space(2f);

            var abilities = _caster != null ? _caster.Abilities : null;
            if (abilities != null && abilities.Count > 0)
            {
                for (int i = 0; i < abilities.Count; i++)
                {
                    var  a     = abilities[i];
                    bool armed = _caster.ArmedAbility == a;
                    GUILayout.Label($"{(armed ? "> " : "  ")}[{i + 1}] {a.DisplayName}  —  " +
                                    $"{a.Effect} {a.Power}, mana {a.ManaCost}, menzil {a.Range}");
                }
            }
            else
            {
                GUILayout.Label("Yetenekler yuklenmedi (Faz 3 kurulumu / Kam yetenekleri?).");
            }

            GUILayout.Space(4f);
            if (_kamMana != null)
                GUILayout.Label($"Mana: {_kamMana.CurrentMana}/{_kamMana.MaxMana}");

            if (_caster != null)
                GUILayout.Label(_caster.HasArmedAbility
                    ? $"HAZIR: {_caster.ArmedAbility.DisplayName}  →  hedefe sol tikla"
                    : "1 / 2 / 3 ile yetenek sec  (Esc: vazgec)");

            Unit enemy = _unitManager != null ? _unitManager.GetFirstEnemy() : null;
            if (enemy != null)
            {
                string shield = enemy.Shield > 0 ? $"  (+{enemy.Shield} kalkan)" : "";
                GUILayout.Label($"Dusman [{enemy.DisplayName}]  HP: {enemy.CurrentHP}/{enemy.MaxHP}{shield}");
            }

            if (!string.IsNullOrEmpty(_lastMessage) && Time.time - _lastMessageTime < 3f)
                GUILayout.Label($"» {_lastMessage}");

            GUILayout.EndArea();
        }
    }
}
