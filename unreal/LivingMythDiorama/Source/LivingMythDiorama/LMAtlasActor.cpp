#include "LMAtlasActor.h"
#include "LivingMythDiorama.h"
#include "LMSnapshotLoader.h"
#include "DrawDebugHelpers.h"
#include "Misc/Paths.h"
#include "Misc/FileHelper.h"

namespace
{
	FColor RoleColor(const FString& Role)
	{
		if (Role == TEXT("settlement"))     return FColor(214, 178, 92);   // gold
		if (Role == TEXT("forest"))         return FColor(46, 120, 60);    // green
		if (Role == TEXT("highland"))       return FColor(130, 130, 140);  // grey
		if (Role == TEXT("coast"))          return FColor(70, 130, 180);   // steel blue
		if (Role == TEXT("grassland"))      return FColor(120, 170, 80);   // light green
		if (Role == TEXT("ruin_or_sacred")) return FColor(150, 110, 180);  // violet
		return FColor(200, 200, 200);                                      // unknown
	}

	FColor DisplayRoleColor(const FString& Role)
	{
		if (Role == TEXT("market"))        return FColor(230, 150, 40);
		if (Role == TEXT("dock"))          return FColor(60, 200, 210);
		if (Role == TEXT("fortification")) return FColor(200, 60, 60);
		if (Role == TEXT("sacred"))        return FColor(170, 120, 200);
		if (Role == TEXT("ruin"))          return FColor(120, 120, 120);
		if (Role == TEXT("ford"))          return FColor(120, 180, 230);
		if (Role == TEXT("farm"))          return FColor(190, 200, 90);
		if (Role == TEXT("camp"))          return FColor(150, 110, 70);
		return FColor::White;
	}

	// The marker palette mirrors the channel split: cairns read as remembrance, place marks as events.
	FColor MarkerKindColor(const FString& Kind)
	{
		if (Kind == TEXT("chronicle_beat"))     return FColor(255, 215, 0);    // gold — the spine
		if (Kind == TEXT("home_memory_cairn"))  return FColor(220, 220, 230);  // pale stone — memory
		if (Kind == TEXT("faction_pulse"))      return FColor(235, 140, 40);   // ember — land fortune
		if (Kind == TEXT("true_place_mark"))    return FColor(210, 70, 70);    // red — a true place event
		return FColor::White;
	}
}

ALMAtlasActor::ALMAtlasActor()
{
	PrimaryActorTick.bCanEverTick = false;
}

void ALMAtlasActor::BeginPlay()
{
	Super::BeginPlay();
	BuildFromSnapshot();
}

FVector ALMAtlasActor::MapToWorld(float Nx, float Ny, float Z) const
{
	// Map space is [0,1]^2; place it flat on XY around this actor's origin, centered on 0.5,0.5.
	const FVector Origin = GetActorLocation();
	return FVector(Origin.X + (Nx - 0.5f) * WorldScale,
	               Origin.Y + (Ny - 0.5f) * WorldScale,
	               Origin.Z + Z);
}

bool ALMAtlasActor::PassesFilter(int32 RegionId) const
{
	return FocusRegionFilter < 0 || RegionId == FocusRegionFilter;
}

void ALMAtlasActor::ClearAtlas()
{
	if (UWorld* W = GetWorld())
	{
		FlushPersistentDebugLines(W);
	}
	RegionCenters.Reset();
}

void ALMAtlasActor::BuildFromSnapshot()
{
	ClearAtlas();

	FString Path = SnapshotPath;
	if (FPaths::IsRelative(Path))
	{
		Path = FPaths::Combine(FPaths::ProjectDir(), Path);
	}

	FLMSnapshot Snap;
	FString Error;
	if (!FLMSnapshotLoader::LoadFromFile(Path, Snap, Error))
	{
		UE_LOG(LogLivingMyth, Error, TEXT("BuildFromSnapshot failed: %s"), *Error);
		return;
	}

	DrawAtlas(Snap);
}

void ALMAtlasActor::DrawAtlas(const FLMSnapshot& Snap)
{
	UWorld* W = GetWorld();
	if (!W) { return; }

	const bool bPersistent = true;
	const float Lifetime = -1.f;

	// --- Regions: flat tiles colored by suggestedUnrealRole. Also builds the id -> center lookup. ---
	for (const FLMRegion& R : Snap.Regions)
	{
		const FVector Center = MapToWorld(R.X, R.Y, 0.f);
		RegionCenters.Add(R.Id, Center); // every region indexed, even if filtered from drawing
		if (!PassesFilter(R.Id)) { continue; }

		const FColor Col = RoleColor(R.SuggestedUnrealRole);
		DrawDebugBox(W, Center, FVector(WorldScale * 0.02f, WorldScale * 0.02f, 20.f),
		             Col, bPersistent, Lifetime, 0, 8.f);
		const FString Label = R.Name.IsEmpty() ? FString::Printf(TEXT("region %d"), R.Id) : R.Name;
		DrawDebugString(W, Center + FVector(0, 0, 60), Label, nullptr, Col, Lifetime);
	}

	// --- Sites: raised pins colored by displayRole. Skipped if their region is filtered out. ---
	for (const FLMSite& S : Snap.Sites)
	{
		if (!PassesFilter(S.RegionId)) { continue; }
		const FVector Loc = MapToWorld(S.X, S.Y, 120.f);
		const FColor Col = DisplayRoleColor(S.DisplayRole);
		DrawDebugSphere(W, Loc, S.IsSeat ? 90.f : 50.f, 8, Col, bPersistent, Lifetime, 0, 4.f);
	}

	// --- Memory markers: the load-bearing honesty test. Anchor by markerKind, never conflate channels. ---
	int32 Placed = 0, Unplaceable = 0, Violations = 0;
	for (const FLMMarker& M : Snap.MemoryMarkers)
	{
		int32 AnchorRegion = LM::NullId;

		if (M.MarkerKind == TEXT("home_memory_cairn"))
		{
			// Contract: a cairn is a remembered HOME, never an in-place event. The feed must NOT carry
			// a regionId here. If it does, the bridge broke its own honesty rule — surface it loudly.
			if (M.RegionId != LM::NullId)
			{
				++Violations;
				UE_LOG(LogLivingMyth, Error,
					TEXT("HONESTY VIOLATION: home_memory_cairn (event %d, '%s') carries regionId %d — refusing to render as a place mark."),
					M.EventId, *M.Label, M.RegionId);
				continue;
			}
			AnchorRegion = M.HomeRegionId; // remembrance anchors at the lineage home
		}
		else
		{
			// chronicle_beat / faction_pulse / true_place_mark anchor at the true place.
			AnchorRegion = M.RegionId;
		}

		const FVector* Center = (AnchorRegion != LM::NullId) ? RegionCenters.Find(AnchorRegion) : nullptr;
		if (!Center || !PassesFilter(AnchorRegion))
		{
			// Honest absence: an unanchored event (e.g. a war with both ids null) is NOT invented onto
			// the map. It belongs in a dev overlay / side rail, never a fabricated pin.
			if (!Center) { ++Unplaceable; }
			continue;
		}

		const FColor Col = MarkerKindColor(M.MarkerKind);
		const FVector Base = *Center;
		const FVector Top = Base + FVector(0, 0, MarkerHeight);
		DrawDebugLine(W, Base, Top, Col, bPersistent, Lifetime, 0, 6.f);
		DrawDebugSphere(W, Top, 70.f, 8, Col, bPersistent, Lifetime, 0, 4.f);
		++Placed;
	}

	// --- Camera framing from cameraHints: focus region + atlas bounds. ---
	const FLMBounds& B = Snap.CameraHints.Bounds;
	const FVector Min = MapToWorld(B.MinX, B.MinY, 0.f);
	const FVector Max = MapToWorld(B.MaxX, B.MaxY, 0.f);
	const FVector BoundsCenter = (Min + Max) * 0.5f;
	const FVector Extent = (Max - Min) * 0.5f;
	DrawDebugBox(W, BoundsCenter, Extent.GetAbs() + FVector(0, 0, 50.f), FColor(255, 255, 255), bPersistent, Lifetime, 0, 3.f);

	if (const FVector* Focus = RegionCenters.Find(Snap.CameraHints.RegionFocusId))
	{
		DrawDebugSphere(W, *Focus + FVector(0, 0, MarkerHeight * 1.5f), 140.f, 12, FColor(0, 255, 255), bPersistent, Lifetime, 0, 5.f);
	}

	UE_LOG(LogLivingMyth, Log,
		TEXT("Atlas drawn: %d regions, %d sites, markers placed %d / unplaceable %d / honesty-violations %d. Focus region %d."),
		Snap.Regions.Num(), Snap.Sites.Num(), Placed, Unplaceable, Violations, Snap.CameraHints.RegionFocusId);

	// Write a machine-readable verdict so the smoke test can be confirmed off the editor (read by tools/ue_smoke.py's caller).
	const FString Verdict = FString::Printf(
		TEXT("{\"schemaVersion\":\"%s\",\"worldName\":\"%s\",\"seed\":%d,\"year\":%d,")
		TEXT("\"regions\":%d,\"sites\":%d,\"markersPlaced\":%d,\"markersUnplaceable\":%d,")
		TEXT("\"honestyViolations\":%d,\"focusRegionId\":%d,\"focusRegionFilter\":%d}"),
		*Snap.SchemaVersion, *Snap.WorldName, Snap.Seed, Snap.Year,
		Snap.Regions.Num(), Snap.Sites.Num(), Placed, Unplaceable, Violations,
		Snap.CameraHints.RegionFocusId, FocusRegionFilter);
	FFileHelper::SaveStringToFile(Verdict, *(FPaths::ProjectSavedDir() / TEXT("smoke_verdict.json")));
}
