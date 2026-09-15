using System;
using System.Collections.Generic;
using HG;
using Logger;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using R2API.Utils;
using RoR2;
using RoR2.ContentManagement;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace HalcyonKnight;

public static class Shrine
{
    public static SpawnCard halcshrineCard;

    [SystemInitializer]
    static void Init()
    {
		AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_DLC2.ShrineHalcyonite_prefab)).Completed += (x) =>
		{
			GameObject shrine = x.Result;
			BossGroup bossGroup = shrine.EnsureComponent<BossGroup>();
			if (shrine.TryGetComponent(out HalcyoniteShrineInteractable halcyoniteShrineInteractable))
			{
				halcyoniteShrineInteractable.goldDrainValue = 2;
				CombatDirector director = halcyoniteShrineInteractable.activationDirector;
				if (director)
				{
					halcyoniteShrineInteractable.activationDirector.enabled = true;
					halcyoniteShrineInteractable.activationDirector.shouldSpawnOneWave = false;
					halcyoniteShrineInteractable.activationDirector.ignoreTeamSizeLimit = false;
				}
				CombatDirector bossdirector = halcyoniteShrineInteractable.combatDirector;
				if (bossdirector)
				{
					bossdirector.goldRewardCoefficient = 0;
				}
			}
		};

        IL.RoR2.HalcyoniteShrineInteractable.DrainConditionMet += DrainConditionMet;
		On.RoR2.PurchaseInteraction.OnTeleporterBeginCharging += OnTeleporterBeginCharging;
		On.RoR2.HalcyoniteShrineInteractable.CalculateCredits += HalcyoniteShrineInteractable_CalculateCredits;
		On.EntityStates.ShrineHalcyonite.ShrineHalcyoniteNoQuality.OnEnter += ShrineHalcyoniteNoQuality_OnEnter;
		On.RoR2.HalcyoniteShrineInteractable.DestroyDrainVFX += HalcyoniteShrineInteractable_DestroyDrainVFX;
		On.EntityStates.ShrineHalcyonite.ShrineHalcyoniteFinished.OnEnter += ShrineHalcyoniteFinished_OnEnter;
		IL.RoR2.GoldSiphonNearbyBodyController.DrainGold += IL_GoldSiphonNearbyBodyController_DrainGold;
		On.EntityStates.ShrineHalcyonite.ShrineHalcyoniteBaseState.ModifyVisuals += ShrineHalcyoniteBaseState_ModifyVisuals;
		On.EntityStates.ShrineHalcyonite.ShrineHalcyoniteBaseState.OnEnter += ShrineHalcyoniteBaseState;

        OptionChangeShrineCredits(null, null);
		HalcyonKnight.ChangeShrineCredits.SettingChanged += OptionChangeShrineCredits;
    }

    private static void IL_GoldSiphonNearbyBodyController_DrainGold(ILContext il)
    {
        ILCursor c = new(il);
		int list2Loc = 0;

		if (c.TryGotoNext(
			x => x.MatchStfld(typeof(GoldSiphonNearbyBodyController), nameof(GoldSiphonNearbyBodyController.isTetheredToAtLeastOneObject))
		) &&
		c.TryGotoPrev(MoveType.Before,
			x => x.MatchLdloc(out list2Loc),
			x => x.MatchCallOrCallvirt(typeof(List<Transform>).GetPropertyGetter(nameof(List<Transform>.Count)))
		))
		{
			c.Emit(OpCodes.Ldarg_0);
			c.Emit(OpCodes.Ldloc, list2Loc);
			c.EmitDelegate<Action<GoldSiphonNearbyBodyController, List<Transform>>>(CreditDrain);
		} else
		{
			Log.Error(il.Method.Name + " IL Hook failed!");
		}

		static void CreditDrain(GoldSiphonNearbyBodyController self, List<Transform> list2)
		{
			if (self.sphereSearch == null || !self.parentShrineReference || !self.stateMachine || !self.transform || !self.purchaseInteraction)
				return;
			if (!self.parentShrineReference.activationDirector.enabled)
				return;
			List<HurtBox> list = CollectionPool<HurtBox, List<HurtBox>>.RentCollection();
			self.SearchForPlayers(list);
			for (int i = 0; i < list.Count; i++)
			{
				HurtBox hurtBox = list[i];
				bool isPlayerControlled = hurtBox.healthComponent.body.isPlayerControlled;
				if (hurtBox && hurtBox.healthComponent && hurtBox.healthComponent.alive && hurtBox.healthComponent.body.master.money <= self.parentShrineReference.goldDrainValue && isPlayerControlled)
				{
					self.parentShrineReference.activationDirector.monsterCredit += 0.2f;

					Transform transform = hurtBox.healthComponent.body?.coreTransform ?? hurtBox.transform;
					list2.Add(transform);
					for (int j = 0; j < self.tetheredGameObjects.Count; j++)
					{
						if (self.tetheredGameObjects[j].tetheredPlayer != null && transform.gameObject == self.tetheredGameObjects[j].tetheredPlayer)
						{
							self.tetheredGameObjects[j].tetheredPlayer = transform.gameObject;
							self.tetheredGameObjects[j].isTethered = true;
						}
					}
				}
				if (list2.Count >= self.maxTargets)
				{
					break;
				}
			}
			CollectionPool<HurtBox, List<HurtBox>>.ReturnCollection(list);
		}
    }

    private static void ShrineHalcyoniteBaseState(On.EntityStates.ShrineHalcyonite.ShrineHalcyoniteBaseState.orig_OnEnter orig, EntityStates.ShrineHalcyonite.ShrineHalcyoniteBaseState self)
    {
		if (self.parentShrineReference)
		{
			self.parentShrineReference.activationDirector.monsterCredit = 0;
		}
		
        orig(self);
		while(self.parentShrineReference.activationDirector.monsterCredit >= 100)
		{
			self.parentShrineReference.activationDirector.monsterCredit /= 2;
			self.parentShrineReference.activationDirector.SpendAllCreditsOnMapSpawns(self.parentShrineReference.gameObject.transform);
		}
    }

    private static void ShrineHalcyoniteBaseState_ModifyVisuals(On.EntityStates.ShrineHalcyonite.ShrineHalcyoniteBaseState.orig_ModifyVisuals orig, EntityStates.ShrineHalcyonite.ShrineHalcyoniteBaseState self)
    {
        orig(self);
		if (self.visualsDone)
		{
			self.parentShrineReference.NetworkgoldMaterialModifier = 10;
		}
    }

    private static void ShrineHalcyoniteNoQuality_OnEnter(On.EntityStates.ShrineHalcyonite.ShrineHalcyoniteNoQuality.orig_OnEnter orig, EntityStates.ShrineHalcyonite.ShrineHalcyoniteNoQuality self)
    {
        orig(self);
		self.transform.Find("meshHalcyoniteShrineStorm").gameObject.SetActive(true);
		self.transform.Find("Particle System").gameObject.SetActive(true);
    }

    private static void HalcyoniteShrineInteractable_DestroyDrainVFX(On.RoR2.HalcyoniteShrineInteractable.orig_DestroyDrainVFX orig, HalcyoniteShrineInteractable self)
    {
        orig(self);
		self.transform.Find("meshHalcyoniteShrineStorm").gameObject.SetActive(false);
		self.transform.Find("Particle System").gameObject.SetActive(false);
    }

    private static void ShrineHalcyoniteFinished_OnEnter(On.EntityStates.ShrineHalcyonite.ShrineHalcyoniteFinished.orig_OnEnter orig, EntityStates.ShrineHalcyonite.ShrineHalcyoniteFinished self)
    {
        orig(self);
		self.transform.Find("meshHalcyoniteShrineStorm").gameObject.SetActive(false);
		self.transform.Find("Particle System").gameObject.SetActive(false);
    }

    private static void OptionChangeShrineCredits(object sender, EventArgs e)
	{
		if (HalcyonKnight.ChangeShrineCredits.Value)
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

    private static WeightedSelection<DirectorCard> GenerateInteractableCardSelection(On.RoR2.SceneDirector.orig_GenerateInteractableCardSelection orig, SceneDirector self)
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

    private static void HalcyoniteShrineInteractable_CalculateCredits(On.RoR2.HalcyoniteShrineInteractable.orig_CalculateCredits orig, HalcyoniteShrineInteractable self)
	{
		orig(self);
		if (self.scaleMonsterCreditWithDifficultyCoefficient)
		{
			self.monsterCredit /= Math.Max(Run.instance.difficultyCoefficient * 0.7f, 1);
		}
	}

	private static void OnTeleporterBeginCharging(On.RoR2.PurchaseInteraction.orig_OnTeleporterBeginCharging orig, TeleporterInteraction self)
	{
		if (!NetworkServer.active)
		{
			return;
		}
		foreach (PurchaseInteraction instances in InstanceTracker.GetInstancesList<PurchaseInteraction>())
		{
			if (instances.name == "ShrineHalcyonite(Clone)")
			{
				if (instances.TryGetComponent(out HalcyoniteShrineInteractable halcyoniteShrineInteractable))
				{
					halcyoniteShrineInteractable.activationDirector.monsterCredit = 0;
					halcyoniteShrineInteractable.activationDirector.enabled = false;
				}
			}
		}
		orig(self);
	}

	private static void DrainConditionMet(ILContext il)
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
}


[RequireComponent(typeof(TetherVfxOrigin))]
public class TetherOverride : MonoBehaviour
{
	public GameObject OverrideVFX;
	public GameObject DefaultVFX;

	public delegate void shouldOverrideDelegate(TetherVfxOrigin tetherVfxOrigin, int index, ref bool result);
	public static shouldOverrideDelegate shouldOverride;
	public List<bool> replacedVFX;

	public TetherVfxOrigin tetherVfxOrigin;

	[SystemInitializer]
	static void Init()
	{
		GameObject NoMoneySiphonVFX = AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_DLC2.GoldSiphonTetherVFX_prefab)).WaitForCompletion().InstantiateClone("NoMoneySiphonVFX");
		if (NoMoneySiphonVFX.TryGetComponent(out LineRenderer lineRenderer))
		{
			Color color = new(1, 0.1f, 0, 0);
			lineRenderer.startColor = color;
			lineRenderer.endColor = color;
			lineRenderer.widthMultiplier = 0.1f;
		}
		AssetAsyncReferenceManager<GameObject>.LoadAsset(new(RoR2_DLC2.ShrineHalcyonite_prefab)).Completed += (x) =>
		{
			Transform goldSiphonNearbyBodyAttachment = x.Result.transform.Find("GoldSiphonNearbyBodyAttachment");
			if (!goldSiphonNearbyBodyAttachment)
				return;

			TetherOverride tetherOverride = goldSiphonNearbyBodyAttachment.gameObject.AddComponent<TetherOverride>();
			tetherOverride.OverrideVFX = NoMoneySiphonVFX;

			TetherOverride.shouldOverride += (TetherVfxOrigin tetherVfxOrigin, int index, ref bool result) =>
			{
				if (!tetherVfxOrigin.TryGetComponent(out GoldSiphonNearbyBodyController goldSiphonNearby))
					return;
				if (!goldSiphonNearby.parentShrineReference.activationDirector.enabled)
					return;
				GameObject tetheredPlayer = goldSiphonNearby.tetheredGameObjects[index].tetheredPlayer;
				
				if (tetheredPlayer && tetheredPlayer.TryGetComponent(out HealthComponent healthComponent) && healthComponent.body &&
				healthComponent.body.master.money <= goldSiphonNearby.goldDrainValue && healthComponent.body.isPlayerControlled)
				{
					result = true;
				}
			};
		};

		On.RoR2.TetherVfxOrigin.AddTether += TetherVfxOrigin_AddTether;
		On.RoR2.TetherVfxOrigin.RemoveTetherAt += TetherVfxOrigin_RemoveTetherAt;
	}

    private static void TetherVfxOrigin_AddTether(On.RoR2.TetherVfxOrigin.orig_AddTether orig, TetherVfxOrigin self, Transform target)
    {
		
		if (self.TryGetComponent(out TetherOverride tetherOverride))
		{
			bool result = false;
			shouldOverride.Invoke(self, self.tetheredTransforms.Count, ref result);
			self.tetherPrefab = result ? tetherOverride.OverrideVFX : tetherOverride.DefaultVFX;
			orig(self, target);
			self.tetherPrefab = tetherOverride.DefaultVFX;
			tetherOverride.replacedVFX.Add(result);
		} else
		{
			orig(self, target);
		}
    }

	private static void TetherVfxOrigin_RemoveTetherAt(On.RoR2.TetherVfxOrigin.orig_RemoveTetherAt orig, TetherVfxOrigin self, int i)
    {
		orig(self, i);
        if (self.TryGetComponent(out TetherOverride tetherOverride))
		{
			tetherOverride.replacedVFX.RemoveAt(i);
		}
    }

    void Awake()
	{
		tetherVfxOrigin = GetComponent<TetherVfxOrigin>();
		DefaultVFX = tetherVfxOrigin.tetherPrefab;
	}

    void Update()
	{
		for(int i = 0; i < tetherVfxOrigin.tetherVfxs.Count; i++)
		{
			bool result = false;
			shouldOverride.Invoke(tetherVfxOrigin, i, ref result);
			ReplaceTetherAt(i, result);
		}
	}

	void ReplaceTetherAt(int index, bool replacement)
	{
		if (replacement == replacedVFX[index])
			return;
		replacedVFX[index] = replacement;
		TetherVfx tetherVfx = tetherVfxOrigin.tetherVfxs[index];
		if (tetherVfx)
		{
			tetherVfx.transform.SetParent(null);
			tetherVfx.Terminate();
		}

		if (tetherVfxOrigin.tetherPrefab)
		{
			GameObject newVFXPrefab = replacement ? OverrideVFX : DefaultVFX;
			tetherVfx = Instantiate(newVFXPrefab, transform).GetComponent<TetherVfx>();
			tetherVfx.tetherTargetTransform = tetherVfxOrigin.tetheredTransforms[index];
			tetherVfxOrigin.tetherVfxs[index] = tetherVfx;
		}
	}
}
