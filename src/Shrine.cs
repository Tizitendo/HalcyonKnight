using System;
using HG;
using Logger;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.ContentManagement;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HalcyonKnight;

public static class Shrine
{
    public static SpawnCard halcshrineCard;

    [SystemInitializer]
    static void Init()
    {
		AssetAsyncReferenceManager<GameObject>.LoadAsset(new AssetReferenceT<GameObject>(RoR2_DLC2.ShrineHalcyonite_prefab)).Completed += (x) =>
		{
			BossGroup bossGroup = x.Result.EnsureComponent<BossGroup>();
		};

		AssetAsyncReferenceManager<GameObject>.LoadAsset(new AssetReferenceT<GameObject>(RoR2_DLC2.ShrineHalcyonite_Stage1_Variant_prefab)).Completed += (x) =>
		{
			BossGroup bossGroup = x.Result.EnsureComponent<BossGroup>();
		};

        IL.RoR2.HalcyoniteShrineInteractable.DrainConditionMet += DrainConditionMet;
		// On.RoR2.HalcyoniteShrineInteractable.CalculateCredits += HalcyoniteShrineInteractable_CalculateCredits;
		On.EntityStates.ShrineHalcyonite.ShrineHalcyoniteBaseState.ModifyVisuals += ShrineHalcyoniteBaseState_ModifyVisuals;
    }

    private static void ShrineHalcyoniteBaseState_ModifyVisuals(On.EntityStates.ShrineHalcyonite.ShrineHalcyoniteBaseState.orig_ModifyVisuals orig, EntityStates.ShrineHalcyonite.ShrineHalcyoniteBaseState self)
    {
        orig(self);
		if (self.visualsDone)
		{
			self.parentShrineReference.NetworkgoldMaterialModifier = 10;
		}
    }

    private static void HalcyoniteShrineInteractable_CalculateCredits(On.RoR2.HalcyoniteShrineInteractable.orig_CalculateCredits orig, HalcyoniteShrineInteractable self)
	{
		orig(self);
		if (self.scaleMonsterCreditWithDifficultyCoefficient)
		{
			self.monsterCredit /= Math.Max(Run.instance.difficultyCoefficient * 0.3f, 1);
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
