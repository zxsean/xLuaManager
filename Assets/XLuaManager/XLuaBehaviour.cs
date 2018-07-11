using System;
using UnityEngine;
using XLua;

/// <summary>
/// Lua
/// </summary>
public class XLuaBehaviour : GameUtilities.GameMonoBehaviour
{
    private bool m_destroyed = false;

    public Injection[] m_injections;

    private Action m_luaStart;
    private Action m_luaEnable;
    private Action m_luaDisable;
    private Action m_luaUpdate;
    private Action m_luaOnDestroy;

    /// <summary>
    /// 运行环境,方便隔离
    /// </summary>
    private LuaTable m_scriptEnv;

    /// <summary>
    /// 储存一下,因为目前lua写法是返回一个table
    /// </summary>
    protected LuaTable m_luaTable = null;

    protected virtual string ScriptFileName { get; set; }

    protected override void Awake()
    {
        m_scriptEnv = XLuaManager.GetInstance().CreateTable();

        // 为每个脚本设置一个独立的环境，可一定程度上防止脚本间全局变量、函数冲突
        LuaTable _meta = XLuaManager.GetInstance().CreateTable();
        _meta.Set("__index", XLuaManager.GetInstance().GlobalTable);
        m_scriptEnv.SetMetaTable(_meta);
        _meta.Dispose();

        m_scriptEnv.Set("self", this);

        foreach (var _injection in m_injections)
        {
            m_scriptEnv.Set(_injection.name, _injection.value);
        }

        var _chunk = ScriptFileName;

        // 读取
        var _objects = XLuaManager.GetInstance().DoStringForPath(_chunk, m_scriptEnv, _chunk);

        if (_objects != null &&
            _objects.Length > 0)
        {
            m_luaTable = _objects[0] as LuaTable; ;
        }
        else
        {
            //Debug.LogError("没找到对应lua");
        }

        Action _luaAwake = null;

        if (m_luaTable != null)
        {
            Debug.LogWarning("成功读取函数" + ScriptFileName);

            _luaAwake = m_luaTable.Get<Action>("Awake");
            m_luaTable.Get("Start", out m_luaStart);
            m_luaTable.Get("Update", out m_luaUpdate);
            m_luaTable.Get("OnDestroy", out m_luaOnDestroy);
            m_luaTable.Get("OnEnable", out m_luaEnable);
            m_luaTable.Get("OnDisable", out m_luaDisable);
        }

        if (!XLuaManager.GetInstance().m_scriptEnvDic.Contains(this))
        {
            XLuaManager.GetInstance().m_scriptEnvDic.Add(this);

            //Debug.LogError("添加");
        }

        //Action _luaAwake = scriptEnv.Get<Action>("Awake");
        //scriptEnv.Get("Start", out luaStart);
        //scriptEnv.Get("Update", out luaUpdate);
        //scriptEnv.Get("OnDestroy", out luaOnDestroy);
        //scriptEnv.Get("OnEnable", out luaEnable);
        //scriptEnv.Get("OnDisable", out luaDisable);

        if (_luaAwake != null)
        {
            _luaAwake();

            _luaAwake = null; ;
        }
    }

    protected void GetFunc<T>(string _key, out T _value)
    {
        if (m_luaTable != null)
        {
            m_luaTable.Get(_key, out _value);
        }
        else
        {
            _value = default(T);
        }
    }

    protected virtual void Start()
    {
        if (m_luaStart != null)
        {
            m_luaStart();

            m_luaStart = null;
        }
    }

    protected virtual void OnEnable()
    {
        if (m_luaEnable != null)
        {
            m_luaEnable();
        }
    }

    protected virtual void OnDisable()
    {
        if (m_luaDisable != null)
        {
            m_luaDisable();
        }
    }

    protected virtual void Update()
    {
        if (m_luaUpdate != null)
        {
            m_luaUpdate();
        }
    }

    /// <summary>
    /// 释放Lua
    /// </summary>
    public virtual void DisposeLua()
    {
        if (!m_destroyed)
        {
            m_destroyed = true;

            if (m_luaOnDestroy != null)
            {
                m_luaOnDestroy();
            }

            m_luaOnDestroy = null;
            m_luaUpdate = null;
            m_luaEnable = null;
            m_luaDisable = null;

            if (m_luaTable != null)
            {
                m_luaTable.Dispose();
                m_luaTable = null;
            }

            if (m_scriptEnv != null)
            {
                m_scriptEnv.Dispose();
                m_scriptEnv = null;
            }
        }
    }

    protected virtual void OnDestroy()
    {
        DisposeLua();

        m_injections = null;
    }
}