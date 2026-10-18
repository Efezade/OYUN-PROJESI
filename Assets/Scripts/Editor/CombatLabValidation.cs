using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using TacticalRPG.Prototypes.CombatLab;

namespace TacticalRPG.Editor
{
    public static class CombatLabValidation
    {
        private static int _checks;
        private static void Check(bool condition, string message)
        { _checks++; if (!condition) throw new Exception("CombatLab validation: " + message); }

        [MenuItem("TacticalRPG/Prototipler/A-B Mantik Kontrolleri", false, 301)]
        public static void RunChecks()
        {
            _checks = 0;
            var config = ScriptableObject.CreateInstance<CombatLabConfig>();
            var a = new CombatLabSimulation(config); var b = new CombatLabSimulation(config);
            a.Start(LabMode.Tactical); b.Start(LabMode.Realtime);
            Check(a.Units.Count == b.Units.Count && a.LivingEnemies == 4, "same starting roster");
            for (int i = 0; i < a.Units.Count; i++)
                Check(a.Units[i].Role == b.Units[i].Role && a.Units[i].Position == b.Units[i].Position && a.Units[i].Health == b.Units[i].Health && a.Units[i].Damage == b.Units[i].Damage, "matched unit " + i);
            Check(a.Cells.Count == 91, "radius-5 hex arena");
            Check(!a.HasLine(new LabHex(-1, -2).World(config.TileRadius), new LabHex(1, -2).World(config.TileRadius)), "pillar blocks ranged line");
            Check(!a.TryMove(new LabHex(0, -2)), "cannot move onto pillar");
            Check(!a.TryMove(a.Nearest(a.Units[1].Position)), "cannot occupy allied hex");
            float mana = a.Mana;
            Check(!a.TrySkill(LabSkill.Pull, new Vector2(100, 100)) && a.Mana == mana && !a.Kam.Acted, "invalid cast does not spend action/mana");
            Check(a.TrySkill(LabSkill.Pull, Vector2.zero), "valid gravity cast");
            Check(a.Mana == mana - config.PullCost, "cast spends specified cost");
            Check(!a.TrySkill(LabSkill.Burst, Vector2.zero), "one Kam action per tactical round");
            int marked = 0; foreach (var u in a.Units) if (!u.Ally && u.Mark > 0) marked++;
            Check(marked > 0, "gravity marks targets");
            Check(a.EndTurn(), "enemy phase starts");
            Check(!a.TryMove(new LabHex(-2, 0)), "commands rejected in enemy phase");
            for (int i = 0; i < 200 && a.Phase == LabPhase.Enemy; i++) a.Tick(.05f, Vector2.zero);
            Check(a.Phase == LabPhase.Player && a.Round == 2, "enemy phase returns control");
            Check(!a.Kam.Acted && a.Mana > mana - config.PullCost, "round resets action and regenerates mana");
            a.TogglePause(); float elapsed = a.Elapsed; Vector2 p = a.Kam.Position;
            a.Tick(.05f, Vector2.right);
            Check(a.Elapsed == elapsed && a.Kam.Position == p && !a.TrySkill(LabSkill.Pull, Vector2.zero), "pause freezes model and actions");
            a.Start(LabMode.Tactical);
            Check(a.Mana == config.Mana && a.Kills == 0 && a.Chains == 0 && a.Round == 1 && !a.Paused && a.Units.Count == 7, "restart clears transient state");
            b.Tick(.05f, Vector2.right); Check(b.Kam.Position != p, "real-time movement works");
            float rtMana = b.Mana; Check(b.TrySkill(LabSkill.Pull, Vector2.zero), "real-time gravity");
            Check(!b.TrySkill(LabSkill.Pull, Vector2.zero) && b.Mana == rtMana - config.PullCost, "cooldown prevents repeat spending");
            b.TogglePause(); p = b.Kam.Position; elapsed = b.Elapsed;
            for (int i = 0; i < 50; i++) b.Tick(.05f, Vector2.one);
            Check(b.Kam.Position == p && b.Elapsed == elapsed, "real-time pause freezes all ticks");
            Check(!a.ChooseAugment(0), "no reward outside reward phase");
            // Controlled high-health fixture verifies all three waves and reward transitions,
            // not balance or whether the prototype is enjoyable for a human.
            var so = new SerializedObject(config); so.FindProperty("_kamHealth").intValue = 2000; so.FindProperty("_guardHealth").intValue = 2000; so.FindProperty("_archerHealth").intValue = 2000; so.ApplyModifiedPropertiesWithoutUndo();
            var tactical = new CombatLabSimulation(config); tactical.Start(LabMode.Tactical);
            int rewardsA = PlayFixture(tactical, config, 16000);
            Check(tactical.Phase == LabPhase.Won && rewardsA == 2 && tactical.Kills == 11, "tactical win through 3 waves and 2 rewards");
            var realtime = new CombatLabSimulation(config); realtime.Start(LabMode.Realtime);
            int rewardsB = PlayFixture(realtime, config, 20000);
            Check(realtime.Phase == LabPhase.Won && rewardsB == 2 && realtime.Kills == 11, "real-time win through 3 waves and 2 rewards");
            Check(tactical.Chains > 0 && realtime.Chains > 0, "both modes exercise chain reactions");
            Check(!tactical.TrySkill(LabSkill.Burst, Vector2.zero) && !realtime.TryAttack(3), "terminal states reject combat input");
            float frozen = realtime.Elapsed; realtime.Tick(.05f, Vector2.one); Check(realtime.Elapsed == frozen, "terminal result remains stable");
            so = new SerializedObject(config); so.FindProperty("_kamHealth").intValue = 1; so.FindProperty("_guardHealth").intValue = 1; so.FindProperty("_archerHealth").intValue = 1; so.ApplyModifiedPropertiesWithoutUndo();
            var loss = new CombatLabSimulation(config); loss.Start(LabMode.Tactical);
            for (int i = 0; i < 3000 && loss.Phase != LabPhase.Lost; i++) { if (loss.Phase == LabPhase.Player) loss.EndTurn(); loss.Tick(.05f, Vector2.zero); }
            Check(loss.Phase == LabPhase.Lost, "Kam death ends encounter");
            Check(!loss.ChooseAugment(0), "loss never awards augment");
            UnityEngine.Object.DestroyImmediate(config);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/combat-lab-validation.json", "{\"checks\":" + _checks + ",\"passed\":true,\"scope\":\"simulation only; no human enjoyment or balance claim\",\"tacticalChains\":" + tactical.Chains + ",\"realtimeChains\":" + realtime.Chains + "}");
            Debug.Log("COMBAT_LAB_VALIDATION_OK " + _checks);
        }
        private static int PlayFixture(CombatLabSimulation s, CombatLabConfig c, int limit)
        {
            int rewards = 0;
            for (int step = 0; step < limit && s.Phase != LabPhase.Won && s.Phase != LabPhase.Lost; step++)
            {
                if (s.Phase == LabPhase.Augment) { Check(s.ChooseAugment(rewards % 3), "valid reward transition"); rewards++; continue; }
                if (s.Phase == LabPhase.Player)
                {
                    if (s.Mode == LabMode.Tactical)
                    {
                        s.TrySkill(LabSkill.Pull, Vector2.zero);
                        for (int i = 2; i >= 0; i--)
                        {
                            var u = s.Units[i]; if (!u.Alive || s.Phase != LabPhase.Player) continue; s.Select(i);
                            LabUnit target = null; float best = float.PositiveInfinity;
                            foreach (var e in s.Units) if (!e.Ally && e.Alive && Vector2.Distance(u.Position, e.Position) < best) { target = e; best = Vector2.Distance(u.Position, e.Position); }
                            if (target == null) break;
                            if (!s.CanHit(u, target))
                            {
                                var reach = s.Reachable(u, c.MoveHexes); LabHex chosen = s.Nearest(u.Position);
                                foreach (var candidate in reach)
                                {
                                    float dist = Vector2.Distance(candidate.Key.World(c.TileRadius), target.Position);
                                    if (dist < best) { best = dist; chosen = candidate.Key; }
                                }
                                s.TryMove(chosen);
                            }
                            s.TryAttack(target.Id);
                        }
                        s.EndTurn();
                    }
                    else { s.TrySkill(LabSkill.Pull, Vector2.zero); s.TrySkill(LabSkill.Burst, s.Kam.Position); }
                }
                Vector2 movement = Vector2.zero;
                if (s.Mode == LabMode.Realtime)
                {
                    foreach (var e in s.Units) if (!e.Ally && e.Alive) { if (Vector2.Distance(s.Kam.Position, e.Position) > 2.4f) movement = e.Position - s.Kam.Position; break; }
                }
                s.Tick(.05f, movement);
            }
            return rewards;
        }
    }
}
