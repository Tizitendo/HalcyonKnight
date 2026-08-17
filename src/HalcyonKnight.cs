using BepInEx;
using BepInEx.Configuration;
using EntityStates.Halcyonite;
using HG;
using Logger;
using MiscFixes.Modules;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using RoR2.ContentManagement;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPathsBetter;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

[assembly: HG.Reflection.SearchableAttribute.OptIn]

namespace HalcyonKnight;

[BepInDependency(HalcyonFixes.HalcyonFixes.PluginGUID, BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(RiskOfOptions.PluginInfo.PLUGIN_GUID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public sealed class HalcyonKnight : BaseUnityPlugin
{
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "Onyx";
    public const string PluginName = "HalcyonKnight";
    public const string PluginVersion = "1.1.9";

	public static HalcyonKnight Instance;
	public static ConfigEntry<bool> ChangeShrineCredits { get; set; }

	public void Awake()
    {
		Log.Init(Logger);
		Instance = SingletonHelper.Assign(Instance, this);
		Options.Init();

		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_ChargeTriLaser_asset)).Completed += (x) =>
		{
			x.Result.TryModifyFieldValue<float>("baseDuration", 1.5f);
		};

		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_TriLaser_asset)).Completed += (x) =>
		{
			x.Result.TryModifyFieldValue<float>("blastRadius", 2f); // 4
		};

		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_WhirlwindWarmUp_asset)).Completed += (x) =>
		{
			x.Result.TryModifyFieldValue<float>("duration", 0.7f); // 0.5
		};

		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_WhirlwindPersuitCycle_asset)).Completed += (x) =>
		{
			x.Result.TryModifyFieldValue<float>("dashSpeedCoefficient", 40f); // 20
			x.Result.TryModifyFieldValue<float>("decelerateDuration", 0.5f); // 1
			x.Result.TryModifyFieldValue<float>("dashSafeExitDuration", 3f); // 5
		};

		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_GoldenSwipe_asset)).Completed += (x) =>
		{
			x.Result.TryModifyFieldValue<float>("baseDuration", 1.5f); // 1
			//x.Result.TryModifyFieldValue<float>("damageCoefficient", 1.2f); // 1.5
		};

		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_GoldenSlash_asset)).Completed += (x) =>
		{
			x.Result.TryModifyFieldValue<float>("baseDuration", 1.1f); // 1
		};

		AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_DLC2_Halcyonite.HalcyoniteMaster_prefab)).Completed += (x) =>
		{
			GameObject master = x.Result;
			foreach (AISkillDriver skillDriver in master.GetComponents<AISkillDriver>())
			{
				switch (skillDriver.customName)
				{
					case "Golden Swipe":
						skillDriver.minDistance = 0f;
						skillDriver.movementType = AISkillDriver.MovementType.Stop;
						//skillDriver.moveInputScale = 0.3f;
						skillDriver.maxDistance = 15f;
						skillDriver.driverUpdateTimerOverride = 1.5f;
						skillDriver.aimVectorMaxSpeedOverride = 0f;
						break;
					case "Golden Slash":
						skillDriver.movementType = AISkillDriver.MovementType.FleeMoveTarget;
						//skillDriver.moveInputScale = 0.8f;
						skillDriver.maxDistance = 10f;
						skillDriver.driverUpdateTimerOverride = 2f;
						break;
					case "TriLaser":
						skillDriver.minDistance = 15f;
						skillDriver.moveInputScale = 0.7f;
						skillDriver.driverUpdateTimerOverride = 2.5f;
						skillDriver.movementType = AISkillDriver.MovementType.ChaseMoveTarget;
						break;
					case "WhirlwindRush":
						skillDriver.minDistance = 20f; // 20
						break;
					case "Follow Target":
						skillDriver.minDistance = 5;
						skillDriver.driverUpdateTimerOverride = 0.5f;
						break;
					case "Follow Nodegraph":
						skillDriver.minDistance = 5;
						break;
				}
			}

			AISkillDriver maintainDistance = master.AddComponent<AISkillDriver>();
			maintainDistance.minDistance = 0;
			maintainDistance.maxDistance = float.MaxValue;
			maintainDistance.movementType = AISkillDriver.MovementType.FleeMoveTarget;

			if (master.TryGetComponent<BaseAI>(out BaseAI baseAI))
			{
				baseAI.prioritizePlayers = true;
			}
		};

		AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_DLC2.ShrineHalcyonite_prefab)).Completed += (x) =>
		{
			GameObject shrine = x.Result;
			BossGroup bossGroup = shrine.EnsureComponent<BossGroup>();
			shrine.GetComponent<PurchaseInteraction>().setUnavailableOnTeleporterActivated = true;
		};

		AssetAsyncReferenceManager<SkillDef>.LoadAsset(new(RoR2_DLC2_Halcyonite.HalcyoniteMonsterWhirlwindRush_asset)).Completed += (x) =>
		{
			SkillDef swipeSkill = x.Result;
			swipeSkill.baseRechargeInterval = 15;
		};

		AssetAsyncReferenceManager<SkillDef>.LoadAsset(new(RoR2_DLC2_Halcyonite.HalcyoniteMonsterGoldenSlash_asset)).Completed += (x) =>
		{
			SkillDef swipeSkill = x.Result;
			swipeSkill.baseRechargeInterval = 7;
		};

		AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_DLC2_Halcyonite.HalcyoniteBody_prefab)).Completed += (x) =>
		{
			if (x.Result.TryGetComponent(out CharacterBody body))
			{
				body.baseMoveSpeed = 9; // 6.6
				// body.baseNameToken = "Halcyon Knight";
				body.subtitleNameToken = "HALCYONITE_BODY_SUBTITLE";
				// body.subtitleNameToken = "Forsaken Heir";
			}
			if (x.Result.TryGetComponent(out ModelLocator modelLocator) && modelLocator.modelTransform)
			{
				Transform pokeHitbox = modelLocator.modelTransform.Find("HitboxGoldenSword");
				if (pokeHitbox)
				{
					pokeHitbox.localScale = new Vector3(2f, 6f, 12f);
				}
				Transform swipeHitbox = modelLocator.modelTransform.Find("HitboxGoldenSlash");
				if (swipeHitbox)
				{
					swipeHitbox.localScale = new Vector3(15f, 0.5f, 10f);
				}
			}
			if (x.Result.TryGetComponent(out SetStateOnHurt setStateOnHurt))
			{
				setStateOnHurt.canBeHitStunned = false;
			}
		};

		string path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lang", "Halc.language");
		if (File.Exists(path))
		{
			LanguageAPI.AddOverlayPath(path);
		} else {
			Log.Error("Failed to find path: " + path);
		}

		IL.EntityStates.Halcyonite.TriLaser.FixedUpdate += TriLaser_FixedUpdate;
		On.EntityStates.Halcyonite.TriLaser.OnEnter += TriLaser_OnEnter;
		On.EntityStates.Halcyonite.WhirlWindPersuitCycle.UpdateFindTarget += UpdateFindTarget;
		On.EntityStates.Halcyonite.TriLaser.FireTriLaser += TriLaser_FireTriLaser;
	}

	static void UpdateFindTarget(On.EntityStates.Halcyonite.WhirlWindPersuitCycle.orig_UpdateFindTarget orig, EntityStates.Halcyonite.WhirlWindPersuitCycle self)
	{
		if (!self.targetBody)
		{
			foreach (BaseAI baseAI in self.characterBody.master.AiComponents)
			{
				if (baseAI.hasAimTarget)
				{
					self.targetBody = baseAI.skillDriverEvaluation.target.characterBody;
					self.targetPos = self.targetBody.footPosition + (self.transform.position - self.targetBody.footPosition).normalized * 2f;
					self.findTargetTimeStamp = self.fixedAge;
					self.startForwardDirt = self.characterDirection.forward;
					break;
				}
			}
		}
		
		orig(self);
		
		if (!self.targetBody &&
		Physics.Raycast(new Ray(self.transform.position, Vector3.down), out _, 50f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
		{
			self.outer.SetNextStateToMain();
		}
	}

	static void TriLaser_OnEnter(On.EntityStates.Halcyonite.TriLaser.orig_OnEnter orig, EntityStates.Halcyonite.TriLaser self)
	{
		orig(self);
		self.targetTimeStamp = 0.1f;
		self.fireCooldown = 0.3f;
	}

	static void TriLaser_FixedUpdate(ILContext il)
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
			c.Emit(OpCodes.Ldarg_0);
			c.EmitDelegate<Func<int, TriLaser, int>>(MoreLasers);
			patchCount++;
		}

		int MoreLasers(int laserCount, TriLaser self)
		{
			if (self.TryGetComponent(out HealthComponent healthComponent) && healthComponent.health <= healthComponent.fullCombinedHealth / 2)
			{
				return laserCount + 12;
			}
			return laserCount + 2;
		}

		if(patchCount == 0)
		{
			Log.Error(il.Method.Name + " IL Hook failed!");
		}
		//Log.Info(il.Method.Name + " Patch Count: " + patchCount);
	}

	private void TriLaser_FireTriLaser(On.EntityStates.Halcyonite.TriLaser.orig_FireTriLaser orig, EntityStates.Halcyonite.TriLaser self)
	{
		orig(self);
		foreach (BaseAI baseAI in self.characterBody.master.AiComponents)
		{
			if (baseAI.hasAimTarget)
			{
				if (Vector3.Distance(baseAI.skillDriverEvaluation.target.characterBody.transform.position, self.transform.position) < 15)
				{
					self.outer.SetNextStateToMain();
				}
				break;
			}
		}
	}
}
