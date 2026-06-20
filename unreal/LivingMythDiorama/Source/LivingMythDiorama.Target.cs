using UnrealBuildTool;
using System.Collections.Generic;

public class LivingMythDioramaTarget : TargetRules
{
	public LivingMythDioramaTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Game;
		DefaultBuildSettings = BuildSettingsVersion.V5;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		// Installed engine: allow this target's own build environment so V5's stricter warning-as-error
		// defaults don't conflict with the precompiled engine's shared build products.
		bOverrideBuildEnvironment = true;
		ExtraModuleNames.Add("LivingMythDiorama");
	}
}
