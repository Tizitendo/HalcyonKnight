using BepInEx;
using BepInEx.Configuration;
using EntityStates.Halcyonite;
using HG;
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
using UnityEngine.Networking;

[assembly: HG.Reflection.SearchableAttribute.OptIn]

namespace HalcyonKnight;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public sealed class HalcyonKnight : BaseUnityPlugin
{
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "Onyx";
    public const string PluginName = "HalcyonKnight";
    public const string PluginVersion = "1.1.9";

	public static HalcyonKnight Instance;
	public static ConfigEntry<bool> ChangeShrineCredits { get; set; }

	static SpawnCard halcshrineCard;

	public void Awake()
    {
		Log.Init(Logger);
		Instance = SingletonHelper.Assign(Instance, this);
		Options.Init();

		AssetReferenceT<EntityStateConfiguration> stateConfig = new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_ChargeTriLaser_asset);
		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(stateConfig).Completed += (x) =>
		{
			x.Result.TryModifyFieldValue<float>("baseDuration", 1.5f);
		};

		stateConfig = new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_TriLaser_asset);
		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(stateConfig).Completed += (x) =>
		{
			x.Result.TryModifyFieldValue<float>("blastRadius", 2f); // 4
		};

		stateConfig = new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_WhirlwindWarmUp_asset);
		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(stateConfig).Completed += (x) =>
		{
			x.Result.TryModifyFieldValue<float>("duration", 0.7f); // 0.5
		};

		stateConfig = new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_WhirlwindPersuitCycle_asset);
		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(stateConfig).Completed += (x) =>
		{
			x.Result.TryModifyFieldValue<float>("dashSpeedCoefficient", 40f); // 20
			x.Result.TryModifyFieldValue<float>("decelerateDuration", 0.5f); // 1
			x.Result.TryModifyFieldValue<float>("dashSafeExitDuration", 3f); // 5
		};

		stateConfig = new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_GoldenSwipe_asset);
		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(stateConfig).Completed += (x) =>
		{
			x.Result.TryModifyFieldValue<float>("baseDuration", 1.3f); // 1
			//x.Result.TryModifyFieldValue<float>("damageCoefficient", 1.2f); // 1.5
			x.Result.TryModifyFieldValue<float>("pushAwayForce", 500f); // 2000
		};

		stateConfig = new(RoR2_DLC2_Halcyonite.EntityStates_HalcyoniteMonster_GoldenSlash_asset);
		AssetAsyncReferenceManager<EntityStateConfiguration>.LoadAsset(stateConfig).Completed += (x) =>
		{
			x.Result.TryModifyFieldValue<float>("pushAwayForce", 500f); // 2000
			x.Result.TryModifyFieldValue<float>("baseDuration", 1.1f); // 1
		};

		AssetReferenceT<GameObject> obj = new(RoR2_DLC2_Halcyonite.HalcyoniteMaster_prefab);
		AssetAsyncReferenceManager<GameObject>.LoadAsset(obj).Completed += (x) =>
		{
			GameObject master = x.Result;
			foreach (AISkillDriver skillDriver in master.GetComponents<AISkillDriver>())
			{
				switch (skillDriver.customName)
				{
					case "Golden Swipe":
						skillDriver.minDistance = 0f;
						skillDriver.movementType = AISkillDriver.MovementType.FleeMoveTarget;
						skillDriver.moveInputScale = 0.5f;
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
						//skillDriver.moveInputScale = 0.7f;
						skillDriver.driverUpdateTimerOverride = 2.5f;
						skillDriver.movementType = AISkillDriver.MovementType.ChaseMoveTarget;
						break;
					case "WhirlwindRush":
						skillDriver.minDistance = 20f; // 20
						break;
					case "Follow Target":
						skillDriver.minDistance = 5;
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

		obj = new(RoR2_DLC2.ShrineHalcyonite_prefab);
		AssetAsyncReferenceManager<GameObject>.LoadAsset(obj).Completed += (x) =>
		{
			GameObject shrine = x.Result;
			BossGroup bossGroup = shrine.EnsureComponent<BossGroup>();
			shrine.GetComponent<PurchaseInteraction>().setUnavailableOnTeleporterActivated = true;
		};

		AssetReferenceT<SkillDef> skillDef = new(RoR2_DLC2_Halcyonite.HalcyoniteMonsterWhirlwindRush_asset);
		AssetAsyncReferenceManager<SkillDef>.LoadAsset(skillDef).Completed += (x) =>
		{
			SkillDef swipeSkill = x.Result;
			swipeSkill.baseRechargeInterval = 15;
		};

		skillDef = new(RoR2_DLC2_Halcyonite.HalcyoniteMonsterGoldenSlash_asset);
		AssetAsyncReferenceManager<SkillDef>.LoadAsset(skillDef).Completed += (x) =>
		{
			SkillDef swipeSkill = x.Result;
			swipeSkill.baseRechargeInterval = 7;
		};

		IL.EntityStates.Halcyonite.TriLaser.FixedUpdate += TriLaser_FixedUpdate;
		On.EntityStates.Halcyonite.TriLaser.OnEnter += TriLaser_OnEnter;
		On.RoR2.CharacterMaster.OnBodyStart += OnBodyStart;
		On.EntityStates.Halcyonite.WhirlWindPersuitCycle.UpdateFindTarget += UpdateFindTarget;
		IL.RoR2.HalcyoniteShrineInteractable.DrainConditionMet += DrainConditionMet;
		On.RoR2.PurchaseInteraction.OnTeleporterBeginCharging += OnTeleporterBeginCharging;
		On.RoR2.HalcyoniteShrineInteractable.CalculateCredits += HalcyoniteShrineInteractable_CalculateCredits;
		On.EntityStates.Halcyonite.TriLaser.FireTriLaser += TriLaser_FireTriLaser;

		OptionChangeShrineCredits(null, null);
		ChangeShrineCredits.SettingChanged += OptionChangeShrineCredits;

		On.EntityStates.ShrineHalcyonite.ShrineHalcyoniteNoQuality.OnEnter += (orig, self) =>
		{
			orig(self);
			self.transform.Find("meshHalcyoniteShrineStorm").gameObject.SetActive(true);
			self.transform.Find("Particle System").gameObject.SetActive(true);
		};

		On.RoR2.HalcyoniteShrineInteractable.DestroyDrainVFX += (orig, self) =>
		{
			orig(self);
			self.transform.Find("meshHalcyoniteShrineStorm").gameObject.SetActive(false);
			self.transform.Find("Particle System").gameObject.SetActive(false);
		};

		On.EntityStates.ShrineHalcyonite.ShrineHalcyoniteFinished.OnEnter += (orig, self) =>
		{
			orig(self);
			self.transform.Find("meshHalcyoniteShrineStorm").gameObject.SetActive(false);
			self.transform.Find("Particle System").gameObject.SetActive(false);
		};
	}

	private void OptionChangeShrineCredits(object sender, EventArgs e)
	{
		if (ChangeShrineCredits.Value)
		{
			On.RoR2.SceneDirector.GenerateInteractableCardSelection += GenerateInteractableCardSelection;
			AssetReferenceT<SpawnCard> interactableCard = new(RoR2_DLC2.iscShrineHalcyoniteTier1_asset);
			AssetAsyncReferenceManager<SpawnCard>.LoadAsset(interactableCard).Completed += (x) =>
			{
				x.Result.directorCreditCost = 30;
				halcshrineCard = x.Result;
			};
		} else {
			On.RoR2.SceneDirector.GenerateInteractableCardSelection -= GenerateInteractableCardSelection;
			AssetReferenceT<SpawnCard> interactableCard = new(RoR2_DLC2.iscShrineHalcyoniteTier1_asset);
			AssetAsyncReferenceManager<SpawnCard>.LoadAsset(interactableCard).Completed += (x) =>
			{
				x.Result.directorCreditCost = 0;
				halcshrineCard = x.Result;
			};
		}
	}

	private WeightedSelection<DirectorCard> GenerateInteractableCardSelection(On.RoR2.SceneDirector.orig_GenerateInteractableCardSelection orig, SceneDirector self)
	{
		WeightedSelection<DirectorCard> result = orig(self);
		for(int i = 0; i < result.Count; i++)
		{
			WeightedSelection<DirectorCard>.ChoiceInfo choice = result.GetChoice(i);
			if(choice.value.spawnCard == halcshrineCard)
			{
				result.ModifyChoiceWeight(i, choice.weight * 2);
			}
		}
		return result;
	}

	private void HalcyoniteShrineInteractable_CalculateCredits(On.RoR2.HalcyoniteShrineInteractable.orig_CalculateCredits orig, HalcyoniteShrineInteractable self)
	{
		orig(self);
		if (self.scaleMonsterCreditWithDifficultyCoefficient)
		{
			self.monsterCredit /= Math.Max(Run.instance.difficultyCoefficient * 0.3f, 1);
		}
	}

	private void OnTeleporterBeginCharging(On.RoR2.PurchaseInteraction.orig_OnTeleporterBeginCharging orig, TeleporterInteraction self)
	{
		orig(self);
		if (!NetworkServer.active)
		{
			return;
		}
		foreach (PurchaseInteraction instances in InstanceTracker.GetInstancesList<PurchaseInteraction>())
		{
			if (instances.name == "ShrineHalcyonite(Clone)")
			{
				if (instances.TryGetComponent(out ChildLocator childLocator))
				{
					Transform child;
					if (childLocator.TryFindChild("GoldSiphonNearbyBodyAttachment", out child))
					{
						child.gameObject.SetActive(false);
					}
					if (childLocator.TryFindChild("StormPortalIndicator", out child))
					{
						child.gameObject.SetActive(false);
					}
					if (childLocator.TryFindChild("RangeIndicator", out child))
					{
						child.gameObject.SetActive(false);
					}
					if (childLocator.TryFindChild("GoldshoresPortalIndicator", out child))
					{
						child.gameObject.SetActive(false);
					}
				}
			}
		}
	}

	private void DrainConditionMet(ILContext il)
	{
		ILCursor c = new ILCursor(il);

		if (c.TryGotoNext(
				x => x.MatchLdfld(typeof(HalcyoniteShrineInteractable), nameof(HalcyoniteShrineInteractable.goldDrained)),
				x => x.MatchConvR4(),
				x => x.MatchLdcR4(out _),
				x => x.MatchDiv()
			) &&
			c.TryGotoNext(MoveType.Before,
				x => x.MatchStloc(out _)
			))
		{
			c.Emit(OpCodes.Pop);
			c.Emit(OpCodes.Ldarg_0);
			c.EmitDelegate<Func<HalcyoniteShrineInteractable, int>>(AdjustHalcScaling);
		}
		else
		{
			Log.Error(il.Method.Name + " IL Hook failed!");
		}

		static int AdjustHalcScaling(HalcyoniteShrineInteractable self)
		{
			if (self.goldDrained > self.lowGoldCost && self.goldDrained < self.midGoldCost)
			{
				return (int)(0.7 + 0.06 * Run.instance.ambientLevel);
			}
			if (self.goldDrained > self.midGoldCost && self.goldDrained < self.maxGoldCost)
			{
				return (int)(1.4 + 0.12 * Run.instance.ambientLevel);
			}
			if (self.goldDrained >= self.maxGoldCost)
			{
				return (int)(2.1 + 0.18 * Run.instance.ambientLevel);
			}
			return 0;
		}
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

	static void OnBodyStart(On.RoR2.CharacterMaster.orig_OnBodyStart orig, CharacterMaster self, CharacterBody body)
	{
		orig(self, body);
		if (body.name == "HalcyoniteBody(Clone)")
		{
			body.modelLocator.modelTransform.GetChild(4).localScale = new Vector3(2.5f, 6f, 12f); //poke
			body.modelLocator.modelTransform.GetChild(7).localScale = new Vector3(15f, 0.8f, 10f); //swipe
			body.baseMoveSpeed = 9; // 6.6
			body.baseNameToken = "Halcyon Knight";
			body.subtitleNameToken = "Forsaken Heir";
			if (body.TryGetComponent<SetStateOnHurt>(out SetStateOnHurt setStateOnHurt))
			{
				setStateOnHurt.canBeHitStunned = false;
			}
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
