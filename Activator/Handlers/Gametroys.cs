#region Copyright © 2015 Kurisu Solutions
// All rights are reserved. Transmission or reproduction in part or whole,
// any form or by any means, mechanical, electronical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
// 
// Document:	Handlers/Gametroys.cs
// Date:		22/09/2015
// Author:		Robin Kurisu
#endregion

using System;
using System.Linq;
using Activator.Base;
using Activator.Data;
using LeagueSharp;
using LeagueSharp.Common;

namespace Activator.Handlers
{
    public class Gametroys
    {
        public static void StartOnUpdate()
        {
            Game.OnUpdate += Game_OnUpdate;
            GameObject.OnCreate += GameObject_OnCreate;
            GameObject.OnDelete += GameObject_OnDelete;
        }

        static void GameObject_OnDelete(GameObject obj, EventArgs args)
        {
            foreach (var troy in Gametroy.Objects)
            {
                if (obj.Name.Contains(troy.Name))
                {
                    troy.Obj = null;
                    troy.Start = 0;
                    troy.Limiter = 0; // reset limiter

                    if (troy.Included)
                        troy.Included = false;
                }
            }
        }

        static void GameObject_OnCreate(GameObject obj, EventArgs args)
        {
            foreach (var troy in Gametroy.Objects)
            {
                if (obj.Name.Contains(troy.Name) && obj.IsValid<GameObject>())
                {
                    troy.Obj = obj;
                    troy.Start = Utils.GameTimeTickCount;

                    if (!troy.Included)
                         troy.Included = true;
                }
            }
        }

        static void Game_OnUpdate(EventArgs args)
        {
            foreach (var hero in Activator.Allies())
            {
                var troy = Gametroy.Objects.FirstOrDefault(x => !x.Owner.IsAlly && x.Included);
                if (troy == null || !troy.Obj.IsValid)
                {
                    // reset damage if obj deleted
                    if (hero.TroyTicks > 0)
                    {
                        hero.IncomeDamage -= 5;
                        hero.TroyTicks -= 1;

                        if (hero.TroyTicks == 0)
                            hero.HitTypes.Clear();
                    }

                    return;
                }

                foreach (var item in Gametroydata.Troys.Where(x => x.Name == troy.Name))
                {
                    if (hero.Player.Distance(troy.Obj.Position) <= item.Radius + hero.Player.BoundingRadius)
                    {                  
                        // check delay (e.g fizz bait)
                        if (Utils.GameTimeTickCount - troy.Start >= item.DelayFromStart)
                        {
                            foreach (var ii in item.HitType)
                            {
                                if (!hero.HitTypes.Contains(ii))
                                     hero.HitTypes.Add(ii);
                            }

                            // limit the damage using an interval
                            if (Utils.GameTimeTickCount - troy.Limiter >= item.Interval * 1000)
                            {
                                hero.Attacker = troy.Owner;
                                hero.IncomeDamage += 5; // todo: get actuall spell damage
                                hero.TroyTicks += 1;
                                troy.Limiter = Utils.GameTimeTickCount;
                            }

                            return;
                        }
                    }
                }

                // reset damage if walked out of obj
                if (hero.TroyTicks > 0)
                {
                    hero.IncomeDamage -= 5;
                    hero.TroyTicks -= 1;

                    if (hero.TroyTicks == 0)
                        hero.HitTypes.Clear();
                }
            }
        }
    }
}