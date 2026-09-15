using System;
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

	public static void SetStunnable(EntityState self, bool stunnable)
	{
		if (self.TryGetComponent(out SetStateOnHurt setStateOnHurt))
		{
			if (self.TryGetComponent(out ExtraChanges extraChanges) && extraChanges.stunCooldown <= 0)
			{
				setStateOnHurt.canBeStunned = stunnable;
			}
		}
	}

	public static void SetFreezable(EntityState self, bool freezeable)
	{
		if (self.TryGetComponent(out SetStateOnHurt setStateOnHurt))
		{
			if (self.TryGetComponent(out ExtraChanges extraChanges) && extraChanges.freezeCooldown <= 0)
			{
				setStateOnHurt.canBeFrozen = freezeable;
			}
		}
	}
}

public class ExtraChanges : MonoBehaviour
{
	EntityStateMachine _weaponStateMachine;
	EntityStateMachine _bodyStateMachine;
	bool _wasStunned;
	bool _wasFrozen;
	public float stunCooldown;
	public float freezeCooldown;

	public const float maxStunCooldown = 5f;
	public const float maxFreezeCooldown = 5f;

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
		bool raycastHit = Physics.Raycast(new Ray(transform.position, Vector3.down), out _, 50f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);

		if (_weaponStateMachine.state is not WhirlwindWarmUp &&
		_weaponStateMachine.state is not WhirlWindPersuitCycle &&
		_weaponStateMachine.nextState is not WhirlwindWarmUp &&
		_weaponStateMachine.nextState is not WhirlWindPersuitCycle)
		{
			if (!raycastHit && _bodyStateMachine.CanInterruptState(InterruptPriority.Immobilize))
			{
				_weaponStateMachine.SetInterruptState(new WhirlwindWarmUp(), InterruptPriority.Immobilize);
			}
		}

		if (_bodyStateMachine.state is StunState)
		{
			_wasStunned = true;
		} else {
			if (_wasStunned) {
				stunCooldown = maxStunCooldown;
				Util.PlaySound("Stop_halcyonite_skill3_loop", gameObject);
				if (TryGetComponent(out SetStateOnHurt setStateOnHurt))
				{
					setStateOnHurt.canBeStunned = false;
				}
			}
			_wasStunned = false;
		}

		if (_bodyStateMachine.state is FrozenState)
		{
			_wasFrozen = true;
		} else {
			if (_wasFrozen) {
				freezeCooldown = maxFreezeCooldown;
				Util.PlaySound("Stop_halcyonite_skill3_loop", gameObject);
				if (TryGetComponent(out SetStateOnHurt setStateOnHurt))
				{
					setStateOnHurt.canBeFrozen = false;
				}
			}
			_wasFrozen = false;
		}

		if (raycastHit)
		{
			if (TryGetComponent(out SetStateOnHurt setStateOnHurt))
			{
				if(stunCooldown > 0 && stunCooldown - Time.fixedDeltaTime <= 0)
				{
					setStateOnHurt.canBeStunned = true;
				}
				stunCooldown -= Time.fixedDeltaTime;
				if(freezeCooldown > 0 && freezeCooldown - Time.fixedDeltaTime <= 0)
				{
					setStateOnHurt.canBeFrozen = true;
				}
				freezeCooldown -= Time.fixedDeltaTime;
			}
		}
	}
}