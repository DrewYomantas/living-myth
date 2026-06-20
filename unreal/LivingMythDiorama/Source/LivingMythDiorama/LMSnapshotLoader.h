#pragma once

#include "CoreMinimal.h"
#include "LMSnapshotTypes.h"

// One-way loader for the Living Myth snapshot bridge (schema v1). Parses JSON into FLMSnapshot and
// gates the schema major version. Never writes back to the Living Myth side.
class LIVINGMYTHDIORAMA_API FLMSnapshotLoader
{
public:
	// Supported schema MAJOR version. v1 is additive-only; unknown fields are ignored by the parser,
	// so any "1.x.y" is accepted. A different major (e.g. "2.0.0") is rejected.
	static constexpr int32 SupportedMajor = 1;

	// Reads + parses the file at AbsolutePath. Returns true on success.
	// On failure, OutError explains why (file missing, parse error, incompatible schema major).
	static bool LoadFromFile(const FString& AbsolutePath, FLMSnapshot& OutSnapshot, FString& OutError);

	// Parses an already-loaded JSON string. Same contract as LoadFromFile.
	static bool LoadFromString(const FString& Json, FLMSnapshot& OutSnapshot, FString& OutError);

private:
	static int32 ParseMajor(const FString& SchemaVersion);
};
