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
			x.Result.EnsureComponent<ExtraChanges>();
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
		if (!Physics.Raycast(new Ray(self.transform.position, Vector3.down), out _, 50f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
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
			if (self.TryGetComponent(out ExtraChanges extraChanges) && extraChanges.stunCooldown <= 0)
			{
				setStateOnHurt.canBeStunned = stunnable;
			}
		}
	}
}

public class ExtraChanges : MonoBehaviour
{
	EntityStateMachine _weaponStateMachine;
	EntityStateMachine _bodyStateMachine;
	bool _wasStunned;
	public float stunCooldown;

	public const float maxStunCooldown = 3f;

	void Awake()
	{
		foreach(EntityStateMachine entityStateMachine in GetComponents<EntityStateMachine>())
		{
			if (entityStateMachine.customName == "Weapon")
			{
				_weaponStateMachine = entityStateMachine;
			}
			if (entityStateMachine.customName == "Body")
			{
				_bodyStateMachine = entityStateMachine;
			}
		}
	}

	void FixedUpdate()
	{
		if (!_weaponStateMachine)
			return;
		if (_weaponStateMachine.state is not WhirlwindWarmUp &&
		_weaponStateMachine.state is not WhirlWindPersuitCycle &&
		_weaponStateMachine.nextState is not WhirlwindWarmUp &&
		_weaponStateMachine.nextState is not WhirlWindPersuitCycle)
		{
			if (!Physics.Raycast(new Ray(transform.position, Vector3.down), out _, 50f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore) &&
			_bodyStateMachine.CanInterruptState(InterruptPriority.Immobilize))
			{
				_weaponStateMachine.SetInterruptState(new EntityStates.Halcyonite.WhirlwindWarmUp(), InterruptPriority.Immobilize);
			}
		}

		if (_bodyStateMachine.state is StunState)
		{
			_wasStunned = true;
		} else {
			if (_wasStunned) {
				stunCooldown = maxStunCooldown;
				if (TryGetComponent<SetStateOnHurt>(out SetStateOnHurt setStateOnHurt))
				{
					setStateOnHurt.canBeStunned = false;
				}
			}
			_wasStunned = false;
		}

		if (_weaponStateMachine.state is not WhirlwindWarmUp &&
		_weaponStateMachine.state is not WhirlWindPersuitCycle &&
		_weaponStateMachine.nextState is not WhirlwindWarmUp &&
		_weaponStateMachine.nextState is not WhirlWindPersuitCycle)
		{
			if(stunCooldown > 0 && stunCooldown - Time.fixedDeltaTime <= 0)
			{
				if (TryGetComponent<SetStateOnHurt>(out SetStateOnHurt setStateOnHurt))
				{
					setStateOnHurt.canBeStunned = true;
				}
			}
			stunCooldown -= Time.fixedDeltaTime;
		}
	}
}