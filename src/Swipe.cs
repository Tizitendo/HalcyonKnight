using MonoDetour;
using MonoDetour.HookGen;
using Logger;
using RoR2;
using UnityEngine;
using RoR2.CharacterAI;
using System;

namespace HalcyonKnight;

[MonoDetourTargets(typeof(HalcyonFixes.FixedSlash))]
public class LibraryClassHooks
{
	[MonoDetourHookInitialize]
	static void Init()
	{
		Md.HalcyonFixes.FixedSlash.OnEnter.Postfix((self) =>
		{
			HitBoxGroup hitBoxGroup = self.FindHitBoxGroup(self.GetHitBoxGroupName());
			float footY = self.characterBody.footPosition.y;

			foreach (HitBox hitBox in hitBoxGroup.hitBoxes)
			{
				hitBox.transform.position = new Vector3(hitBox.transform.position.x, footY + 6f, hitBox.transform.position.z);
			}
		});

		Md.HalcyonFixes.FixedSlash.FixedUpdate.Postfix((self) =>
		{
			//if (self.fixedAge > 0.2f)
			//	return;
			//HitBoxGroup hitBoxGroup = self.FindHitBoxGroup(self.GetHitBoxGroupName());
			float? targetY = null;
			foreach (BaseAI baseAI in self.characterBody.master.AiComponents)
			{
				if (baseAI.hasAimTarget && baseAI.skillDriverEvaluation.target.characterBody.isPlayerControlled)
				{
					targetY = baseAI.skillDriverEvaluation.target.characterBody.footPosition.y + 1;
					break;
				}
			}
			if (targetY == null) {
				targetY = -float.MaxValue;
				return;
			}

			float footY = self.characterBody.footPosition.y;
			targetY = Mathf.Clamp(targetY.Value, footY - 2, footY + 4);
			
			foreach (HitBox hitBox in self.hitBoxGroup.hitBoxes)
			{
				float heightDiff = self.transform.position.y - self.characterBody.previousPosition.y;
				hitBox.transform.position = new Vector3(hitBox.transform.position.x, hitBox.transform.position.y - heightDiff, hitBox.transform.position.z);
				if (targetY < hitBox.transform.position.y)
				{
					hitBox.transform.position = new Vector3(hitBox.transform.position.x, targetY.Value, hitBox.transform.position.z);
				}
			}
		});
	}
}