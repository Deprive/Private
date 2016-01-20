#region Copyright © 2015 Kurisu Solutions
// All rights are reserved. Transmission or reproduction in part or whole,
// any form or by any means, mechanical, electronical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
// 
// Document:	Handlers/Events.cs
// Date:		22/09/2015
// Author:		Robin Kurisu
#endregion

using System;
using System.Linq;
using Activator.Base;
using LeagueSharp;
using LeagueSharp.Common;

namespace Activator.Handlers
{
    public class Stealth
    {
        private static bool _loaded;

        public static void Init()
        {
            if (!_loaded)
            {
                GameObject.OnCreate += GameObject_OnCreate;
                Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnStealth;
                _loaded = true;
            }
        }

        static void GameObject_OnCreate(GameObject sender, EventArgs args)
        {
            #region Rengar/Leblanc (Stealth)

            foreach (var hero in Activator.Allies())
            {
                if (sender.IsEnemy && sender.Name.Contains("Rengar_Base_R_Alert"))
                {
                    if (hero.Player.Distance(sender.Position) <= 1200)
                    {
                        hero.HitTypes.Add(HitType.Stealth);
                        Utility.DelayAction.Add(200, () => hero.HitTypes.Remove(HitType.Stealth));
                    }
                }

                if (sender.IsEnemy && sender.Name == "LeBlanc_Base_P_poof.troy")
                {
                    if (hero.Player.Distance(sender.Position) <= 1200)
                    {
                        hero.HitTypes.Add(HitType.Stealth);
                        Utility.DelayAction.Add(200, () => hero.HitTypes.Remove(HitType.Stealth));
                    }
                }
            }

            #endregion
        }

        static void Obj_AI_Base_OnStealth(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            #region Stealth

            var attacker = sender as Obj_AI_Hero;
            if (attacker == null || attacker.IsAlly || !attacker.IsValid<Obj_AI_Hero>())
                return;

            foreach (var hero in Activator.Heroes.Where(h => h.Player.Distance(attacker) <= 1200))
            {
                if (Data.Spelldata.Spells.Any(x => args.SData.Name.ToLower() == x.SDataName && x.HitType.Contains(HitType.Stealth)))
                {
                    hero.HitTypes.Add(HitType.Stealth);
                    Utility.DelayAction.Add(200, () => hero.HitTypes.Remove(HitType.Stealth));
                }
            }

            #endregion
        }
    }
}
