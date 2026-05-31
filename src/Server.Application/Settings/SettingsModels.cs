namespace Server.Application.Settings;

public record AppMetaDto(string DbPath, string Version, string AppName, string Repository, string License);

public record DatabaseInfoDto(string Path, bool Exists);

public record DatabaseUpdateDto(string Message, string Path, bool Exists);

public record RefreshResultDto(string Message, string DbPath);

public record AboutDto(string Name, string Description, string Version, string License, string Repository, string Author, bool OpenSource);
