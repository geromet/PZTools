using NLua;
using System.Collections.Generic;

namespace PZViewer
{
    internal class LuaParser
    {
        public static Dictionary<object, object>[] GetDistributions(string filePath = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\ProjectZomboid")
        {
            Lua lua = new Lua() { MaximumRecursion = 20 };
            Dictionary<object, object>[] distributions = new Dictionary<object, object>[2];
            lua.DoFile(filePath + "\\media\\lua\\server\\Items\\Distributions.lua");
            distributions[0] = ConvertLuaTableToDictionary(lua.GetTable("Distributions")[1]);
            lua.DoFile(filePath + "\\media\\lua\\server\\Items\\ProceduralDistributions.lua");
            distributions[1] = ConvertLuaTableToDictionary(lua.GetTable("ProceduralDistributions.list"));
            return distributions;
        }
        private static Dictionary<object, object> ConvertLuaTableToDictionary(object luaTable)
        {
            var result = new Dictionary<object, object>();

            if (luaTable is LuaTable)
            {
                var table = (LuaTable)luaTable;

                foreach (var key in table.Keys)
                {
                    var value = table[key];

                    if (value is LuaTable)
                    {
                        result[key] = ConvertLuaTableToDictionary(value);
                    }
                    else
                    {
                        result[key] = value;
                    }
                }
            }
            return result;
        }
    }
}
