using System;
using System.Collections.Generic;
using System.IO;

namespace MoonwalkPro;

/// <summary>Minimal INI reader/writer that preserves comments and ordering on save.</summary>
internal sealed class Ini
{
    readonly List<string> _lines;
    readonly string _path;

    public Ini(string path)
    {
        _path = path;
        _lines = File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string>();
    }

    static bool IsSection(string line, out string name)
    {
        var t = line.Trim();
        if (t.StartsWith("[") && t.EndsWith("]")) { name = t.Substring(1, t.Length - 2).Trim(); return true; }
        name = "";
        return false;
    }

    static bool IsKey(string line, out string key, out string val)
    {
        key = ""; val = "";
        var t = line.TrimStart();
        if (t.StartsWith(";") || t.StartsWith("#")) return false;
        int eq = line.IndexOf('=');
        if (eq < 0) return false;
        key = line.Substring(0, eq).Trim();
        val = line.Substring(eq + 1).Trim();
        return key.Length > 0;
    }

    public string Get(string section, string key, string def)
    {
        string cur = "";
        foreach (var line in _lines)
        {
            if (IsSection(line, out var s)) { cur = s; continue; }
            if (cur.Equals(section, StringComparison.OrdinalIgnoreCase)
                && IsKey(line, out var k, out var v)
                && k.Equals(key, StringComparison.OrdinalIgnoreCase))
                return v;
        }
        return def;
    }

    public int GetInt(string section, string key, int def)
        => int.TryParse(Get(section, key, def.ToString()), out int v) ? v : def;

    public bool GetBool(string section, string key, bool def)
        => GetInt(section, key, def ? 1 : 0) != 0;

    public void Set(string section, string key, string value)
    {
        int sectionStart = -1, sectionEnd = _lines.Count;
        for (int i = 0; i < _lines.Count; i++)
        {
            if (IsSection(_lines[i], out var s))
            {
                if (sectionStart < 0 && s.Equals(section, StringComparison.OrdinalIgnoreCase)) sectionStart = i;
                else if (sectionStart >= 0) { sectionEnd = i; break; }
            }
        }

        if (sectionStart < 0)
        {
            if (_lines.Count > 0 && _lines[^1].Trim().Length > 0) _lines.Add("");
            _lines.Add("[" + section + "]");
            _lines.Add(key + "=" + value);
            return;
        }

        for (int j = sectionStart + 1; j < sectionEnd; j++)
        {
            if (IsKey(_lines[j], out var k, out _) && k.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                _lines[j] = key + "=" + value;
                return;
            }
        }
        _lines.Insert(sectionEnd, key + "=" + value);
    }

    public void Save() => File.WriteAllLines(_path, _lines);
}
