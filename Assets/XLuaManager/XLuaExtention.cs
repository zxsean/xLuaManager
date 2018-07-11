using System;

namespace XLua
{
    [LuaCallCSharp]
    [ReflectionUse]
    public static class XLuaExtention
    {
        public static int ToNumber(this Enum _enum)
        {
            return Convert.ToInt32(_enum);
        }

        public static bool IsNull(this UnityEngine.Object _o)
        {
            return _o == null;
        }
    }
}