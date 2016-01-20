using System;
using System.Linq;
using System.Collections.Generic;
using LeagueSharp;
using LeagueSharp.Common;
using Activator.Base;

namespace Activator.Handlers
{
    public class Projections
    {
        internal static int LastCastedTimeStamp;
        internal static Obj_AI_Hero Player = ObjectManager.Player;

        public static void Init()
        {
            MissileClient.OnCreate += MissileClient_OnSpellMissileCreate;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnUnitSpellCast;
        }

        private static void MissileClient_OnSpellMissileCreate(GameObject sender, EventArgs args)
        {
            #region FoW / Missile

            if (!sender.IsValid<MissileClient>())
            {
                return;
            }

            var missile = (MissileClient) sender;
            if (missile.SpellCaster != null && missile.SpellCaster.Team != Player.Team)
            {
                var startPos = missile.StartPosition.To2D();
                var endPos = missile.EndPosition.To2D();

                var data = Data.Spelldata.GetByMissileName(missile.SData.Name.ToLower());
                if (data == null)
                    return;

                var direction = (endPos - startPos).Normalized();

                if (startPos.Distance(endPos) > data.CastRange)
                    endPos = startPos + direction * data.CastRange;

                if (startPos.Distance(endPos) < data.CastRange && data.FixedRange)
                    endPos = startPos + direction * data.CastRange;

                foreach (var hero in Activator.Allies())
                {
                    // reset if needed
                    Essentials.ResetIncomeDamage(hero.Player);

                    var distance = (1000 * (startPos.Distance(hero.Player.ServerPosition) / data.MissileSpeed));
                    var endtime = -100 + Game.Ping / 2 + distance;

                    // setup projection
                    var proj = hero.Player.ServerPosition.To2D().ProjectOn(startPos, endPos);
                    var projdist = hero.Player.ServerPosition.To2D().Distance(proj.SegmentPoint);

                    // get the evade time 
                    var evadetime = (int) (1000 * 
                        (missile.SData.LineWidth - projdist + hero.Player.BoundingRadius) /hero.Player.MoveSpeed);

                    // check if hero on segment
                    if (missile.SData.LineWidth + hero.Player.BoundingRadius + 35 <= projdist)
                    {
                        continue;
                    }

                    if (data.Global || Activator.Origin.Item("evade").GetValue<bool>())
                    {
                        // ignore if can evade
                        if (hero.Player.NetworkId == Player.NetworkId)
                        {
                            if (hero.Player.CanMove && evadetime < endtime)
                            {
                                // check next player
                                continue;
                            }
                        }
                    }

                    if (Activator.Origin.Item(data.SDataName + "predict").GetValue<bool>())
                    {
                        hero.Attacker = missile.SpellCaster;
                        hero.HitTypes.Add(HitType.Spell);
                        hero.IncomeDamage += 1;

                        if (Activator.Origin.Item(data.SDataName + "danger").GetValue<bool>())
                            hero.HitTypes.Add(HitType.Danger);
                        if (Activator.Origin.Item(data.SDataName + "crowdcontrol").GetValue<bool>())
                            hero.HitTypes.Add(HitType.CrowdControl);
                        if (Activator.Origin.Item(data.SDataName + "ultimate").GetValue<bool>())
                            hero.HitTypes.Add(HitType.Ultimate);
                        if (Activator.Origin.Item(data.SDataName + "forceexhaust").GetValue<bool>())
                            hero.HitTypes.Add(HitType.ForceExhaust);

                        Utility.DelayAction.Add(Game.Ping + 250, () =>
                        {
                            if (hero.IncomeDamage > 0)
                                hero.IncomeDamage -= 1;

                            hero.HitTypes.Remove(HitType.Spell);

                            if (Activator.Origin.Item(data.SDataName + "danger").GetValue<bool>())
                                hero.HitTypes.Remove(HitType.Danger);
                            if (Activator.Origin.Item(data.SDataName + "crowdcontrol").GetValue<bool>())
                                hero.HitTypes.Remove(HitType.CrowdControl);
                            if (Activator.Origin.Item(data.SDataName + "ultimate").GetValue<bool>())
                                hero.HitTypes.Remove(HitType.Ultimate);
                            if (Activator.Origin.Item(data.SDataName + "forceexhaust").GetValue<bool>())
                                hero.HitTypes.Remove(HitType.ForceExhaust);
                        });
                    }
                }
            }

            #endregion
        }

        private static void Obj_AI_Base_OnUnitSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (Essentials.IsEpicMinion(sender) || sender.Name.StartsWith("Sru_Crab"))
                return;

            #region Hero

            if (sender.IsEnemy && sender.Type == GameObjectType.obj_AI_Hero)
            {
                var attacker = sender as Obj_AI_Hero;
                if (attacker != null && attacker.IsValid<Obj_AI_Hero>())
                {
                    LastCastedTimeStamp = Utils.GameTimeTickCount;

                    foreach (var hero in Activator.Allies())
                    {
                        Essentials.ResetIncomeDamage(hero.Player);

                        #region auto attack

                        if (args.SData.Name.ToLower().Contains("attack") && args.Target != null)
                        {
                            if (args.Target.NetworkId == hero.Player.NetworkId)
                            {
                                float dmg = 0;

                                dmg += (int) Math.Abs(attacker.GetAutoAttackDamage(hero.Player, true));

                                if (attacker.HasBuff("sheen"))
                                    dmg += (int) Math.Abs(attacker.GetAutoAttackDamage(hero.Player, true) +
                                                          attacker.GetCustomDamage("sheen", hero.Player));

                                if (attacker.HasBuff("lichbane"))
                                    dmg += (int) Math.Abs(attacker.GetAutoAttackDamage(hero.Player, true) +
                                                          attacker.GetCustomDamage("lichbane", hero.Player));

                                if (attacker.HasBuff("itemstatikshankcharge") &&
                                    attacker.GetBuff("itemstatikshankcharge").Count == 100)
                                    dmg += new[] { 62, 120, 200 }[attacker.Level / 3];

                                if (args.SData.Name.ToLower().Contains("crit"))
                                    dmg += (int) Math.Abs(attacker.GetAutoAttackDamage(hero.Player, true));

                                Utility.DelayAction.Add(100 - Game.Ping/2, () =>
                                {
                                    hero.Attacker = attacker;
                                    hero.HitTypes.Add(HitType.AutoAttack);
                                    hero.IncomeDamage += dmg;

                                    Utility.DelayAction.Add((int) (250 + attacker.AttackCastDelay *1000), () =>
                                    {
                                        hero.Attacker = null;
                                        hero.IncomeDamage -= dmg;
                                        hero.HitTypes.Remove(HitType.AutoAttack);
                                    });
                                });
                            }
                        }

                        #endregion

                        var data = Data.Spelldata.SomeSpells.Find(x => x.SDataName == args.SData.Name.ToLower());
                        if (data == null)
                            continue;

                        #region self/selfaoe

                        if (args.SData.TargettingType == SpellDataTargetType.Self ||
                            args.SData.TargettingType == SpellDataTargetType.SelfAoe)
                        {
                            var fromObj = ObjectManager.Get<GameObject>().FirstOrDefault(
                                x =>
                                    data.FromObject != null && !x.IsAlly && data.FromObject.Any(y => x.Name.Contains(y)));

                            var correctpos = fromObj?.Position ?? attacker.ServerPosition;
                            if (hero.Player.Distance(correctpos) > data.CastRange)
                                continue;

                            if (data.SDataName == "kalistaexpungewrapper" &&
                                !hero.Player.HasBuff("kalistaexpungemarker"))
                                continue;

                            var evadetime = 1000 * (data.CastRange - hero.Player.Distance(correctpos) +
                                                    hero.Player.BoundingRadius) / hero.Player.MoveSpeed;

                            if (Activator.Origin.Item("evade").GetValue<bool>())
                            {
                                // ignore if can evade
                                if (hero.Player.NetworkId == Player.NetworkId)
                                {
                                    if (hero.Player.CanMove && evadetime < data.Delay)
                                    {
                                        // check next player
                                        continue;
                                    }
                                }
                            }

                            if (!Activator.Origin.Item(data.SDataName + "predict").GetValue<bool>())
                                continue;

                            var dmg = (int) Math.Abs(attacker.GetSpellDamage(hero.Player, data.SDataName));
                            if (dmg == 0)
                            {
                                dmg = (int)(hero.Player.Health / hero.Player.MaxHealth * 5);
                                Console.WriteLine("Activator# - There is no Damage Lib for: " + data.SDataName);
                            }

                            // delay the spell a bit before missile endtime
                            Utility.DelayAction.Add((int) (data.Delay - (data.Delay * 0.7)), () =>
                            {
                                hero.Attacker = attacker;
                                hero.HitTypes.Add(HitType.Spell);
                                hero.IncomeDamage += dmg;

                                if (Activator.Origin.Item(data.SDataName + "danger").GetValue<bool>())
                                    hero.HitTypes.Add(HitType.Danger);
                                if (Activator.Origin.Item(data.SDataName + "crowdcontrol").GetValue<bool>())
                                    hero.HitTypes.Add(HitType.CrowdControl);
                                if (Activator.Origin.Item(data.SDataName + "ultimate").GetValue<bool>())
                                    hero.HitTypes.Add(HitType.Ultimate);
                                if (Activator.Origin.Item(data.SDataName + "forceexhaust").GetValue<bool>())
                                    hero.HitTypes.Add(HitType.ForceExhaust);

                                // lazy safe reset
                                Utility.DelayAction.Add((int) data.Delay + Game.Ping + 100, () =>
                                {
                                    hero.Attacker = null;
                                    hero.IncomeDamage -= dmg;
                                    hero.HitTypes.Remove(HitType.Spell);

                                    if (Activator.Origin.Item(data.SDataName + "danger").GetValue<bool>())
                                        hero.HitTypes.Remove(HitType.Danger);
                                    if (Activator.Origin.Item(data.SDataName + "crowdcontrol").GetValue<bool>())
                                        hero.HitTypes.Remove(HitType.CrowdControl);
                                    if (Activator.Origin.Item(data.SDataName + "ultimate").GetValue<bool>())
                                        hero.HitTypes.Remove(HitType.Ultimate);
                                    if (Activator.Origin.Item(data.SDataName + "forceexhaust").GetValue<bool>())
                                        hero.HitTypes.Remove(HitType.ForceExhaust);
                                });
                            });
                        }

                        #endregion

                        #region skillshot

                        if (args.SData.TargettingType == SpellDataTargetType.Cone ||
                            args.SData.TargettingType.ToString().Contains("Location") ||
                            args.SData.TargettingType == (SpellDataTargetType) 10 && data.SDataName == "azirq")
                        {
                            var fromObj =
                                ObjectManager.Get<GameObject>()
                                    .FirstOrDefault(
                                        x =>
                                            !x.IsAlly && data.FromObject != null &&
                                            data.FromObject.Any(y => x.Name.Contains(y)));                

                            var islineskillshot = args.SData.TargettingType == SpellDataTargetType.Cone ||
                                                  args.SData.LineWidth > 0;

                            var startpos = fromObj?.Position ?? attacker.ServerPosition;

                            var correctwidth = 0f;

                            if (args.SData.CastRadius > 0)
                                correctwidth = args.SData.CastRadius;

                            if (args.SData.CastRadius < 1 && args.SData.CastRadiusSecondary > 0)
                                correctwidth = args.SData.CastRadiusSecondary;

                            if (args.SData.CastRadiusSecondary < 1 && args.SData.CastRangeDisplayOverride > 0)
                                correctwidth = args.SData.CastRangeDisplayOverride;

                            if (islineskillshot && args.SData.TargettingType != SpellDataTargetType.Cone)
                                correctwidth = args.SData.LineWidth;

                            if (data.SDataName == "azirq")
                            {
                                correctwidth = 275f;
                                islineskillshot = true;
                            }

                            if (hero.Player.Distance(startpos) > data.CastRange)
                                continue;

                            var distance = (int) (1000 * (startpos.Distance(hero.Player.ServerPosition) / data.MissileSpeed));
                            var endtime = data.Delay - 100 + Game.Ping / 2f + distance - (Utils.GameTimeTickCount - LastCastedTimeStamp);

                            var iscone = args.SData.TargettingType == SpellDataTargetType.Cone;
                            var direction = (args.End.To2D() - startpos.To2D()).Normalized();
                            var endpos = startpos.To2D() + direction * startpos.To2D().Distance(args.End.To2D());

                            if (startpos.To2D().Distance(endpos) > data.CastRange)
                                endpos = startpos.To2D() + direction * data.CastRange;

                            if (startpos.To2D().Distance(endpos) < data.CastRange && data.FixedRange)
                                endpos = startpos.To2D() + direction * data.CastRange;

                            var proj = hero.Player.ServerPosition.To2D().ProjectOn(startpos.To2D(), endpos);
                            var projdist = hero.Player.ServerPosition.To2D().Distance(proj.SegmentPoint);

                            int evadetime = 0;

                            if (islineskillshot)
                                evadetime =
                                    (int)
                                        (1000 * (correctwidth - projdist + hero.Player.BoundingRadius) /
                                         hero.Player.MoveSpeed);

                            if (!islineskillshot)
                                evadetime =
                                    (int)
                                        (1000 *
                                         (correctwidth - hero.Player.Distance(startpos) + hero.Player.BoundingRadius) /
                                         hero.Player.MoveSpeed);

                            if ((!islineskillshot || iscone) &&
                                hero.Player.Distance(endpos) <= correctwidth + hero.Player.BoundingRadius + 35 ||
                                islineskillshot && correctwidth + hero.Player.BoundingRadius + 35 > projdist)
                            {
                                if (hero.Player.NetworkId == Player.NetworkId &&
                                   (data.Global || Activator.Origin.Item("evade").GetValue<bool>()))
                                {
                                    if (hero.Player.CanMove && evadetime < endtime)
                                    {
                                        continue;
                                    }
                                }

                                if (!Activator.Origin.Item(data.SDataName + "predict").GetValue<bool>())
                                    continue;

                                var dmg = (int) Math.Abs(attacker.GetSpellDamage(hero.Player, data.SDataName));
                                if (dmg == 0)
                                {
                                    dmg = (int) (hero.Player.Health / hero.Player.MaxHealth * 5);
                                    Console.WriteLine("Activator# - There is no Damage Lib for: " + data.SDataName);
                                }

                                Utility.DelayAction.Add((int) (endtime - (endtime * 0.7)), () =>
                                {
                                    hero.Attacker = attacker;
                                    hero.HitTypes.Add(HitType.Spell);
                                    hero.IncomeDamage += dmg;

                                    if (Activator.Origin.Item(data.SDataName + "danger").GetValue<bool>())
                                        hero.HitTypes.Add(HitType.Danger);
                                    if (Activator.Origin.Item(data.SDataName + "crowdcontrol").GetValue<bool>())
                                        hero.HitTypes.Add(HitType.CrowdControl);
                                    if (Activator.Origin.Item(data.SDataName + "ultimate").GetValue<bool>())
                                        hero.HitTypes.Add(HitType.Ultimate);
                                    if (Activator.Origin.Item(data.SDataName + "forceexhaust").GetValue<bool>())
                                        hero.HitTypes.Add(HitType.ForceExhaust);

                                    Utility.DelayAction.Add((int) (150 + data.Delay), () =>
                                    {
                                        hero.Attacker = null;
                                        hero.IncomeDamage -= dmg;
                                        hero.HitTypes.Remove(HitType.Spell);

                                        if (Activator.Origin.Item(data.SDataName + "danger").GetValue<bool>())
                                            hero.HitTypes.Remove(HitType.Danger);
                                        if (Activator.Origin.Item(data.SDataName + "crowdcontrol").GetValue<bool>())
                                            hero.HitTypes.Remove(HitType.CrowdControl);
                                        if (Activator.Origin.Item(data.SDataName + "ultimate").GetValue<bool>())
                                            hero.HitTypes.Remove(HitType.Ultimate);
                                        if (Activator.Origin.Item(data.SDataName + "forceexhaust").GetValue<bool>())
                                            hero.HitTypes.Remove(HitType.ForceExhaust);
                                    });
                                });
                            }
                        }

                        #endregion

                        #region unit type

                        if (args.SData.TargettingType == SpellDataTargetType.Unit ||
                            args.SData.TargettingType == SpellDataTargetType.SelfAndUnit)
                        {
                            if (args.Target == null || args.Target.Type != GameObjectType.obj_AI_Hero)
                                continue;

                            // check if is targeteting the hero on our table
                            if (hero.Player.NetworkId != args.Target.NetworkId)
                                continue;

                            // target spell dectection
                            if (hero.Player.Distance(attacker.ServerPosition) > data.CastRange)
                                continue;

                            var distance =
                                (int) (1000 * (attacker.Distance(hero.Player.ServerPosition) / data.MissileSpeed));
                            var endtime = data.Delay - 100 + Game.Ping / 2 + distance -
                                          (Utils.GameTimeTickCount - LastCastedTimeStamp);

                            if (!Activator.Origin.Item(data.SDataName + "predict").GetValue<bool>())
                                continue;

                            var dmg = (int) Math.Abs(attacker.GetSpellDamage(hero.Player, args.SData.Name));
                            if (dmg == 0)
                            {
                                dmg = (int)(hero.Player.Health / hero.Player.MaxHealth * 5);
                                Console.WriteLine("Activator# - There is no Damage Lib for: " + data.SDataName);
                            }

                            Utility.DelayAction.Add((int) (endtime - (endtime * 0.7)), () =>
                            {
                                hero.Attacker = attacker;
                                hero.HitTypes.Add(HitType.Spell);
                                hero.IncomeDamage += dmg;

                                if (Activator.Origin.Item(data.SDataName + "danger").GetValue<bool>())
                                    hero.HitTypes.Add(HitType.Danger);
                                if (Activator.Origin.Item(data.SDataName + "crowdcontrol").GetValue<bool>())
                                    hero.HitTypes.Add(HitType.CrowdControl);
                                if (Activator.Origin.Item(data.SDataName + "ultimate").GetValue<bool>())
                                    hero.HitTypes.Add(HitType.Ultimate);
                                if (Activator.Origin.Item(data.SDataName + "forceexhaust").GetValue<bool>())
                                    hero.HitTypes.Add(HitType.ForceExhaust);

                                // lazy reset
                                Utility.DelayAction.Add((int) endtime + Game.Ping + 100, () =>
                                {
                                    hero.Attacker = null;
                                    hero.IncomeDamage -= dmg;
                                    hero.HitTypes.Remove(HitType.Spell);

                                    if (Activator.Origin.Item(data.SDataName + "danger").GetValue<bool>())
                                        hero.HitTypes.Remove(HitType.Danger);
                                    if (Activator.Origin.Item(data.SDataName + "crowdcontrol").GetValue<bool>())
                                        hero.HitTypes.Remove(HitType.CrowdControl);
                                    if (Activator.Origin.Item(data.SDataName + "ultimate").GetValue<bool>())
                                        hero.HitTypes.Remove(HitType.Ultimate);
                                    if (Activator.Origin.Item(data.SDataName + "forceexhaust").GetValue<bool>())
                                        hero.HitTypes.Remove(HitType.ForceExhaust);
                                });
                            });
                        }

                        #endregion
                    }
                }
            }

            #endregion

            #region Turret

            if (sender.IsEnemy && sender.Type == GameObjectType.obj_AI_Turret && args.Target.Type == Player.Type)
            {
                var turret = sender as Obj_AI_Turret;
                if (turret != null && turret.IsValid<Obj_AI_Turret>())
                {
                    foreach (var hero in Activator.Allies())
                    {
                        if (args.Target.NetworkId == hero.Player.NetworkId && !hero.Immunity)
                        {
                            var dmg = (int) Math.Abs(turret.CalcDamage(hero.Player, Damage.DamageType.Physical,
                                turret.BaseAttackDamage + turret.FlatPhysicalDamageMod));

                            if (turret.Distance(hero.Player.ServerPosition) <= 900)
                            {
                                if (Player.Distance(hero.Player.ServerPosition) <= 1000)
                                {
                                    Utility.DelayAction.Add(450, () =>
                                    {
                                        hero.HitTypes.Add(HitType.TurretAttack);
                                        hero.TowerDamage += dmg;

                                        Utility.DelayAction.Add(150, () =>
                                        {
                                            hero.Attacker = null;
                                            hero.TowerDamage -= dmg;
                                            hero.HitTypes.Remove(HitType.TurretAttack);
                                        });
                                    });
                                }
                            }
                        }
                    }
                }
            }

            #endregion

            #region Minion

            if (sender.IsEnemy && sender.Type == GameObjectType.obj_AI_Minion && args.Target.Type == Player.Type)
            {
                var minion = sender as Obj_AI_Minion;
                if (minion != null && minion.IsValid<Obj_AI_Minion>())
                {
                    foreach (var hero in Activator.Allies())
                    {
                        if (hero.Player.NetworkId == args.Target.NetworkId && !hero.Immunity)
                        {
                            if (hero.Player.Distance(minion.ServerPosition) <= 750)
                            {
                                if (Player.Distance(hero.Player.ServerPosition) <= 1000)
                                {
                                    hero.HitTypes.Add(HitType.MinionAttack);
                                    hero.MinionDamage =
                                        (int) Math.Abs(minion.CalcDamage(hero.Player, Damage.DamageType.Physical,
                                            minion.BaseAttackDamage + minion.FlatPhysicalDamageMod));

                                    Utility.DelayAction.Add(250, () =>
                                    {
                                        hero.HitTypes.Remove(HitType.MinionAttack);
                                        hero.MinionDamage = 0;
                                    });
                                }
                            }
                        }
                    }
                }
            }

            #endregion

            #region Gangplank Barrel

            if (sender.IsEnemy && sender.Type == GameObjectType.obj_AI_Hero)
            {
                var attacker = sender as Obj_AI_Hero;
                if (attacker.ChampionName == "Gangplank" && attacker.IsValid<Obj_AI_Hero>())
                {
                    foreach (var hero in Activator.Allies())
                    {
                        Essentials.ResetIncomeDamage(hero.Player);
                        List<Obj_AI_Minion> gplist = new List<Obj_AI_Minion>();

                        gplist.AddRange(ObjectManager.Get<Obj_AI_Minion>()
                            .Where(
                                x =>
                                    x.CharData.BaseSkinName == "gangplankbarrel" &&
                                    x.Position.Distance(x.Position) <= 375 && x.IsHPBarRendered)
                            .OrderBy(y => y.Position.Distance(hero.Player.ServerPosition)));

                        for (var i = 0; i < gplist.Count; i++)
                        {
                            var obj = gplist[i];
                            if (hero.Player.Distance(obj.Position) > 375 || args.Target.Name != "Barrel")
                            {
                                continue;
                            }

                            var dmg = (int) Math.Abs(attacker.GetAutoAttackDamage(hero.Player, true) * 1.2 + 150);
                            if (args.SData.Name.ToLower().Contains("crit"))
                            {
                                dmg = dmg * 2;
                            }

                            Utility.DelayAction.Add(100 + (100 * i), () =>
                            {
                                hero.Attacker = attacker;
                                hero.HitTypes.Add(HitType.Spell);
                                hero.IncomeDamage += dmg;

                                Utility.DelayAction.Add(400 + (100 * i), delegate
                                {
                                    hero.Attacker = null;
                                    hero.HitTypes.Remove(HitType.Spell);
                                    hero.IncomeDamage -= dmg;
                                });
                            });
                        }
                    }
                }
            }

            #endregion      

            #region Items

            if (sender.IsEnemy && sender.Type == GameObjectType.obj_AI_Hero)
            {
                var attacker = sender as Obj_AI_Hero;
                if (attacker != null && attacker.IsValid<Obj_AI_Hero>())
                {
                    if (args.SData.TargettingType == SpellDataTargetType.Unit)
                    {
                        foreach (var hero in Activator.Allies())
                        {
                            Essentials.ResetIncomeDamage(hero.Player);

                            if (args.Target.NetworkId != hero.Player.NetworkId)
                                continue;
  
                            if (args.SData.Name.ToLower() == "bilgewatercutlass")
                            {
                                var dmg = (float) attacker.GetItemDamage(hero.Player, Damage.DamageItems.Bilgewater);

                                hero.Attacker = attacker;
                                hero.HitTypes.Add(HitType.AutoAttack);
                                hero.IncomeDamage += dmg;

                                Utility.DelayAction.Add(250, () =>
                                {
                                    hero.Attacker = null;
                                    hero.HitTypes.Remove(HitType.AutoAttack);
                                    hero.IncomeDamage -= dmg;
                                });
                            }

                            if (args.SData.Name.ToLower() == "itemswordoffeastandfamine")
                            {
                                var dmg = (float) attacker.GetItemDamage(hero.Player, Damage.DamageItems.Botrk);

                                hero.Attacker = attacker;
                                hero.HitTypes.Add(HitType.AutoAttack);
                                hero.IncomeDamage += dmg;

                                Utility.DelayAction.Add(250, () =>
                                {
                                    hero.Attacker = null;
                                    hero.HitTypes.Remove(HitType.AutoAttack);
                                    hero.IncomeDamage -= dmg;
                                });
                            }

                            if (args.SData.Name.ToLower() == "hextechgunblade")
                            {
                                var dmg = (float) attacker.GetItemDamage(hero.Player, Damage.DamageItems.Hexgun);

                                hero.Attacker = attacker;
                                hero.HitTypes.Add(HitType.AutoAttack);
                                hero.IncomeDamage += dmg;

                                Utility.DelayAction.Add(250, () =>
                                {
                                    hero.Attacker = null;
                                    hero.HitTypes.Remove(HitType.AutoAttack);
                                    hero.IncomeDamage -= dmg;
                                });
                            }
                        }
                    }
                }
            }

            #endregion

            #region LucianQ

            if (sender.IsEnemy && args.SData.Name == "LucianQ")
            {
                foreach (var a in Activator.Allies())
                {
                    var delay = ((350 - Game.Ping) / 1000f);

                    var herodir = (a.Player.ServerPosition - a.Player.Position).Normalized();
                    var expectedpos = args.Target.Position + herodir * a.Player.MoveSpeed * (delay);

                    if (args.Start.Distance(expectedpos) < 1100)
                        expectedpos = args.Target.Position +
                                     (args.Target.Position - sender.ServerPosition).Normalized() * 800;

                    var proj = a.Player.ServerPosition.To2D().ProjectOn(args.Start.To2D(), expectedpos.To2D());
                    var projdist = a.Player.ServerPosition.To2D().Distance(proj.SegmentPoint);

                    if (Activator.Origin.Item("lucianqpredict").GetValue<bool>())
                    {
                        if (100 + a.Player.BoundingRadius > projdist)
                        {
                            a.Attacker = sender;
                            a.HitTypes.Add(HitType.Spell);
                            a.IncomeDamage += 1;

                            if (Activator.Origin.Item("lucianqdanger").GetValue<bool>())
                                a.HitTypes.Add(HitType.Danger);
                            if (Activator.Origin.Item("lucianqcrowdcontrol").GetValue<bool>())
                                a.HitTypes.Add(HitType.CrowdControl);
                            if (Activator.Origin.Item("lucianqultimate").GetValue<bool>())
                                a.HitTypes.Add(HitType.Ultimate);
                            if (Activator.Origin.Item("lucianqforceexhaust").GetValue<bool>())
                                a.HitTypes.Add(HitType.ForceExhaust);

                            Utility.DelayAction.Add(350 - Game.Ping, () =>
                            {
                                if (a.IncomeDamage > 0)
                                    a.IncomeDamage -= 1;

                                a.Attacker = null;
                                a.HitTypes.Remove(HitType.Spell);

                                if (Activator.Origin.Item("lucianqdanger").GetValue<bool>())
                                    a.HitTypes.Remove(HitType.Danger);
                                if (Activator.Origin.Item("lucianqcrowdcontrol").GetValue<bool>())
                                    a.HitTypes.Remove(HitType.CrowdControl);
                                if (Activator.Origin.Item("lucianqultimate").GetValue<bool>())
                                    a.HitTypes.Remove(HitType.Ultimate);
                                if (Activator.Origin.Item("lucianqforceexhaust").GetValue<bool>())
                                    a.HitTypes.Remove(HitType.ForceExhaust);
                            });
                        }
                    }
                }
            }

            #endregion
     
        }
    }
}