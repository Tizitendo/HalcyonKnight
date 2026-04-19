using EntityStates;
using EntityStates.Halcyonite;
using HG;
using RoR2;
using RoR2.ContentManagement;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace HalcyonKnight;

static class AntiFallOff
{
	[SystemInitializer]
	static void Init()
	{
		AssetReferenceT<GameObject> obj = new(RoR2_DLC2_Halcyonite.HalcyoniteBody_prefab);
		AssetAsyncReferenceManager<GameObject>.LoadAsset(obj).Completed += (x) =>
		{
			x.Result.EnsureComponent<ForceWhirlWindState>();
		};

		On.EntityStates.Halcyonite.WhirlWindPersuitCycle.OnEnter += WhirlWindPersuitCycle_OnEnter;
		On.EntityStates.Halcyonite.WhirlWindPersuitCycle.OnExit += WhirlWindPersuitCycle_OnExit;
		On.EntityStates.Halcyonite.WhirlwindWarmUp.OnEnter += WhirlWindPersuitCycle_OnEnter;
		On.EntityStates.Halcyonite.WhirlwindWarmUp.OnExit += WhirlWindPersuitCycle_OnExit;
		On.EntityStates.Halcyonite.WhirlWindPersuitCycle.UpdateLand += WhirlWindPersuitCycle_UpdateLand;
	}

	static void WhirlWindPersuitCycle_UpdateLand(On.EntityStates.Halcyonite.WhirlWindPersuitCycle.orig_UpdateLand orig, EntityStates.Halcyonite.WhirlWindPersuitCycle self)
	{
		orig(self);
		if (!Physics.Raycast(new Ray(self.transform.position, Vector3.down), out _, 200f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
		{
			self.outer.SetNextState(new EntityStates.Halcyonite.WhirlwindWarmUp());
		}
	}

	static void WhirlWindPersuitCycle_OnExit(On.EntityStates.Halcyonite.WhirlwindWarmUp.orig_OnExit orig, EntityStates.Halcyonite.WhirlwindWarmUp self)
	{
		orig(self);
		SetStunnable(self, true);
	}

	static void WhirlWindPersuitCycle_OnEnter(On.EntityStates.Halcyonite.WhirlwindWarmUp.orig_OnEnter orig, EntityStates.Halcyonite.WhirlwindWarmUp self)
	{
		orig(self);
		SetStunnable(self, false);
	}

	static void WhirlWindPersuitCycle_OnExit(On.EntityStates.Halcyonite.WhirlWindPersuitCycle.orig_OnExit orig, EntityStates.Halcyonite.WhirlWindPersuitCycle self)
	{
		orig(self);
		SetStunnable(self, true);
	}

	static void WhirlWindPersuitCycle_OnEnter(On.EntityStates.Halcyonite.WhirlWindPersuitCycle.orig_OnEnter orig, EntityStates.Halcyonite.WhirlWindPersuitCycle self)
	{
		orig(self);
		SetStunnable(self, false);
	}

	static void SetStunnable(EntityState self, bool stunnable)
	{
		if (self.TryGetComponent<SetStateOnHurt>(out SetStateOnHurt setStateOnHurt))
		{
			setStateOnHurt.canBeHitStunned = stunnable;
			setStateOnHurt.canBeStunned = stunnable;
		}
	}
}

public class ForceWhirlWindState : MonoBehaviour
{
	EntityStateMachine stateMachine;

	void Awake()
	{
		foreach(EntityStateMachine entityStateMachine in GetComponents<EntityStateMachine>())
		{
			if (entityStateMachine.customName == "Weapon")
			{
				stateMachine = entityStateMachine;
			}
		}
	}

	void FixedUpdate()
	{
		if (!stateMachine)
			return;
		if (stateMachine.state is not WhirlwindWarmUp &&
		stateMachine.state is not WhirlWindPersuitCycle &&
		stateMachine.nextState is not WhirlwindWarmUp &&
		stateMachine.nextState is not WhirlWindPersuitCycle)
		{
			if (!Physics.Raycast(new Ray(transform.position, Vector3.down), out _, 200f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
			{
				stateMachine.SetInterruptState(new EntityStates.Halcyonite.WhirlwindWarmUp(), InterruptPriority.Immobilize);
			}
		}
	}
}