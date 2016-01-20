#region Copyright © 2015 Kurisu Solutions
// All rights are reserved. Transmission or reproduction in part or whole,
// any form or by any means, mechanical, electronical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
// 
// Document:	Activator/Program.cs
// Date:		22/09/2015
// Author:		Robin Kurisu
#endregion

using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security;
using System.Security.Permissions;

#region Namespaces © 2015
using LeagueSharp;
using LeagueSharp.Common;
using Activator.Base;
using Activator.Data;
using Activator.Handlers;
using Activator.Items;
using Activator.Spells;
using Activator.Summoners;
#endregion

namespace Activator
{
    internal class Activator
    {
        internal static Menu Origin;
        internal static Obj_AI_Hero Player;

        internal static int MapId;
        internal static int LastUsedTimeStamp;
        internal static int LastUsedDuration;

        internal static SpellSlot Smite;
        internal static bool SmiteInGame;
        internal static bool TroysInGame;
        internal static bool UseEnemyMenu, UseAllyMenu;

        public static System.Version Version;
        public static List<Champion> Heroes = new List<Champion>();

        private static void Main(string[] args)
        {
            Version = Assembly.GetExecutingAssembly().GetName().Version;
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            try
            {
                Player = ObjectManager.Player;
                MapId = (int) Utility.Map.GetMap().Type;

                GetSmiteSlot();
                GetGameTroysInGame();
                GetAurasInGame();
                GetHeroesInGame();
                GetComboDamage();
                GetSpellsInGame();

                Origin = new Menu("Activator", "activator", true);

                var cmenu = new Menu("Cleansers", "cmenu");
                SubMenu(cmenu, false);
                GetItemGroup("Items.Cleansers").ForEach(t => NewItem((CoreItem) NewInstance(t), cmenu));
                Origin.AddSubMenu(cmenu);

                var dmenu = new Menu("Defensives", "dmenu");
                SubMenu(dmenu, false);
                GetItemGroup("Items.Defensives").ForEach(t => NewItem((CoreItem) NewInstance(t), dmenu));
                Origin.AddSubMenu(dmenu);

                var smenu = new Menu("Summoners", "smenu");
                GetItemGroup("Summoners").ForEach(t => NewSumm((CoreSum) NewInstance(t), smenu));
                SubMenu(smenu, true, true);
                Origin.AddSubMenu(smenu);

                var omenu = new Menu("Offensives", "omenu");
                SubMenu(omenu, true);
                GetItemGroup("Items.Offensives").ForEach(t => NewItem((CoreItem) NewInstance(t), omenu));
                Origin.AddSubMenu(omenu);

                var imenu = new Menu("Consumables", "imenu");
                GetItemGroup("Items.Consumables").ForEach(t => NewItem((CoreItem) NewInstance(t), imenu));
                Origin.AddSubMenu(imenu);

                var amenu = new Menu("Auto Spells", "amenu");
                SubMenu(amenu, false);
                GetItemGroup("Spells.Evaders").ForEach(t => NewSpell((CoreSpell) NewInstance(t), amenu));
                GetItemGroup("Spells.Shields").ForEach(t => NewSpell((CoreSpell) NewInstance(t), amenu));
                GetItemGroup("Spells.Health").ForEach(t => NewSpell((CoreSpell) NewInstance(t), amenu));
                GetItemGroup("Spells.Slows").ForEach(t => NewSpell((CoreSpell) NewInstance(t), amenu));
                GetItemGroup("Spells.Heals").ForEach(t => NewSpell((CoreSpell) NewInstance(t), amenu));
                Origin.AddSubMenu(amenu);

                var zmenu = new Menu("Misc/Settings", "settings");

                if (SmiteInGame)
                {
                    var ddmenu = new Menu("Drawings", "drawings");
                    ddmenu.AddItem(new MenuItem("drawsmitet", "Draw Smite Text")).SetValue(false);
                    ddmenu.AddItem(new MenuItem("drawfill", "Draw Smite Fill")).SetValue(false);
                    ddmenu.AddItem(new MenuItem("drawsmite", "Draw Smite Range")).SetValue(false);
                    zmenu.AddSubMenu(ddmenu);
                }

                zmenu.AddItem(new MenuItem("acdebug", "Debug")).SetValue(false);
                zmenu.AddItem(new MenuItem("evade", "Evade Integration")).SetValue(true);
                zmenu.AddItem(new MenuItem("healthp", "Ally Priority:")).SetValue(new StringList(new[] { "Low HP", "Most AD/AP", "Most HP" }, 1));
                zmenu.AddItem(new MenuItem("usecombo", "Combo (active)")).SetValue(new KeyBind(32, KeyBindType.Press, true));

                var uumenu = new Menu("Spell Database", "evadem");
                LoadSpellMenu(uumenu);
                zmenu.AddSubMenu(uumenu);

                Origin.AddSubMenu(zmenu);
                Origin.AddToMainMenu();

                // drawings
                Drawings.Init();

                // handlers
                Projections.Init();

                // tracks dangerous or lethal buffs/auras
                Buffs.StartOnUpdate();

                // tracks gameobjects 
                Gametroys.StartOnUpdate();

                // on bought item
                Obj_AI_Base.OnPlaceItemInSlot += Obj_AI_Base_OnPlaceItemInSlot;

                Game.OnChat += Game_OnChat;
                Game.PrintChat("<b>Activator#</b> - Loaded!");
                Updater.UpdateCheck();

                // init valid auto spells
                foreach (var autospell in Lists.Spells)
                    if (Player.GetSpellSlot(autospell.Name) != SpellSlot.Unknown)
                        Game.OnUpdate += autospell.OnTick;

                // init valid summoners
                foreach (var summoner in Lists.Summoners)
                    if (summoner.Slot != SpellSlot.Unknown ||
                        summoner.ExtraNames.Any(x => Player.GetSpellSlot(x) != SpellSlot.Unknown))
                        Game.OnUpdate += summoner.OnTick;

                // find items (if F5)
                foreach (var item in Lists.Items)
                {
                    if (!LeagueSharp.Common.Items.HasItem(item.Id)) 
                        continue;

                    if (!Lists.BoughtItems.Contains(item))
                    {
                        Game.OnUpdate += item.OnTick;
                        Lists.BoughtItems.Add(item);
                        Game.PrintChat("<b>Activator#</b> - <font color=\"#FFF280\">" + item.Name + "</font> active!");
                    }
                }

                Utility.DelayAction.Add(3000, CheckEvade);
            }

            catch (Exception e)
            {
                Console.WriteLine(e);
                Game.PrintChat("Exception thrown at <font color=\"#FFF280\">Activator.OnGameLoad</font>");
            }
        }

        private static void Obj_AI_Base_OnPlaceItemInSlot(Obj_AI_Base sender, Obj_AI_BasePlaceItemInSlotEventArgs args)
        {
            if (!sender.IsMe)
                return;

            foreach (var item in Lists.Items)
            {
                if (item.Id == (int) args.Id)
                {
                    if (!Lists.BoughtItems.Contains(item))
                    {
                        Game.OnUpdate += item.OnTick;
                        Lists.BoughtItems.Add(item);
                        Game.PrintChat("<b>Activator#</b> - <font color=\"#FFF280\">" + item.Name + "</font> active!");
                    }
                }
            }
        }

        private static void NewItem(CoreItem item, Menu parent)
        {
            try
            {
                if (item.Maps.Contains((MapType) MapId) || 
                    item.Maps.Contains(MapType.Common))
                {
                    Lists.Items.Add(item.CreateMenu(parent));
                }
            }

            catch (Exception e)
            {
                Console.WriteLine(e);
                Game.PrintChat("Exception thrown at <font color=\"#FFF280\">Activator.NewItem</font>");
            }
        }

        private static void NewSpell(CoreSpell spell, Menu parent)
        {
            try
            {
                if (Player.GetSpellSlot(spell.Name) != SpellSlot.Unknown)
                    Lists.Spells.Add(spell.CreateMenu(parent));
            }

            catch (Exception e)
            {
                Console.WriteLine(e);
                Game.PrintChat("Exception thrown at <font color=\"#FFF280\">Activator.NewSpell</font>");
            }
        }

        private static void NewSumm(CoreSum summoner, Menu parent)
        {
            try
            {
                if (summoner.Name.Contains("smite") && SmiteInGame)
                    Lists.Summoners.Add(summoner.CreateMenu(parent));

                if (!summoner.Name.Contains("smite") && Player.GetSpellSlot(summoner.Name) != SpellSlot.Unknown)
                    Lists.Summoners.Add(summoner.CreateMenu(parent));
            }

            catch (Exception e)
            {
                Console.WriteLine(e);
                Game.PrintChat("Exception thrown at <font color=\"#FFF280\">Activator.NewSumm</font>");
            }
        }

        private static List<Type> GetItemGroup(string nspace)
        {
            return
                Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(t => t.IsClass && t.Namespace == "Activator." + nspace &&
                                t.Name != "CoreItem" && t.Name != "CoreSpell" && t.Name != "CoreSum" &&
                               !t.Name.Contains("c__") && !t.Name.Contains("<>c")).ToList(); // kek
        }

        private static void GetComboDamage()
        {
            foreach (KeyValuePair<string, List<DamageSpell>> entry in Damage.Spells)
            {
                if (entry.Key == Player.ChampionName)
                    foreach (DamageSpell spell in entry.Value)
                        Spelldata.DamageLib.Add(spell.Damage, spell.Slot);
            }
        }

        private static void GetHeroesInGame()
        {
            foreach (var i in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.Team == Player.Team))
                Heroes.Add(new Champion(i, 0));

            foreach (var i in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.Team != Player.Team))
                Heroes.Add(new Champion(i, 0));
        }

        private static void GetSmiteSlot()
        {
            if (Player.GetSpell(SpellSlot.Summoner1).Name.ToLower().Contains("smite"))
            {
                SmiteInGame = true;
                Smite = SpellSlot.Summoner1;
            }

            if (Player.GetSpell(SpellSlot.Summoner2).Name.ToLower().Contains("smite"))
            {
                SmiteInGame = true;
                Smite = SpellSlot.Summoner2;
            }
        }

        private static void GetGameTroysInGame()
        {
            foreach (var i in ObjectManager.Get<Obj_AI_Hero>().Where(h => h.Team != Player.Team))
            {
                foreach (var item in Gametroydata.Troys.Where(x => x.ChampionName == i.ChampionName))
                {
                    TroysInGame = true;
                    Gametroy.Objects.Add(new Gametroy(i, item.Slot, item.Name, 0, false));
                }
            }
        }

        private static void GetSpellsInGame()
        {
            foreach (var i in ObjectManager.Get<Obj_AI_Hero>().Where(h => h.Team != Player.Team))
                foreach (var item in Spelldata.Spells.Where(x => x.ChampionName == i.ChampionName.ToLower()))
                    Spelldata.SomeSpells.Add(item);
        }

        private static void GetAurasInGame()
        {
            foreach (var i in ObjectManager.Get<Obj_AI_Hero>().Where(h => h.Team != Player.Team))
                foreach (var aura in Buffdata.BuffList.Where(x => x.Champion == i.ChampionName))
                    Buffdata.SomeAuras.Add(aura);
        }

        public static IEnumerable<Champion> Allies()
        {
            switch (Origin.Item("healthp").GetValue<StringList>().SelectedIndex)
            {
                case 0:
                    return Heroes.Where(h => h.Player.IsAlly)
                        .OrderBy(h => h.Player.Health / h.Player.MaxHealth * 100);
                case 1:
                    return Heroes.Where(h => h.Player.IsAlly)
                        .OrderByDescending(h => h.Player.FlatPhysicalDamageMod + h.Player.FlatMagicDamageMod);
                case 2:
                    return Heroes.Where(h => h.Player.IsAlly)
                        .OrderByDescending(h => h.Player.Health);
            }

            return null;
        }

        private static void SubMenu(Menu parent, bool enemy, bool both = false)
        {
            var menu = new Menu("Config", parent.Name + "sub");

            var ireset = new MenuItem(parent.Name + "clear", "Deselect [All]");
            menu.AddItem(ireset).SetValue(false);

            foreach (var hero in both ? HeroManager.AllHeroes : enemy ? HeroManager.Enemies : HeroManager.Allies)
            {
                var side = hero.Team == Player.Team ? "[Ally]" : "[Enemy]";
                var mitem = new MenuItem(parent.Name + "useon" + hero.NetworkId,
                    "Use for " + hero.ChampionName + " " + side);

                menu.AddItem(mitem.DontSave()).SetValue(true);

                if (both)
                {
                    mitem.Show(hero.IsAlly && UseAllyMenu || hero.IsEnemy && UseEnemyMenu);
                }
            }

            ireset.ValueChanged += (sender, args) =>
            {
                if (args.GetNewValue<bool>())
                {
                    foreach (var hero in both 
                        ? HeroManager.AllHeroes : enemy 
                        ? HeroManager.Enemies : HeroManager.Allies)
                        menu.Item(parent.Name + "useon" + hero.NetworkId).SetValue(hero.IsMe);

                    Utility.DelayAction.Add(100, () => ireset.SetValue(false));
                }
            };

            parent.AddSubMenu(menu);
        }

        private static void LoadSpellMenu(Menu parent)
        {
            foreach (var unit in Heroes.Where(h => h.Player.Team != Player.Team))
            {
                var menu = new Menu(unit.Player.ChampionName, unit.Player.NetworkId + "menu");

                // new menu per spell
                foreach (var entry in Spelldata.Spells)
                {
                    if (entry.ChampionName == unit.Player.ChampionName.ToLower())
                    {
                        var newmenu = new Menu(entry.SDataName, entry.SDataName);

                        // activation parameters
                        newmenu.AddItem(new MenuItem(entry.SDataName + "predict", "enabled").DontSave())
                            .SetValue(entry.CastRange != 0f);
                        newmenu.AddItem(new MenuItem(entry.SDataName + "danger", "danger").DontSave())
                            .SetValue(entry.HitType.Contains(HitType.Danger));
                        newmenu.AddItem(new MenuItem(entry.SDataName + "crowdcontrol", "crowdcontrol").DontSave())
                            .SetValue(entry.HitType.Contains(HitType.CrowdControl));
                        newmenu.AddItem(new MenuItem(entry.SDataName + "ultimate", "danger ultimate").DontSave())
                            .SetValue(entry.HitType.Contains(HitType.Ultimate));
                        newmenu.AddItem(new MenuItem(entry.SDataName + "forceexhaust", "force exhuast").DontSave())
                            .SetValue(entry.HitType.Contains(HitType.ForceExhaust));
                        menu.AddSubMenu(newmenu);
                    }
                }

                parent.AddSubMenu(menu);
            }
        }

        private static void CheckEvade()
        {
            if (Menu.GetMenu("ezEvade", "ezEvade") != null)
                Origin.Item("evade").SetValue(true);

            if (Menu.GetMenu("Evade", "Evade") != null)
                Origin.Item("evade").SetValue(true);

            if (Menu.GetMenu("Evade", "Evade") == null && Menu.GetMenu("ezEvade", "ezEvade") == null)
                Origin.Item("evade").SetValue(false);
        }

        private static object NewInstance(Type type)
        {
            try
            {
                var target = type.GetConstructor(Type.EmptyTypes);
                var dynamic = new DynamicMethod(string.Empty, type, new Type[0], target.DeclaringType);
                var il = dynamic.GetILGenerator();

                il.DeclareLocal(target.DeclaringType);
                il.Emit(OpCodes.Newobj, target);
                il.Emit(OpCodes.Stloc_0);
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ret);

                var method = (Func<object>) dynamic.CreateDelegate(typeof(Func<object>));
                return method();
            }

            catch (Exception e)
            {
                Console.WriteLine(e);
                Game.PrintChat("Exception thrown at <font color=\"#FFF280\">Activator.NewInstance</font>");
                return null;
            }
        }

        private static void Game_OnChat(GameChatEventArgs args)
        {
            if ((args.Sender?.IsMe ?? false) && args.Message == ".changelog")
            {
                try
                {
                    new PermissionSet(PermissionState.Unrestricted).Assert();
                    System.Diagnostics.Process.Start("https://github.com/xKurisu/Activator/commits/master");
                    args.Process = false;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    Game.PrintChat("Exception thrown at <font color=\"#FFF280\">Activator.Changelog</font>");
                }

                finally
                {
                    CodeAccessPermission.RevertAssert();
                }
            }
        }
    }
}