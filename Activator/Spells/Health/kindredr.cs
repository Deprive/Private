using System;
using Activator.Base;
using LeagueSharp;
using LeagueSharp.Common;

namespace Activator.Spells.Health
{
    class kindredr : CoreSpell
    {
        internal override string Name
        {
            get { return "kindredr"; }
        }

        internal override string DisplayName
        {
            get { return "Lamb's Respite | R"; }
        }

        internal override float Range
        {
            get { return 400f; }
        }

        internal override MenuType[] Category
        {
            get { return new[] { MenuType.SelfLowHP }; }
        }

        internal override int DefaultHP
        {
            get { return 20; }
        }

        internal override int DefaultMP
        {
            get { return 0; }
        }

        public override void OnTick(EventArgs args)
        {
            if (!Menu.Item("use" + Name).GetValue<bool>() || !IsReady())
                return;

            foreach (var hero in Activator.Allies())
            {
                if (Parent.Item(Parent.Name + "useon" + hero.Player.NetworkId).GetValue<bool>())
                {
                    if (hero.Player.Distance(Player.ServerPosition) <= 500)
                    {
                        if (!hero.Player.HasBuffOfType(BuffType.Invulnerability))
                        {
                            if (hero.Player.Health / hero.Player.MaxHealth * 100 <=
                                Menu.Item("selflowhp" + Name + "pct").GetValue<Slider>().Value)
                            {
                                if (hero.IncomeDamage > 0)
                                    UseSpellOn(Player);
                            }
                        }
                    }

                    if (hero.Player.Distance(Player.ServerPosition) <= Range)
                    {
                        if (hero.Player.Health / hero.Player.MaxHealth * 100 <=
                            Menu.Item("selflowhp" + Name + "pct").GetValue<Slider>().Value)
                        {
                            if (hero.IncomeDamage > 0 && !hero.Player.IsMe)
                                UseSpellOn(hero.Player);
                        }
                    }
                }
            }
        }
    }
}
