using UnrealBuildTool;
using System.Collections.Generic;

public class LivingMythDioramaEditorTarget : TargetRules
{
	public LivingMythDioramaEditorTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Editor;
		DefaultBuildSettings = BuildSettingsVersion.V5;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		// Installed engine: allow this target's own build environment so V5's stricter warning-as-error
		// defaults don't conflict with the precompiled UnrealEditor's shared build products.
		bOverrideBuildEnvironment = true;
		ExtraModuleNames.Add("LivingMythDiorama");
	}
}
