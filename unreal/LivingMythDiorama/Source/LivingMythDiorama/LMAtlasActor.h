#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "LMSnapshotTypes.h"
#include "LMAtlasActor.generated.h"

// Import Smoke V1: drops a Living Myth snapshot into the level as debug-drawn geometry.
// Regions/sites lay out by normalized x/y; memory markers render anchored per markerKind, honoring
// the RegionId vs HomeRegionId channel split. Debug-draw is deliberate — it proves the DATA lays out
// honestly with zero art commitment; swap the Draw* helpers for real meshes in a later fidelity pass.
UCLASS()
class LIVINGMYTHDIORAMA_API ALMAtlasActor : public AActor
{
	GENERATED_BODY()

public:
	ALMAtlasActor();

	// Path to a bridge JSON snapshot. Relative paths resolve against the project dir.
	// Defaults to the committed reference sample copied into Content/.
	UPROPERTY(EditAnywhere, Category = "Living Myth")
	FString SnapshotPath = TEXT("Content/Snapshots/reference_seed1_year250.json");

	// Normalized [0,1] map coords are multiplied by this to reach world units.
	UPROPERTY(EditAnywhere, Category = "Living Myth")
	float WorldScale = 20000.f;

	// Height of a memory-marker pin (world units).
	UPROPERTY(EditAnywhere, Category = "Living Myth")
	float MarkerHeight = 400.f;

	// If >= 0, only this region (its sites + markers anchored to it) is drawn — the DoD's
	// "render one honest region". -1 draws the whole atlas.
	UPROPERTY(EditAnywhere, Category = "Living Myth")
	int32 FocusRegionFilter = -1;

	// Builds the atlas from SnapshotPath. Clears any prior debug draw first.
	// BlueprintCallable so the editor Python console can drive it (tools/ue_smoke.py).
	UFUNCTION(CallInEditor, BlueprintCallable, Category = "Living Myth")
	void BuildFromSnapshot();

	UFUNCTION(CallInEditor, BlueprintCallable, Category = "Living Myth")
	void ClearAtlas();

	virtual void BeginPlay() override;

private:
	FVector MapToWorld(float Nx, float Ny, float Z) const;
	void DrawAtlas(const FLMSnapshot& Snap);
	bool PassesFilter(int32 RegionId) const;

	// region id -> world center, built from regions[]; the only place markers/sites resolve a location.
	TMap<int32, FVector> RegionCenters;
};
