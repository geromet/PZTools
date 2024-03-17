using System;
using System.Collections.Generic;
using NLua;
using System.IO;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Diagnostics;
using Data.Models.Items.Distributions;

namespace Data
{
    public class DataBase
    {
        public static void CreateDB(string folderPath = @"C:\Program Files (x86)\Steam\steamapps\common\ProjectZomboid")
        {
            DistributionContext context = new DistributionContext();
            context.Database.EnsureCreated();
            Lua lua = new Lua() { MaximumRecursion = 20 };
            lua.DoFile(folderPath + "\\media\\lua\\server\\Items\\Distributions.lua");
            var tableValues = lua.GetTable("Distributions").Values;
            foreach (LuaTable table in tableValues)
            {
                foreach (KeyValuePair<object, object> kvp in table)//Room, bag, profession, cache
                {
                    Distribution distribution = new Distribution() { Name = kvp.Key.ToString() };
                    if (kvp.Value.GetType() == typeof(LuaTable))
                    {
                        foreach (KeyValuePair<object, object> kvp2 in (LuaTable)kvp.Value)//Properties. Booleans, containers, lists...
                        {
                            switch (kvp2.Key.ToString())
                            {
                                case "isShop":
                                        distribution.IsShop = true;
                                    break;
                                case "DontSpawnAmmo":
                                        distribution.DontSpawnAmmo = true;
                                    break;
                                case "MaxMap":
                                    distribution.MaxMap = int.Parse(kvp2.Value.ToString());
                                    break;
                                case "StashChance":
                                    distribution.StashChance = int.Parse(kvp2.Value.ToString());
                                    break;
                                case "FillRand":
                                    distribution.FillRand = int.Parse(kvp2.Value.ToString());
                                    break;
                                case "rolls":
                                    distribution.ItemRolls = int.Parse(kvp2.Value.ToString());
                                    break;
                                case "items":
                                    if (kvp2.Value.GetType() == typeof(LuaTable))
                                    {
                                        Item item = new();
                                        foreach (KeyValuePair<object, object> kvp3 in (LuaTable)kvp2.Value)//Items
                                        {
                                            if (char.IsNumber(kvp3.Value.ToString()[0]) && char.IsNumber(kvp3.Value.ToString().Last()))
                                            {
                                                item.Chance = double.Parse(kvp3.Value.ToString());
                                                distribution.ItemChances.Add(item);
                                            }
                                            else
                                            {
                                                item = new() { Name = kvp3.Value.ToString() };
                                            }
                                        }
                                    }
                                    break;
                                case "junk":
                                    if (kvp2.Value.GetType() == typeof(LuaTable))
                                    {
                                        foreach (KeyValuePair<object, object> kvp3 in (LuaTable)kvp2.Value)//Junk
                                        {
                                            if (kvp3.Value.ToString() == "rolls")
                                            {
                                                distribution.JunkRolls = (int)kvp3.Value;
                                            }
                                            else
                                            {
                                                Item junkItem = new();
                                                if (char.IsNumber(kvp3.Value.ToString()[0]))
                                                {
                                                    junkItem.Chance = (long)kvp3.Value;
                                                    distribution.ItemChances.Add(junkItem);
                                                }
                                                else
                                                {
                                                    junkItem = new() { Name = kvp3.Key.ToString() };
                                                }
                                            }

                                        }
                                    }
                                    break;
                                default:
                                    if (kvp2.Value.GetType() == typeof(LuaTable))
                                    {
                                        Container container = new Container() { Name = kvp2.Key.ToString() };
                                        foreach (KeyValuePair<object, object> kvp3 in (LuaTable)kvp2.Value)//Containers
                                        {
                                            switch (kvp3.Key.ToString())
                                            {
                                                case "fillRand":
                                                    if (kvp3.Value.ToString() == "1")
                                                    {
                                                        container.FillRand = false;
                                                    }
                                                    else
                                                    {
                                                        container.FillRand = false;
                                                    }

                                                    break;
                                                case "rolls":
                                                    container.ItemRolls = int.Parse(kvp3.Value.ToString());
                                                    break;
                                                case "items":
                                                    if (kvp3.Value.GetType() == typeof(LuaTable))
                                                    {
                                                        Item item = new();
                                                        foreach (KeyValuePair<object, object> kvp4 in (LuaTable)kvp3.Value)//Items
                                                        {
                                                            if (char.IsNumber(kvp4.Value.ToString()[0]) && char.IsNumber(kvp4.Value.ToString().Last()))
                                                            {
                                                                item.Chance = double.Parse(kvp4.Value.ToString());
                                                                container.ItemChances.Add(item);
                                                            }
                                                            else
                                                            {
                                                                item = new() { Name = kvp4.Value.ToString() };
                                                            }
                                                        }
                                                    }
                                                    break;
                                                case "junk":
                                                    if (kvp3.Value.GetType() == typeof(LuaTable))
                                                    {
                                                        Item item = new();
                                                        foreach (KeyValuePair<object, object> kvp4 in (LuaTable)kvp3.Value)//Junk
                                                        {
                                                            if (char.IsNumber(kvp4.Value.ToString()[0]) && char.IsNumber(kvp4.Value.ToString().Last()))
                                                            {
                                                                item.Chance = double.Parse(kvp4.Value.ToString());
                                                                container.ItemChances.Add(item);
                                                            }
                                                            else
                                                            {
                                                                item = new() { Name = kvp4.Value.ToString() };
                                                            }
                                                        }
                                                    }
                                                    break;
                                                case "procedural":
                                                    if (kvp3.Value.ToString() == "true")
                                                    {
                                                        container.Procedural = true;
                                                    }
                                                    else
                                                    {
                                                        container.Procedural = false;
                                                    }
                                                    break;
                                                case "dontSpawnAmmo":
                                                    if (kvp3.Value.ToString() == "true")
                                                    {
                                                        container.DontSpawnAmmo = true;
                                                    }
                                                    else
                                                    {
                                                        container.DontSpawnAmmo = false;
                                                    }
                                                    break;
                                                case "procList":
                                                    if (kvp3.Value.GetType() == typeof(LuaTable))
                                                    {
                                                        foreach (KeyValuePair<object, object> kvp4 in (LuaTable)kvp3.Value)//procListEntries
                                                        {
                                                            ProcListEntry procListEntry = new();
                                                            if (kvp4.Value.GetType() == typeof(LuaTable))
                                                            {
                                                                foreach (KeyValuePair<object, object> kvp5 in (LuaTable)kvp4.Value)//procListEntrieData
                                                                {
                                                                    switch (kvp5.Key.ToString())
                                                                    {
                                                                        case "name":
                                                                            procListEntry.Name = kvp5.Value.ToString();
                                                                            break;
                                                                        case "min":
                                                                            procListEntry.Min = int.Parse(kvp5.Value.ToString());
                                                                            break;
                                                                        case "max":
                                                                            procListEntry.Max = int.Parse(kvp5.Value.ToString());
                                                                            break;
                                                                        case "weightChance":
                                                                            procListEntry.WeightChance = int.Parse(kvp5.Value.ToString());
                                                                            break;
                                                                        case "forceForTiles":
                                                                            procListEntry.ForceForTiles = kvp5.Value.ToString();
                                                                            break;
                                                                        case "forceForRooms":
                                                                            procListEntry.ForceForRooms = kvp5.Value.ToString();
                                                                            break;
                                                                        case "forceForZones":
                                                                            procListEntry.ForceForZones = kvp5.Value.ToString();
                                                                            break;
                                                                        case "forceForItems":
                                                                            procListEntry.ForceForItems = kvp5.Value.ToString();
                                                                            break;
                                                                        default:
                                                                            break;
                                                                    }

                                                                }
                                                                container.ProcListEntries.Add(procListEntry);
                                                            }
                                                        }
                                                    }
                                                    break;
                                                default:
                                                    if (kvp3.Value.GetType() == typeof(LuaTable))
                                                    {
                                                        foreach (KeyValuePair<object, object> kvp4 in (LuaTable)kvp3.Value)//procListEntries
                                                        {
                                                            ProcListEntry procListEntry = new();
                                                            if (kvp4.Value.GetType() == typeof(LuaTable))
                                                            {
                                                                foreach (KeyValuePair<object, object> kvp5 in (LuaTable)kvp4.Value)//procListEntrieData
                                                                {
                                                                    switch (kvp5.Key.ToString())
                                                                    {
                                                                        case "name":
                                                                            procListEntry.Name = kvp5.Value.ToString();
                                                                            //Todo: Parse the ProceduralDistributions File (earlier) and put distribution in procListEntry (search DB)
                                                                            //Distributions where type = procedural && procListEntryId = procListEntry.Id
                                                                            break;
                                                                        case "min":
                                                                            procListEntry.Min = int.Parse(kvp5.Value.ToString());
                                                                            break;
                                                                        case "max":
                                                                            procListEntry.Max = int.Parse(kvp5.Value.ToString());
                                                                            break;
                                                                        case "weightChance":
                                                                            procListEntry.WeightChance = int.Parse(kvp5.Value.ToString());
                                                                            break;
                                                                        case "forceForTiles":
                                                                            procListEntry.ForceForTiles = kvp5.Value.ToString();
                                                                            break;
                                                                        case "forceForRooms":
                                                                            procListEntry.ForceForRooms = kvp5.Value.ToString();
                                                                            break;
                                                                        case "forceForZones":
                                                                            procListEntry.ForceForZones = kvp5.Value.ToString();
                                                                            break;
                                                                        case "forceForItems":
                                                                            procListEntry.ForceForItems = kvp5.Value.ToString();
                                                                            break;
                                                                        default:
                                                                            Debug.Fail("Something changed and the programs needs updating");
                                                                            break;
                                                                    }

                                                                }
                                                                container.ProcListEntries.Add(procListEntry);
                                                            }
                                                        }
                                                    }
                                                    break;
                                            }
                                        }
                                        distribution.Containers.Add(container);
                                    }
                                    break;

                            }
                        }
                    }
                    context.Add(distribution);
                    context.SaveChanges();
                }
            }
        }
    }

}
