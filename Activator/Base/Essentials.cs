#region Copyright © 2015 Kurisu Solutions
// All rights are reserved. Transmission or reproduction in part or whole,
// any form or by any means, mechanical, electronical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
// 
// Document:	Base/Essentials.cs
// Date:		22/09/2015
// Author:		Robin Kurisu
#endregion

using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;

namespace Activator.Base
{
    class Essentials
    {
        /// <summary>
        /// Returns if the minion is an "Epic" minion (baron, dragon, etc)
        /// </summary>
        /// <param name="minion">The minion. </param>
        /// <returns></returns>
        public static bool IsEpicMinion(Obj_AI_Base minion)
        {
            var name = minion.Name;
            return minion is Obj_AI_Minion &&
                  (name.StartsWith("SRU_Baron") || name.StartsWith("SRU_Dragon") ||
                   name.StartsWith("SRU_RiftHerald") || name.StartsWith("TT_Spiderboss"));
        }

        /// <summary>
        /// Returns if the minion is a "Large" minion (Red Buff, Blue Buff, etc)
        /// </summary>
        /// <param name="minion">The minion. </param>
        /// <param name="notMini">Check if is mini. </param>
        /// <returns></returns>
        public static bool IsLargeMinion(Obj_AI_Base minion, bool notMini = true)
        {
            var name = minion.Name;
            return minion is Obj_AI_Minion && (notMini && !minion.Name.Contains("Mini")) &&
                   (name.StartsWith("SRU_Blue") || name.StartsWith("SRU_Red") || name.StartsWith("TT_NWraith1.1") ||
                    name.StartsWith("TT_NWraith4.1") || name.StartsWith("TT_NGolem2.1") || name.StartsWith("TT_NGolem5.1") ||
                    name.StartsWith("TT_NWolf3.1") || name.StartsWith("TT_NWolf6.1"));
        }

        /// <summary>
        /// Returns if the minion is a "Small" minion (Razorbeak, Krug, etc)
        /// </summary>
        /// <param name="minion">The minion. </param>
        /// <param name="notMini">Check if is mini. </param>
        /// <returns></returns>
        public static bool IsSmallMinion(Obj_AI_Base minion, bool notMini = true)
        {
            var name = minion.Name;
            return minion is Obj_AI_Minion && (notMini && !minion.Name.Contains("Mini")) &&
                  (name.StartsWith("SRU_Murkwolf") || name.StartsWith("SRU_Razorbeak") ||
                   name.StartsWith("SRU_Gromp") || name.StartsWith("SRU_Krug"));  
        }


        /// <summary>
        /// Will try to Reset income damage if target is not valid.
        /// </summary>
        /// <param name="hero">The hero to reset damage. </param>
        public static void ResetIncomeDamage(Obj_AI_Hero hero)
        {
            foreach (var unit in Activator.Heroes.Where(x => x.Player.NetworkId == hero.NetworkId))
            {
                if (!unit.Player.IsValidTarget(float.MaxValue, false) || unit.Player.IsZombie || unit.Immunity)
                {
                    unit.Attacker = null;
                    unit.IncomeDamage = 0;
                    unit.HitTypes.Clear();
                }       
            }
        }
    }
}
