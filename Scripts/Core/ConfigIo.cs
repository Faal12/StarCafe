using System;
using Godot;

public static class ConfigIo
{
	public static ConfigFile LoadOrEmpty(string path)
	{
		var config = new ConfigFile();
		config.Load(path);
		return config;
	}

	public static T ReadEnum<T>(ConfigFile config, string section, string key, T fallback) where T : struct, Enum
	{
		int raw = config.GetValue(section, key, Convert.ToInt32(fallback)).AsInt32();
		return Enum.IsDefined(typeof(T), raw) ? (T)Enum.ToObject(typeof(T), raw) : fallback;
	}

	public static int ReadInt(ConfigFile config, string section, string key, int fallback, int min, int max) =>
		Math.Clamp(config.GetValue(section, key, fallback).AsInt32(), min, max);

	public static double ReadDouble(ConfigFile config, string section, string key, double fallback, double min, double max) =>
		Math.Clamp(config.GetValue(section, key, fallback).AsDouble(), min, max);

	public static bool ReadBool(ConfigFile config, string section, string key, bool fallback) =>
		config.GetValue(section, key, fallback).AsBool();

	public static string ReadString(ConfigFile config, string section, string key, string fallback) =>
		config.GetValue(section, key, fallback).AsString();
}
