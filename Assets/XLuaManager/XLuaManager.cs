using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using XLua;

/// <summary>
/// XLua管理器
/// </summary>
[ExecuteInEditMode]
public class XLuaManager : MonoSingleton<XLuaManager>
{
    private const string __LUA_PATH = "/Editor/luascripts/";

    private const string __LUA_INIT = "LuaInit";

    private const string __ABNAME = "gametexts/lua";

    private const string __ASSETBUNDLE__PATH = "Assets/BuildRes/GameTexts/Lua/";

    /// <summary>
    /// 资源加载代理
    /// 根据自己实际情况变更,这里只是一个例子
    /// </summary>
    /// <param name="_assetBundleName"></param>
    /// <param name="_assetName"></param>
    /// <param name="_callback"></param>
    private delegate void LoadBundleAsync(string _assetBundleName, string _assetName, Action<AssetBundle> _callback);

    private LoadBundleAsync loadBundleAsync;

    /// <summary>
    ///
    /// </summary>
    internal static float m_lastGcTime = 0;

    /// <summary>
    /// GC间隔
    /// </summary>
    internal const float __GC_INTERVAL = 1;

#if UNITY_EDITOR
    public bool m_useBundle = false;
#elif (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    public bool m_useBundle = true;
#endif

    /// <summary>
    /// 初始化成功标志
    /// </summary>
    public bool m_init = false;

    public static bool m_usePersistentPath = false;

    /// <summary>
    /// Lua路径
    /// </summary>
    public static string m_luaFilesPath;

    /// <summary>
    /// 有且唯一
    /// </summary>
    private LuaEnv m_luaenv = null;

    /// <summary>
    /// 储存加载的LuaAB
    /// </summary>
    private AssetBundle m_assetBundle = null;

    /// <summary>
    /// 全局函数字典
    /// </summary>
    private readonly Dictionary<string, LuaFunction> m_globalFuncDic = new Dictionary<string, LuaFunction>();

    /// <summary>
    /// XLuaBehaviour表,储存所有XLuaBehaviour对象
    /// </summary>
    public HashSet<XLuaBehaviour> m_scriptEnvDic = new HashSet<XLuaBehaviour>();

    /// <summary>
    /// 重读所有脚本
    /// </summary>
    private Action m_loadScript = null;

    private Func<string, LuaTable> m_require = null;

    private Func<string, LuaTable> m_dofile = null;

    /// <summary>
    /// 加载不执行
    /// </summary>
    private Func<string, LuaFunction> m_loadfile = null;

    #region Signature

    /// <summary>
    /// ras_key
    /// </summary>
    public static string PUBLIC_KEY = "";

    /// <summary>
    /// 加载
    /// </summary>
    private LuaEnv.CustomLoader m_signatureLoader = null;

    #endregion Signature

    /// <summary>
    /// 其他地方还要用
    /// </summary>
    /// <param name="_filepath"></param>
    /// <returns></returns>
    private byte[] XLuaCustomLoader(ref string _filepath)
    {
        // 普通加载
        if (!m_useBundle)
        {
            Debug.Log("#XLua# LoadLuaFormFile:" + _filepath);

            string _path = LuaPath(_filepath);

            byte[] _bytes = null;

            if (File.Exists(_path))
            {
                _bytes = File.ReadAllBytes(_path);
            }

            return _bytes;
        }
        // 走资源包
        else
        {
            Debug.Log("LoadLuaFormAB:" + _filepath);

            // 添加加载路径,2种
            string _path = __ASSETBUNDLE__PATH;
            _path = _path + _filepath.Replace(".lua", "").Replace('.', '/') + ".bytes";

            byte[] _bytes = null;

            if (m_assetBundle == null)
            {
                Debug.LogError("Script AssetBundle null!");
            }
            else
            {
                TextAsset _txtAsset = m_assetBundle.LoadAsset(_path) as TextAsset;
                _bytes = _txtAsset != null ? _txtAsset.bytes : null;
            }

            return _bytes;
        }
    }

    private void Awake()
    {
        m_luaFilesPath = Application.dataPath + __LUA_PATH;

        if (m_luaenv == null)
        {
            m_luaenv = new LuaEnv();
        }

        m_require = m_luaenv.Global.Get<Func<string, LuaTable>>("require");
        m_dofile = m_luaenv.Global.Get<Func<string, LuaTable>>("dofile");
        m_loadfile = m_luaenv.Global.Get<Func<string, LuaFunction>>("loadfile");

        m_luaenv.AddLoader(XLuaCustomLoader);
    }

    public void Init()
    {
        // 读取AssetBundle
        loadBundleAsync(__ABNAME, "", (_ab) =>
        {
            m_assetBundle = _ab;

            // 先调用HotFix
            CheckHotFix();

            Require(__LUA_INIT);

            m_loadScript = Get<Action>("Init", false);

            m_loadScript();

            TestCall();

            Debug.Log("#XLua# Init XLua OK!");

            m_init = true;
        });
    }

    public void DeInit()
    {
        m_loadScript = null;

        m_require = null;
        m_dofile = null;
        m_loadfile = null;

        m_enableHotFix = null;

        if (m_disableHotFix != null)
        {
            m_disableHotFix();

            m_disableHotFix = null;
        }

        m_signatureLoader = null;

        ClearAllCacheLuaFunction();

        foreach (var _table in m_scriptEnvDic)
        {
            if (_table != null)
            {
                _table.DisposeLua();
            }
        }

        m_scriptEnvDic.Clear();

        if (m_luaenv != null)
        {
            m_luaenv.Dispose();
            m_luaenv = null;
        }
    }

    /// <summary>
    /// 调用Lua代码中的初始化函数
    /// </summary>
    public void InitLogic()
    {
        CallGlobalFunc<Action>("Startup", false);
    }

    #region HotFix

    /// <summary>
    /// 结束的时候需要调用禁用hotfix
    /// </summary>
    private Action m_disableHotFix = null;

    /// <summary>
    /// 弃用hotfix
    /// </summary>
    private Action m_enableHotFix = null;

    /// <summary>
    ///
    /// </summary>
    private void CheckHotFix()
    {
        if (string.IsNullOrEmpty(PUBLIC_KEY))
        {
            // key无效
            return;
        }

        m_signatureLoader = new SignatureLoader(PUBLIC_KEY, (ref string _filepath) =>
        {
            _filepath = Path.Combine(Application.persistentDataPath, _filepath);

            if (File.Exists(_filepath))
            {
                return File.ReadAllBytes(_filepath);
            }
            else
            {
                return null;
            }
        });

        string _str = "GameData/Lua/hotfix.bytes";

        //this.DoString(m_signatureLoader(ref _str));

        //return;

        string _filePath = Path.Combine(Application.persistentDataPath, _str);

        Debug.Log("hotfix:" + _filePath);

        if (File.Exists(_filePath))
        {
            Debug.LogError("HotFix Start");

            this.DoString(File.ReadAllBytes(_filePath));

            m_enableHotFix = m_luaenv.Global.Get<Action>("EnableHotFix");

            m_disableHotFix = m_luaenv.Global.Get<Action>("DisableHotFix");

            // 调用
            m_enableHotFix();

            Debug.LogError("HotFix End");
        }
    }

    #endregion HotFix

    /// <summary>
    /// 这个方法等于-->require _file
    /// </summary>
    /// <param name="_file"></param>
    /// <returns></returns>
    public LuaTable Require(string _file)
    {
        if (m_luaenv != null)
        {
            Debug.Log(string.Format("Lua Require: {0}", _file));

            return m_require(_file);
        }

        return null;
    }

    public LuaTable DoFile(string _file)
    {
        if (m_luaenv != null)
        {
            Debug.Log(string.Format("#XLua# DoFile: {0}", _file));

            return m_dofile(_file);
        }

        return null;
    }

    public object[] DoString(string _str)
    {
        if (m_luaenv != null)
        {
            Debug.Log(string.Format("#XLua# DoString: {0}", _str));

            return m_luaenv.DoString(_str);
        }

        return null;
    }

    public object[] DoStringForPath(string _path, LuaTable _env, string _chunkName = "chunk")
    {
        var _bytes = XLuaCustomLoader(ref _path);

        if (_bytes != null)
        {
            var _objects = DoString(_bytes, _env, _chunkName);

            return _objects;
        }
        else
        {
            //Debug.LogError("DoStringForPath Error!");

            return null;
        }
    }

    public object[] DoString(string _chunk, LuaTable _env, string _chunkName = "chunk")
    {
        if (m_luaenv != null)
        {
            Debug.Log(string.Format("#XLua# DoString: {0}", _chunk));

            return m_luaenv.DoString(_chunk, _chunkName, _env);
        }

        return null;
    }

    public object[] DoString(byte[] _chunk, LuaTable _env, string _chunkName = "chunk")
    {
        if (m_luaenv != null)
        {
            Debug.Log(string.Format("#XLua# DoString: {0}", _chunk));

            return m_luaenv.DoString(_chunk, _chunkName, _env);
        }

        return null;
    }

    public object[] DoString(byte[] _bytes)
    {
        if (m_luaenv != null)
        {
            Debug.Log(string.Format("#XLua# DoString: {0}", _bytes));

            return m_luaenv.DoString(_bytes);
        }

        return null;
    }

    public void GC()
    {
        if (m_luaenv != null)
        {
            ClearAllCacheLuaFunction();

            m_luaenv.FullGc();
        }
    }

    /// <summary>
    /// 全局表
    /// </summary>
    public LuaTable GlobalTable
    {
        get
        {
            return m_luaenv.Global;
        }
    }

    public LuaTable CreateTable()
    {
        return m_luaenv.NewTable();
    }

    /// <summary>
    /// 从一个数组创建一个Table,从1开始哦
    /// </summary>
    /// <param name="_array"></param>
    /// <returns></returns>
    public LuaTable CreateTable(Array _array)
    {
        LuaTable _luaTable = m_luaenv.NewTable();

        for (int i = 0; i < _array.Length; ++i)
        {
            _luaTable.Set(i + 1, _array.GetValue(i));
        }

        return _luaTable;
    }

    /// <summary>
    /// 从泛型List构造一个LuaTable
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="_list"></param>
    /// <returns></returns>
    public LuaTable CreateTable<T>(List<T> _list)
    {
        LuaTable _luaTable = m_luaenv.NewTable();

        for (int i = 0; i < _list.Count; ++i)
        {
            _luaTable.Set(i + 1, _list[i]);
        }

        return _luaTable;
    }

    /// <summary>
    /// 从泛型字典构造一个LuaTable
    /// </summary>
    /// <typeparam name="K">字典的key类型</typeparam>
    /// <typeparam name="V">字典的value类型</typeparam>
    /// <param name="_dic"></param>
    /// <returns></returns>
    public LuaTable CreateTable<K, V>(Dictionary<K, V> _dic)
    {
        LuaTable _luaTable = m_luaenv.NewTable();

        foreach (var _kvp in _dic)
        {
            _luaTable.Set(_kvp.Key, _kvp.Value);
        }

        return _luaTable;
    }

    /// <summary>
    /// 返回一个LuaTable
    /// </summary>
    /// <param name="_name"></param>
    /// <param name="_findInPath">是否目录查找</param>
    /// <returns></returns>
    public LuaTable GetLuaTable(string _name, bool _findInPath)
    {
        LuaTable _table = null;

        if (_findInPath)
        {
            _table = m_luaenv.Global.GetInPath<LuaTable>(_name);
        }
        else
        {
            _table = m_luaenv.Global.Get<LuaTable>(_name);
        }

        if (_table == null)
        {
            Debug.LogError("#XLua# not find_[" + _name + "]_luaTable");

            return null;
        }

        return _table;
    }

    /// <summary>
    /// 返回一个Lua方法
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="_key"></param>
    /// <param name="_findInPath">目录中查找,类似aa.bb.cc</param>
    /// <returns></returns>
    public T Get<T>(string _key, bool _findInPath)
    {
        T _value;

        if (_findInPath)
        {
            _value = m_luaenv.Global.GetInPath<T>(_key);
        }
        else
        {
            _value = m_luaenv.Global.Get<T>(_key);
        }

        if (_value == null)
        {
            Debug.LogError(string.Format("#XLua#[Get] not find_[{0}]_Type_{1}", _key, typeof(T)));

            return default(T);
        }
        else
        {
            return _value;
        }
    }

    /// <summary>
    /// 调用一个LuaTable下的方法,这个调用的方法会在调用的时候传递一个table的self
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="_tableName"></param>
    /// <param name="_funcName"></param>
    /// <param name="_findInPath"></param>
    /// <param name="_args"></param>
    /// <returns></returns>
    public T CallTableFunc<T>(string _tableName, string _funcName, bool _findInPath, params object[] _args)
    {
        object[] _objects = CallTableFunc(_tableName, _funcName, _findInPath, _args);

        // 返回第一位
        if (_objects != null &&
            _objects.Length >= 1)
        {
            return (T)Convert.ChangeType(_objects[0], typeof(T));
        }

        return default(T);
    }

    /// <summary>
    /// 调用传入Table中的方法,这个调用会将传入的Table传递给方法
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="_luaTable"></param>
    /// <param name="_funcName"></param>
    /// <param name="_args"></param>
    /// <returns></returns>
    public T CallTableFunc<T>(LuaTable _luaTable, string _funcName, params object[] _args)
    {
        // 不需要再找一层,因为这里限定只找Table下面的
        LuaFunction _luaFunction = _luaTable.Get<LuaFunction>(_funcName);

        if (_luaFunction == null)
        {
            Debug.LogError("#XLua# not find_" + _funcName + "_func");

            return default(T);
        }
        else
        {
            // 需要传递Table进去
            object[] _toLuaArgs = new object[_args.Length + 1];
            _toLuaArgs[0] = _luaTable;
            _args.CopyTo(_toLuaArgs, 1);

            object[] _objects = _luaFunction.Call(_toLuaArgs);

            // 返回第一位
            if (_objects != null &&
                _objects.Length >= 1)
            {
                return (T)Convert.ChangeType(_objects[0], typeof(T));
            }

            return default(T);
        }
    }

    /// <summary>
    /// 调用一个LuaTable下的方法,这个调用的方法会在调用的时候传递一个table的self
    /// </summary>
    /// <param name="_tableName"></param>
    /// <param name="_funcName"></param>
    /// <param name="_findInPath"></param>
    /// <param name="_args"></param>
    /// <returns></returns>
    public object[] CallTableFunc(string _tableName, string _funcName, bool _findInPath, params object[] _args)
    {
        LuaTable _luaTable = GetLuaTable(_tableName, _findInPath);

        if (_luaTable != null)
        {
            // 不需要再找一层,因为这里限定只找Table下面的
            LuaFunction _luaFunction = _luaTable.Get<LuaFunction>(_funcName);

            if (_luaFunction == null)
            {
                Debug.LogError(string.Format("#XLua# not find_[{0}__{1}]_TableFunc", _tableName, _funcName));

                return null;
            }
            else
            {
                //Debug.LogError("_args:" + _args.Length + "__" + _args[0]);

                // 需要传递Table进去
                object[] _toLuaArgs = new object[_args.Length + 1];
                _toLuaArgs[0] = _luaTable;
                _args.CopyTo(_toLuaArgs, 1);

                return _luaFunction.Call(_toLuaArgs);
            }
        }

        return null;
    }

    /// <summary>
    /// 调用一个全局方法,如果_findInPath是true,可以查找类似aa.bb.cc()这种
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="_funcName"></param>
    /// <param name="_findInPath"></param>
    /// <param name="_args"></param>
    /// <returns></returns>
    public T CallGlobalFunc<T>(string _funcName, bool _findInPath, params object[] _args)
    {
        object[] _objects = CallGlobalFunc(_funcName, _findInPath, _args);

        // 返回第一位
        if (_objects != null &&
            _objects.Length >= 1)
        {
            return (T)Convert.ChangeType(_objects[0], typeof(T));
        }

        return default(T);
    }

    /// <summary>
    /// 调用一个全局方法,如果_findInPath是true,可以查找类似aa.bb.cc()这种
    /// </summary>
    /// <param name="_funcName"></param>
    /// <param name="_findInPath"></param>
    /// <param name="_args"></param>
    /// <returns></returns>
    public object[] CallGlobalFunc(string _funcName, bool _findInPath, params object[] _args)
    {
        if (m_globalFuncDic.ContainsKey(_funcName))
        {
            return m_globalFuncDic[_funcName].Call(_args);
        }

        LuaFunction _luaFunction;

        if (_findInPath)
        {
            _luaFunction = m_luaenv.Global.GetInPath<LuaFunction>(_funcName);
        }
        else
        {
            _luaFunction = m_luaenv.Global.Get<LuaFunction>(_funcName);
        }

        if (_luaFunction != null)
        {
            m_globalFuncDic[_funcName] = _luaFunction;

            return _luaFunction.Call(_args);
        }

        Debug.LogError("#XLua# not find_" + _funcName + "_func");

        return null;
    }

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    public void ClearAllCacheLuaFunction()
    {
        foreach (var _kvp in m_globalFuncDic)
        {
            _kvp.Value.Dispose();
        }

        m_globalFuncDic.Clear();
    }

    /// <summary>
    /// 取得Lua路径
    /// </summary>
    public static string LuaPath(string _name)
    {
        string _path;
#if UNITY_EDITOR
        _path = m_usePersistentPath ? Application.persistentDataPath : m_luaFilesPath;
#else
		_path = m_usePersistentPath ? Application.persistentDataPath : m_luaFilesPath;
#endif
        string _lowerName = _name.ToLower();

        if (_lowerName.EndsWith(".lua"))
        {
            int _index = _name.LastIndexOf('.');

            _name = _name.Substring(0, _index);
        }

        _name = _name.Replace('.', '/');

        return _path + _name + ".lua";
    }

    /// <summary>
    /// Update is called once per frame
    /// </summary>
    private void Update()
    {
        if (m_luaenv != null)
        {
            if (Time.time - m_lastGcTime > __GC_INTERVAL)
            {
                m_luaenv.Tick();
                m_lastGcTime = Time.time;
            }
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        DeInit();
    }

    #region 读取Lua配置

    /// <summary>
    /// Lua配置路径
    /// </summary>
    private const string __LUACONFIGPATH = "luasettings/";

    private LuaTable LoadLuaConfig(string _filePath)
    {
        if (!File.Exists(_filePath))
        {
            Debug.LogError(string.Format("Can't find file:{0}", _filePath));

            return null;
        }

        // 加载配置
        LuaTable _luaTable = m_dofile(_filePath);

        if (_luaTable == null)
        {
            Debug.LogError(string.Format("Can't dofile file:{0}", _filePath));

            return null;
        }

        return _luaTable;
    }

    /// <summary>
    /// 从字符串读取
    /// </summary>
    /// <param name="_str"></param>
    /// <returns></returns>
    private LuaTable LoadLuaConfigFromStr(string _str)
    {
        if (string.IsNullOrEmpty(_str))
        {
            Debug.LogError("String is empty");

            return null;
        }

        // 加载配置
        object[] _object = DoString(_str);

        if (_object == null)
        {
            Debug.LogError(string.Format("Can't load lua config from string:{0}", _str));

            return null;
        }

        return _object[0] as LuaTable;
    }

    /// <summary>
    /// require会去搜索路径下找
    /// 会在里面进行拼接.默认setting/xxx.lua
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="_lua"></param>
    /// <returns></returns>
    public Dictionary<TKey, TValue> LoadLuaConfigFromLua<TKey, TValue>(string _lua)
    {
        // 简化
        _lua = string.Format("{0}{1}", __LUACONFIGPATH, _lua);

        LuaTable _luaTable = m_require(_lua);

        if (_luaTable != null)
        {
            var _dic = _luaTable.Cast<Dictionary<TKey, TValue>>();

            return _dic;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// 文件中读取,绝对路径
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="_filePath"></param>
    /// <returns></returns>
    public Dictionary<TKey, TValue> LoadLuaConfigFromFile<TKey, TValue>(string _filePath)
    {
        LuaTable _luaTable = LoadLuaConfig(_filePath);

        if (_luaTable != null)
        {
            var _dic = _luaTable.Cast<Dictionary<TKey, TValue>>();

            return _dic;
        }
        else
        {
            return null;
        }
    }

    public List<T> LoadLuaConfigFromFile<T>(string _filePath)
    {
        LuaTable _luaTable = LoadLuaConfig(_filePath);

        if (_luaTable != null)
        {
            var _list = _luaTable.Cast<List<T>>();

            return _list;
        }
        else
        {
            return null;
        }
    }

    public Dictionary<TKey, TValue> LoadLuaConfigFromString<TKey, TValue>(string _str)
    {
        LuaTable _luaTable = LoadLuaConfigFromStr(_str);

        if (_luaTable != null)
        {
            var _dic = _luaTable.Cast<Dictionary<TKey, TValue>>();

            return _dic;
        }
        else
        {
            return null;
        }
    }

    public List<T> LoadLuaConfigFromString<T>(string _str)
    {
        LuaTable _luaTable = LoadLuaConfigFromStr(_str);

        if (_luaTable != null)
        {
            var _list = _luaTable.Cast<List<T>>();

            return _list;
        }
        else
        {
            return null;
        }
    }

    #endregion 读取Lua配置

    #region 测试方法

    /// <summary>
    /// 测试方法
    /// </summary>
    public void TestCall()
    {
    }

    #endregion 测试方法
}