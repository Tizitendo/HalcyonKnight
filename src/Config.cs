using BepInEx;
using BepInEx.Bootstrap;
using RiskOfOptions;
using RiskOfOptions.Options;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace HalcyonKnight;

static class Options
{
	public static bool IsEnabled => Chainloader.PluginInfos.ContainsKey(RiskOfOptions.PluginInfo.PLUGIN_GUID);

	public static void Init()
	{
		HalcyonKnight.ChangeShrineCredits = HalcyonKnight.Instance.Config.Bind("General", "Increase credit cost", false, "Increase stage 1 shrine credit cost 0 -> 30 (for reference stage 2+ is 50)");
		if (Options.IsEnabled)
		{
			RiskOfOptionsConfig();
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static void RiskOfOptionsConfig() {
		const string MOD_GUID = HalcyonKnight.PluginGUID;
		const string MOD_NAME = HalcyonKnight.PluginName;

		ModSettingsManager.AddOption(new CheckBoxOption(HalcyonKnight.ChangeShrineCredits), MOD_GUID, MOD_NAME);

		ModSettingsManager.SetModDescription($"Options for {MOD_NAME}", MOD_GUID, MOD_NAME);

		FileInfo iconFile = null;
		DirectoryInfo dir = new DirectoryInfo(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
		do
		{
			FileInfo[] files = dir.GetFiles("icon.png", SearchOption.TopDirectoryOnly);
			if (files != null && files.Length > 0)
			{
				iconFile = files[0];
				break;
			}

			dir = dir.Parent;
		} while (dir != null && dir.Exists && !string.Equals(dir.Name, "plugins", StringComparison.OrdinalIgnoreCase));

		if (iconFile != null)
		{
			Texture2D iconTexture = new Texture2D(256, 256);
			if (iconTexture.LoadImage(File.ReadAllBytes(iconFile.FullName)))
			{
				Sprite iconSprite = Sprite.Create(iconTexture, new Rect(0f, 0f, iconTexture.width, iconTexture.height), new Vector2(0.5f, 0.5f));
				iconSprite.name = $"{MOD_NAME}Icon";

				ModSettingsManager.SetModIcon(iconSprite, MOD_GUID, MOD_NAME);
			}
		}
	}
}