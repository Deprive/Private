﻿using System;
using System.Linq;
using Activator.Base;
using LeagueSharp;
using LeagueSharp.Common;

namespace Activator.Summoners
{
    internal class exhuast : CoreSum
    {
        internal override string Name => "summonerexhaust";
        internal override string DisplayName => "Exhaust";
        internal override string[] ExtraNames => new[] { "" };
        internal override float Range => 650f;
        internal override int Duration => 100;
        public override void OnTick(EventArgs args)
        {
            if (!Menu.Item("use" + Name).GetValue<bool>() || !IsReady())
                return;

            var hid = Activator.Heroes
                .OrderByDescending(h => h.Player.FlatPhysicalDamageMod)
                .FirstOrDefault(h => h.Player.IsValidTarget(Range + 250));

            foreach (var hero in Activator.Allies())
            {
                var attacker = hero.Attacker as Obj_AI_Hero;
                if (attacker == null || hid == null)
                    continue;

                if (hero.Player.Distance(Player.ServerPosition) > Range)
                    continue;

                if (attacker.Distance(hero.Player.ServerPosition) <= 1250)
                {
                    if (hero.HitTypes.Contains(HitType.ForceExhaust))
                    {
                        UseSpellOn(attacker);
                    }

                    if (!Parent.Item(Parent.Name + "useon" + attacker.NetworkId).GetValue<bool>())
                        continue;

                    if (Menu.Item("use" + Name + "ulti").GetValue<bool>())
                    {
                        if (hero.HitTypes.Contains(HitType.Ultimate))
                        {
                            if (Menu.Item("f" + Name).GetValue<bool>())
                                UseSpellOn(attacker);

                            else if (hero.IncomeDamage / hero.Player.MaxHealth * 100 >= 45)
                                UseSpellOn(attacker, Menu.Item("mode" + Name).GetValue<StringList>().SelectedIndex == 1);

                            else if (hero.Player.Health / hero.Player.MaxHealth * 100 <= 50)
                                UseSpellOn(attacker, Menu.Item("mode" + Name).GetValue<StringList>().SelectedIndex == 1);

                            else if (hero.IncomeDamage >= hero.Player.Health)
                                UseSpellOn(attacker, Menu.Item("mode" + Name).GetValue<StringList>().SelectedIndex == 1);
                        }
                    }

                    if (hero.Player.Health / hero.Player.MaxHealth * 100 <=
                        Menu.Item("a" + Name + "pct").GetValue<Slider>().Value)
                    {
                        if (!hero.Player.IsFacing(attacker))
                        {
                            if (attacker.NetworkId == hid.Player.NetworkId)
                            {
                                UseSpellOn(attacker, Menu.Item("mode" + Name).GetValue<StringList>().SelectedIndex == 1);
                            }
                        }
                    }

                    if (attacker.Health / attacker.MaxHealth * 100 <=
                        Menu.Item("e" + Name + "pct").GetValue<Slider>().Value)
                    {
                        if (!attacker.IsFacing(hero.Player))
                        {
                            UseSpellOn(attacker, Menu.Item("mode" + Name).GetValue<StringList>().SelectedIndex == 1);
                        }
                    }
                }
            }
        }
    }
}
