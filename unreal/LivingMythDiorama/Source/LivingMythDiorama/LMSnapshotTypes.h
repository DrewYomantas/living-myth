#pragma once

#include "CoreMinimal.h"
#include "LMSnapshotTypes.generated.h"

// USTRUCT mirror of the Living Myth Unreal Snapshot Bridge schema v1.0.0.
// Source of truth: docs/UNREAL_SNAPSHOT_BRIDGE.md + UnrealSnapshot.cs in the Living Myth repo.
//
// Field-name mapping: the bridge emits camelCase JSON ("controllingFactionId"); UE properties are
// PascalCase. FJsonObjectConverter matches names case-insensitively, so they bind directly.
//
// Nullable convention (load-bearing — see the honesty rules in the bridge doc):
//   * Nullable STRING ids (faction ids, colours) -> FString, empty ("") means JSON null / absent.
//   * Nullable INT ids (region/home/person/seat) -> int32 defaulted to LM::NullId (-1). A JSON null
//     leaves the default untouched, so -1 == "not set". Region/site ids are >= 0, so -1 never collides.
// The RegionId vs HomeRegionId channels are preserved verbatim and must NOT be conflated downstream.

namespace LM { static constexpr int32 NullId = -1; }

USTRUCT()
struct FLMCounts
{
	GENERATED_BODY()

	UPROPERTY() int32 Regions = 0;
	UPROPERTY() int32 Factions = 0;
	UPROPERTY() int32 Sites = 0;
	UPROPERTY() int32 PeopleAlive = 0;
	UPROPERTY() int32 PeopleEver = 0;
	UPROPERTY() int32 Events = 0;
	UPROPERTY() int32 MemoryMarkers = 0;
	UPROPERTY() int32 ChronicleBeats = 0;
};

USTRUCT()
struct FLMRegion
{
	GENERATED_BODY()

	UPROPERTY() int32 Id = LM::NullId;
	UPROPERTY() FString Name;                  // nullable -> "" when absent
	UPROPERTY() FString Terrain;               // coast | forest | highland | plains
	UPROPERTY() float X = 0.f;                 // normalized [0,1]
	UPROPERTY() float Y = 0.f;                 // normalized [0,1]
	UPROPERTY() FString ControllingFactionId;  // nullable faction id; "" == unheld
	UPROPERTY() int32 HomeMemoryCount = 0;
	UPROPERTY() int32 TrueEventCount = 0;
	UPROPERTY() FString SuggestedUnrealRole;   // settlement|forest|highland|coast|grassland|ruin_or_sacred|unknown
};

USTRUCT()
struct FLMFaction
{
	GENERATED_BODY()

	UPROPERTY() FString Id;
	UPROPERTY() FString Name;
	UPROPERTY() FString Color;          // null in v1 (sim authors no colour)
	UPROPERTY() FString SymbolicColor;  // "#RRGGBB" derived render hint
	UPROPERTY() int32 SeatRegionId = LM::NullId;
	UPROPERTY() int32 Prosperity = 0;
	UPROPERTY() int32 LeaderPersonId = LM::NullId;
	UPROPERTY() TArray<FString> Traits;
};

USTRUCT()
struct FLMSite
{
	GENERATED_BODY()

	UPROPERTY() int32 Id = LM::NullId;
	UPROPERTY() int32 RegionId = LM::NullId;
	UPROPERTY() FString Name;
	UPROPERTY() FString Type;        // SacredGrove, HillFort, MarketVillage, ...
	UPROPERTY() FString TypeLabel;
	UPROPERTY() bool IsSeat = false;
	UPROPERTY() float X = 0.f;
	UPROPERTY() float Y = 0.f;
	UPROPERTY() FString DisplayRole; // market|dock|fortification|sacred|ruin|ford|farm|camp
};

USTRUCT()
struct FLMPerson
{
	GENERATED_BODY()

	UPROPERTY() int32 Id = LM::NullId;
	UPROPERTY() FString Name;
	UPROPERTY() FString FactionId;
	UPROPERTY() int32 HomeRegionId = LM::NullId;
	UPROPERTY() int32 CurrentRegionId = LM::NullId; // always null in v1 (not modeled)
	UPROPERTY() TArray<FString> RoleTags;           // leader | prophet
	UPROPERTY() bool Alive = false;
	UPROPERTY() int32 BirthYear = 0;
	UPROPERTY() int32 DeathYear = LM::NullId;
	UPROPERTY() int32 Age = 0;
};

USTRUCT()
struct FLMMarker
{
	GENERATED_BODY()

	UPROPERTY() int32 EventId = LM::NullId;
	UPROPERTY() int32 Year = 0;
	UPROPERTY() FString Type;
	UPROPERTY() int32 RegionId = LM::NullId;     // where it happened (true place anchor)
	UPROPERTY() int32 HomeRegionId = LM::NullId; // where it is remembered (lineage home root)
	UPROPERTY() FString MarkerKind;              // chronicle_beat|home_memory_cairn|faction_pulse|true_place_mark
	UPROPERTY() FString Label;
	UPROPERTY() FString Description;
	UPROPERTY() TArray<FString> InvolvedFactionIds;
	UPROPERTY() TArray<int32> InvolvedPersonIds;
};

USTRUCT()
struct FLMBeat
{
	GENERATED_BODY()

	UPROPERTY() int32 BeatIndex = 0;
	UPROPERTY() int32 EventId = LM::NullId;
	UPROPERTY() int32 Year = 0;
	UPROPERTY() FString Type;
	UPROPERTY() int32 RegionId = LM::NullId;
	UPROPERTY() int32 HomeRegionId = LM::NullId;
	UPROPERTY() FString Label;
	UPROPERTY() FString CausalHint;
};

USTRUCT()
struct FLMBounds
{
	GENERATED_BODY()

	UPROPERTY() float MinX = 0.f;
	UPROPERTY() float MinY = 0.f;
	UPROPERTY() float MaxX = 1.f;
	UPROPERTY() float MaxY = 1.f;
};

USTRUCT()
struct FLMCameraHints
{
	GENERATED_BODY()

	UPROPERTY() FString PreferredMode;            // "atlas"
	UPROPERTY() int32 RegionFocusId = LM::NullId;
	UPROPERTY() FLMBounds Bounds;
};

USTRUCT()
struct FLMSnapshot
{
	GENERATED_BODY()

	UPROPERTY() FString SchemaVersion;
	UPROPERTY() FString GeneratedBy;
	UPROPERTY() int32 Seed = 0;
	UPROPERTY() int32 Year = 0;
	UPROPERTY() FString WorldName;
	UPROPERTY() FLMCounts Counts;
	UPROPERTY() TArray<FLMRegion> Regions;
	UPROPERTY() TArray<FLMFaction> Factions;
	UPROPERTY() TArray<FLMSite> Sites;
	UPROPERTY() TArray<FLMPerson> PeopleHighlights;
	UPROPERTY() TArray<FLMMarker> MemoryMarkers;
	UPROPERTY() TArray<FLMBeat> ChroniclePath;
	UPROPERTY() FLMCameraHints CameraHints;
	UPROPERTY() TArray<FString> ExportWarnings;
};
