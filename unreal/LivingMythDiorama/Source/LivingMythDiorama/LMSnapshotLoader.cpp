#include "LMSnapshotLoader.h"
#include "LivingMythDiorama.h"
#include "JsonObjectConverter.h"
#include "Misc/FileHelper.h"

int32 FLMSnapshotLoader::ParseMajor(const FString& SchemaVersion)
{
	FString Major = SchemaVersion;
	int32 Dot;
	if (SchemaVersion.FindChar('.', Dot))
	{
		Major = SchemaVersion.Left(Dot);
	}
	return FCString::IsNumeric(*Major) ? FCString::Atoi(*Major) : -1;
}

bool FLMSnapshotLoader::LoadFromFile(const FString& AbsolutePath, FLMSnapshot& OutSnapshot, FString& OutError)
{
	FString Json;
	if (!FFileHelper::LoadFileToString(Json, *AbsolutePath))
	{
		OutError = FString::Printf(TEXT("Could not read snapshot file: %s"), *AbsolutePath);
		return false;
	}
	return LoadFromString(Json, OutSnapshot, OutError);
}

bool FLMSnapshotLoader::LoadFromString(const FString& Json, FLMSnapshot& OutSnapshot, FString& OutError)
{
	OutSnapshot = FLMSnapshot();

	if (!FJsonObjectConverter::JsonObjectStringToUStruct<FLMSnapshot>(Json, &OutSnapshot, 0, 0))
	{
		OutError = TEXT("JSON failed to parse into FLMSnapshot (malformed JSON or unexpected shape).");
		return false;
	}

	if (OutSnapshot.SchemaVersion.IsEmpty())
	{
		OutError = TEXT("Snapshot has no schemaVersion — refusing to consume an unversioned feed.");
		return false;
	}

	const int32 Major = ParseMajor(OutSnapshot.SchemaVersion);
	if (Major != SupportedMajor)
	{
		OutError = FString::Printf(
			TEXT("Incompatible schema major: got '%s' (major %d), this consumer supports major %d."),
			*OutSnapshot.SchemaVersion, Major, SupportedMajor);
		return false;
	}

	UE_LOG(LogLivingMyth, Log,
		TEXT("Loaded snapshot v%s — world '%s', seed %d, year %d (%d regions / %d sites / %d markers / %d beats)."),
		*OutSnapshot.SchemaVersion, *OutSnapshot.WorldName, OutSnapshot.Seed, OutSnapshot.Year,
		OutSnapshot.Regions.Num(), OutSnapshot.Sites.Num(),
		OutSnapshot.MemoryMarkers.Num(), OutSnapshot.ChroniclePath.Num());

	for (const FString& Warning : OutSnapshot.ExportWarnings)
	{
		UE_LOG(LogLivingMyth, Warning, TEXT("exportWarning: %s"), *Warning);
	}

	return true;
}
