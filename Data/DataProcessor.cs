using DataInput.Models;
using DataInput.Models.Interfaces;
using NLua;
using System.Diagnostics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DataInput;

public class DataProcessor
{
    public List<Distribution> Distributions { get; private set; } = new List<Distribution>();
    public List<Models.Error> Errors { get; set; } = new List<Models.Error>();
    private readonly List<string> CacheNames = new List<string>()
        {
            "FoodCache1",
            "GunCache1",
            "GunCache2",
            "MedicalCache1",
            "SafehouseLoot",
            "ShotgunCache1",
            "ShotgunCache2",
            "SurvivorCache1",
            "SurvivorCache2",
            "ToolsCache1"

        };
    private readonly List<string> ProfessionNames = new List<string>()
        {
            "BandPractice",
            "Carpenter",
            "Chef",
            "Electrician",
            "Farmer",
            "Nurse"
        };
    private readonly List<string> BagNames = new List<string>()
        {
            "Bag_ALICEpack",
            "Bag_ALICEpack_Army",
            "Bag_BigHikingBag",
            "Bag_BowlingBallBag",
            "Bag_DoctorBag",
            "Bag_DuffelBag",
            "Bag_DuffelBagTINT",
            "Bag_FoodSnacks",
            "Bag_FoodCanned",
            "Bag_GolfBag",
            "Bag_InmateEscapedBag",
            "Bag_JanitorToolbox",
            "Bag_MedicalBag",
            "Bag_Military",
            "Bag_MoneyBag",
            "Bag_NormalHikingBag",
            "Bag_Schoolbag",
            "Bag_ShotgunBag",
            "Bag_ShotgunDblBag",
            "Bag_ShotgunDblSawnoffBag",
            "Bag_ShotgunSawnoffBag",
            "Bag_SurvivorBag",
            "Bag_ToolBag",
            "Bag_WeaponBag",
            "Bag_WorkerBag",
            "Briefcase",
            "FirstAidKit",
            "Flightcase",
            "Garbagebag",
            "GroceryBag1",
            "GroceryBag2",
            "GroceryBag3",
            "GroceryBag4",
            "GroceryBag5",
            "Guitarcase",
            "Handbag",
            "Lunchbag",
            "Lunchbox",
            "Lunchbox2",
            "Paperbag",
            "Paperbag_Jays",
            "Paperbag_Spiffos",
            "PistolCase1",
            "PistolCase2",
            "PistolCase3",
            "Plasticbag",
            "Purse",
            "RevolverCase1",
            "RevolverCase2",
            "RevolverCase3",
            "RifleCase1",
            "RifleCase2",
            "RifleCase3",
            "Bag_Satchel",
            "SeedBag",
            "SewingKit",
            "ShotgunCase1",
            "ShotgunCase2",
            "Suitcase",
            "Toolbox",
            "Tote"


        };
    private List<Distribution> ProceduralDistributions = new();
    public void ParseData(string folderPath = @"C:\Program Files (x86)\Steam\steamapps\common\ProjectZomboid")
    {
        ParseProceduralDistributions(folderPath);
        ParseDistributions(folderPath);
        Distributions.AddRange(ProceduralDistributions);
    }
   
   
    private List<Distribution> ConvertLuaTableValues(LuaTable table)
    {
        List<Distribution> result = new List<Distribution>();
        foreach (KeyValuePair<object, object> distributionKvp in table)//Room, bag, profession, cache
        {
            Distribution distribution = new Distribution() { Name = distributionKvp.Key.ToString() };
            if (distributionKvp.Value.GetType() == typeof(LuaTable))
            {
                foreach (KeyValuePair<object, object> distributionValueKvp in (LuaTable)distributionKvp.Value)//Properties. Booleans, containers, lists...
                {
                    switch (distributionValueKvp.Key.ToString())
                    {
                        case "isShop":
                            distribution.IsShop = true;
                            break;
                        case "DontSpawnAmmo":
                            
                                distribution.DontSpawnAmmo = true;
                                break;
                            
                        case "MaxMap":
                            
                                distribution.MaxMap = int.Parse(distributionValueKvp.Value.ToString());
                                break;
                            
                        case "StashChance":
                            
                                distribution.StashChance = int.Parse(distributionValueKvp.Value.ToString());
                                break;
                            
                        case "FillRand":
                            
                                if ((distributionValueKvp.Value.ToString()) == "0")
                                {
                                    distribution.FillRand = false;
                                }
                                else
                                {
                                    distribution.FillRand = true;
                                }
                                break;
                            
                        case "rolls":
                            
                                distribution.ItemRolls = int.Parse(distributionValueKvp.Value.ToString());
                                break;
                            
                        case "items":
                                ReadItemChances(distribution, distributionValueKvp.Value);
                                break;
                            
                        case "junk":
                            
                                ReadJunkChances(distribution, distributionValueKvp.Value);
                                break;
                            
                        default:
                            {
                                if (distributionValueKvp.Value.GetType() == typeof(LuaTable))
                                {
                                    Container container = new Container() { Name = distributionValueKvp.Key.ToString() };
                                    foreach (KeyValuePair<object, object> containerKvp in (LuaTable)distributionValueKvp.Value)//Containers
                                    {
                                        switch (containerKvp.Key.ToString())
                                        {
                                            case "fillRand":
                                                {
                                                    container.FillRand = containerKvp.Value.ToString() == "1" && false;
                                                    break;
                                                }
                                            case "rolls":
                                                {
                                                    container.ItemRolls = int.Parse(containerKvp.Value.ToString());
                                                    break;
                                                }
                                            case "items":
                                                {
                                                    ReadItemChances(container, containerKvp.Value);
                                                    break;
                                                }
                                            case "junk":
                                                {
                                                    ReadJunkChances(container, containerKvp.Value);
                                                    break;
                                                }
                                            case "procedural":
                                                {
                                                    if (containerKvp.Value.ToString() == "True")
                                                    {
                                                        container.Procedural = true;
                                                    }
                                                    break;
                                                }
                                            case "dontSpawnAmmo":
                                                {
                                                    if (containerKvp.Value.ToString() == "True")
                                                    {
                                                        container.DontSpawnAmmo = true;
                                                    }
                                                    break;
                                                }
                                            default:
                                                {
                                                    ReadProcLists(container, containerKvp.Value);
                                                    break;
                                                }
                                        }
                                    }
                                    distribution.Containers.Add(container);
                                }
                                break;
                            }
                    }
                }
            }

            try
            {
                if (CacheNames.Contains(distribution.Name)) distribution.DistributionType = "Cache";
                else if (BagNames.Contains(distribution.Name)) distribution.DistributionType = "Bag";
                else if (ProfessionNames.Contains(distribution.Name)) distribution.DistributionType = "Profession";
                else distribution.DistributionType = "Room";
                result.Add(distribution);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

        }
        return result;
    }
    private void ParseProceduralDistributions(string folderPath)
    {
        List<Distribution> result = new();
        Lua lua = new Lua() { MaximumRecursion = 20 };
        lua.DoFile(folderPath + "\\media\\lua\\server\\Items\\ProceduralDistributions.lua");
        var tableValues = lua.GetTable("ProceduralDistributions.list");
        if (tableValues.GetType() == typeof(LuaTable))
        {
            result = ConvertLuaTableValues(tableValues);
        }
        foreach (Distribution distribution in result)
        {
            distribution.DistributionType = "Procedural";
        }
        ProceduralDistributions = result;




    }
    private void ParseDistributions(string folderPath)
    {
        List<Distribution> result = new();
        Lua lua = new Lua() { MaximumRecursion = 20 };
        lua.DoFile(folderPath + "\\media\\lua\\server\\Items\\Distributions.lua");
        var tableValues = lua.GetTable("Distributions").Values;
        var luaTable = default(LuaTable);
        foreach (var item in tableValues)//One item but can't use [0] !?
        {
            if (item is LuaTable)
            {
                luaTable = (LuaTable)item;
                result.AddRange(ConvertLuaTableValues(luaTable));
                break;
            }
        }
        Distributions = result;
    }
    private bool IsLuaTable(object Object)
    {
        return Object.GetType() == typeof(LuaTable);
    }
    private void ReadItemChances(IItemParent parent, object children)
    {
        if (!IsLuaTable(children)) return;
        Item item = new();
        foreach (KeyValuePair<object, object> itemKvp in (LuaTable)children)
        {
#pragma warning disable CS8602, CS8604
            if (char.IsNumber(itemKvp.Value.ToString()[0]) && char.IsNumber(itemKvp.Value.ToString().Last()))
            {
                item.Chance = double.Parse(itemKvp.Value.ToString());
                parent.ItemChances.Add(item);
            }
            else
            {
                item.Name = itemKvp.Value.ToString();
            }
#pragma warning restore CS8602, CS8604
        }
    }
    private void ReadJunkChances(IItemParent parent, object children)
    {
        if (!IsLuaTable(children)) return;
        foreach (KeyValuePair<object, object> junkKvp in (LuaTable)children)
        {
            if (junkKvp.Key.ToString() == "rolls")
            {
#pragma warning disable CS8604
                parent.JunkRolls = int.Parse(junkKvp.Value.ToString());
#pragma warning restore CS8604
            }
            else
            {
                ReadItemChances(parent, junkKvp.Value);
            }
        }
    }
    private void ReadProcLists(Container container, object children)
    {
        if (!IsLuaTable(children)) return;
        ProcListEntry procListEntry = new();
        foreach (KeyValuePair<object, object> kvp in (LuaTable)children)
        {
            if (!IsLuaTable(kvp.Value))
            {
                switch (kvp.Key.ToString())
                {
                    case "name":
                        {
                            procListEntry.Name = kvp.Value.ToString();
                            Distribution procedural = ProceduralDistributions.Find(d => d.Name == procListEntry.Name);
                            if (procedural == null)
                            {
                                procedural =
                                    ProceduralDistributions.Find(d => d.Name.ToLower() == procListEntry.Name.ToLower());
                                if (procedural == null)
                                {
                                    Errors.Add(new Models.Error()
                                    {
                                        Code = 1,
                                        Description = "No procedural distribution with name \"" + procListEntry.Name +
                                                      "\" found.",
                                        FileName = "ProceduralDistributions.lua"
                                    });
                                }
                            }
                            else
                            {
                                procListEntry.ProceduralDistribution = procedural;
                            }

                            break;
                        }
                    case "min":
                        {
                            procListEntry.Min = int.Parse(kvp.Value.ToString());
                            break;
                        }
                    case "max":
                        {
                            procListEntry.Max = int.Parse(kvp.Value.ToString());
                            break;
                        }
                    case "weightChance":
                        {
                            procListEntry.WeightChance = int.Parse(kvp.Value.ToString());
                            break;
                        }
                    case "forceForTiles":
                        {
                            procListEntry.ForceForTiles = kvp.Value.ToString();
                            break;
                        }
                    case "forceForRooms":
                        {
                            procListEntry.ForceForRooms = kvp.Value.ToString();
                            break;
                        }
                    case "forceForZones":
                        {
                            procListEntry.ForceForZones = kvp.Value.ToString();
                            break;
                        }
                    case "forceForItems":
                        {
                            procListEntry.ForceForItems = kvp.Value.ToString();
                            break;
                        }
                    default:
                        {
                            Debug.Fail("Something changed and the programs needs updating");
                            break;
                        }
                }
            }
            else
            {
                foreach (KeyValuePair<object, object> procListEntryKvp in (LuaTable)kvp.Value)
                {
                    switch (procListEntryKvp.Key.ToString())
                    {
                        case "name":
                            {
                                procListEntry.Name = procListEntryKvp.Value.ToString();
                                Distribution procedural = ProceduralDistributions.Find(d => d.Name == procListEntry.Name);
                                if (procedural == null)
                                {
                                    procedural =
                                        ProceduralDistributions.Find(d => d.Name.ToLower() == procListEntry.Name.ToLower());
                                    if (procedural == null)
                                    {
                                        Errors.Add(new Models.Error()
                                        {
                                            Code = 1,
                                            Description = "No procedural distribution with name \"" + procListEntry.Name +
                                                          "\" found.",
                                            FileName = "ProceduralDistributions.lua"
                                        });
                                    }
                                }
                                else
                                {
                                    procListEntry.ProceduralDistribution = procedural;
                                }

                                break;
                            }
                        case "min":
                            {
                                procListEntry.Min = int.Parse(procListEntryKvp.Value.ToString());
                                break;
                            }
                        case "max":
                            {
                                procListEntry.Max = int.Parse(procListEntryKvp.Value.ToString());
                                break;
                            }
                        case "weightChance":
                            {
                                procListEntry.WeightChance = int.Parse(procListEntryKvp.Value.ToString());
                                break;
                            }
                        case "forceForTiles":
                            {
                                procListEntry.ForceForTiles = procListEntryKvp.Value.ToString();
                                break;
                            }
                        case "forceForRooms":
                            {
                                procListEntry.ForceForRooms = procListEntryKvp.Value.ToString();
                                break;
                            }
                        case "forceForZones":
                            {
                                procListEntry.ForceForZones = procListEntryKvp.Value.ToString();
                                break;
                            }
                        case "forceForItems":
                            {
                                procListEntry.ForceForItems = kvp.Value.ToString();
                                break;
                            }
                        default:
                            {
                                Debug.Fail("Something changed and the programs needs updating");
                                break;
                            }
                    }

                }
                container.ProcListEntries.Add(procListEntry);
                procListEntry = new();
            }

            if (procListEntry.Name != null)
            {
                container.ProcListEntries.Add(procListEntry);
            }

        }
    }
}