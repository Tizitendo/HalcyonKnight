using System;
using Logger;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.ContentManagement;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace HalcyonKnight;

public static class Shrine
{
    static SpawnCard halcshrineCard;

    [SystemInitializer]
    static void Init()
    {
        IL.RoR2.HalcyoniteShrineInteractable.DrainConditionMet += DrainConditionMet;
		On.RoR2.PurchaseInteraction.OnTeleporterBeginCharging += OnTeleporterBeginCharging;
		On.RoR2.HalcyoniteShrineInteractable.CalculateCredits += HalcyoniteShrineInteractable_CalculateCredits;

        OptionChangeShrineCredits(null, null);
		HalcyonKnight.ChangeShrineCredits.SettingChanged += OptionChangeShrineCredits;

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
			self.monsterCredit /= Math.Max(Run.instance.difficultyCoefficient * 0.3f, 1);
		}
	}

	private static void OnTeleporterBeginCharging(On.RoR2.PurchaseInteraction.orig_OnTeleporterBeginCharging orig, TeleporterInteraction self)
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
