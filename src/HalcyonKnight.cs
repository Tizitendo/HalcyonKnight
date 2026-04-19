using BepInEx;
using Logger;
using MiscFixes.Modules;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.CharacterAI;
using RoR2.ContentManagement;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

[assembly: HG.Reflection.SearchableAttribute.OptIn]

namespace HalcyonKnight;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]

public class HalcyonKnight : BaseUnityPlugin
{
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "Onyx";
    public const string PluginName = "HalcyonKnight";
    public const string PluginVersion = "1.0.0";

    public void Awake()
    {
        Log.Init(Logger);

		AssetReferenceT<EntityStateConfiguration> stateConfig = new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_ChargeTriLaser_asset);
		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(stateConfig).Completed += (x) =>
		{
			Extensions.TryModifyFieldValue<float>(x.Result, "baseDuration", 1.5f);
		};

		stateConfig = new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_WhirlwindPersuitCycle_asset);
		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(stateConfig).Completed += (x) =>
		{
			x.Result.TryModifyFieldValue<float>("dashSpeedCoefficient", 30f); // 20
			x.Result.TryModifyFieldValue<float>("decelerateDuration", 0.5f); // 1
		};

		stateConfig = new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_GoldenSwipe_asset);
		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(stateConfig).Completed += (x) =>
		{
			x.Result.TryModifyFieldValue<float>("baseDuration", 1.2f); // 1
			x.Result.TryModifyFieldValue<float>("damageCoefficient", 1.2f); // 1.5
			x.Result.TryModifyFieldValue<float>("pushAwayForce", 500f); // 2000
		};

		stateConfig = new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_GoldenSlash_asset);
		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(stateConfig).Completed += (x) =>
		{
			x.Result.TryModifyFieldValue<float>("pushAwayForce", 500f); // 2000
		};

		AssetReferenceT<GameObject> obj = new(RoR2_DLC2_Halcyonite.HalcyoniteMaster_prefab);
		AssetAsyncReferenceManager<GameObject>.LoadAsset(obj).Completed += (x) =>
		{
			GameObject master = x.Result;
			foreach(AISkillDriver skillDriver in master.GetComponents<AISkillDriver>())
			{
				switch (skillDriver.customName)
				{
					case "Golden Swipe":
						skillDriver.minDistance = 0f;
						skillDriver.movementType = AISkillDriver.MovementType.FleeMoveTarget;
						break;
					case "Golden Slash":
						skillDriver.movementType = AISkillDriver.MovementType.FleeMoveTarget;
						break;
					case "TriLaser":
						skillDriver.minDistance = 10f;
						skillDriver.movementType = AISkillDriver.MovementType.FleeMoveTarget;
						break;
				}
			}
		};

		AssetReferenceT<SkillDef> skillDef = new(RoR2_DLC2_Halcyonite.HalcyoniteMonsterWhirlwindRush_asset);
		AssetAsyncReferenceManager<SkillDef>.LoadAsset(skillDef).Completed += (x) =>
		{
			SkillDef swipeSkill = x.Result;
			swipeSkill.baseRechargeInterval = 15;
		};

		IL.EntityStates.Halcyonite.TriLaser.FixedUpdate += MoreLasers;
		IL.RoR2.CombatDirector.Spawn += ForceHalcyonBoss;
		On.EntityStates.Halcyonite.TriLaser.OnEnter += TriLaser_OnEnter;
		On.RoR2.CharacterMaster.OnBodyStart += OnBodyStart;
	}

	static void OnBodyStart(On.RoR2.CharacterMaster.orig_OnBodyStart orig, CharacterMaster self, CharacterBody body)
	{
		orig(self, body);
		if (body.name == "HalcyoniteBody(Clone)")
		{
			body.modelLocator.modelTransform.GetChild(4).localScale = new Vector3(4f, 6f, 12f);
			body.modelLocator.modelTransform.GetChild(7).localScale = new Vector3(15f, 1.3f, 9f);
		}
	}

	static void TriLaser_OnEnter(On.EntityStates.Halcyonite.TriLaser.orig_OnEnter orig, EntityStates.Halcyonite.TriLaser self)
	{
		orig(self);
		self.targetTimeStamp = 0.1f;
		self.fireCooldown = 0.3f;
	}

	static void ForceHalcyonBoss(ILContext il)
	{
		ILCursor c = new ILCursor(il);

		if (c.TryGotoNext(MoveType.After,
				x => x.MatchCallOrCallvirt(typeof(DirectorCore), nameof(DirectorCore.TrySpawnObject))
			))
		{
			c.Emit(OpCodes.Ldarg_0);
			c.EmitDelegate<Func<GameObject, CombatDirector, GameObject>>(HalcyonBoss);
		}
		else
		{
			Log.Error(il.Method.Name + " IL Hook failed!");
		}

		GameObject HalcyonBoss(GameObject enemy, CombatDirector self)
		{
			if (self.isHalcyonShrineSpawn && enemy)
			{
				if (enemy.TryGetComponent<CharacterMaster>(out CharacterMaster master))
				{
					master.isBoss = true;
				}
			}
			return enemy;
		}
	}

	static void MoreLasers(ILContext il)
	{
		ILCursor c = new ILCursor(il);
		int patchCount = 0;
		int laserCount = 0;

		while (c.TryGotoNext(MoveType.After,
				x => x.MatchLdfld(typeof(EntityStates.Halcyonite.TriLaser), nameof(EntityStates.Halcyonite.TriLaser.timesFired)),
				x => x.MatchLdcI4(out laserCount),
				x => !x.MatchAdd()
			))
		{
			c.Index--;
			c.Emit(OpCodes.Pop);
			c.Emit(OpCodes.Ldc_I4, laserCount + 2);
			patchCount++;
		}

		if(patchCount == 0)
		{
			Log.Error(il.Method.Name + " IL Hook failed!");
		}
		//Log.Info(il.Method.Name + " Patch Count: " + patchCount);
	}
}
