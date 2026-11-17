namespace CsToBeamNG.Converter.BeamNG.Install;

/// <summary>The outcome of <see cref="ModInstaller.Install"/>.</summary>
/// <param name="ModDir">The mod folder.</param>
/// <param name="FileCount">The number of files written, not counting the file list.</param>
/// <param name="ByteCount">Their total size in bytes.</param>
/// <param name="ReplacedFileCount">How many of them replaced an existing file.</param>
/// <param name="RemovedFileCount">The number of files of the previous install that are no longer produced and were deleted.</param>
public sealed record InstallResult(string ModDir, int FileCount, long ByteCount, int ReplacedFileCount, int RemovedFileCount);
