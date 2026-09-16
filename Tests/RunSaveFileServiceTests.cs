using NUnit.Framework;
using System;
using System.IO;

[TestFixture]
public sealed class RunSaveFileServiceTests
{
	private const int Width = 24;
	private const int Height = 16;
	private const int ZoneCount = 5;

	private string _directory;

	[SetUp]
	public void SetUp()
	{
		_directory = Path.Combine(Path.GetTempPath(), "TurnDungeonSaveTests_" + Guid.NewGuid());
		Directory.CreateDirectory(_directory);
	}

	[TearDown]
	public void TearDown()
	{
		if (Directory.Exists(_directory))
			Directory.Delete(_directory, recursive: true);
	}

	[Test]
	public void HasSaveFile_FalseWhenNothingHasBeenSaved()
	{
		Assert.That(RunSaveFileService.HasSaveFile(_directory), Is.False);
	}

	[Test]
	public void Save_ThenLoad_RoundTripsTheEnvelope()
	{
		RunSaveEnvelope envelope = CreateEnvelope(seed: 7);

		RunSaveFileService.Save(_directory, envelope);
		SaveFileLoadOutcome outcome = RunSaveFileService.Load(_directory);

		Assert.That(RunSaveFileService.HasSaveFile(_directory), Is.True);
		Assert.That(outcome.Result, Is.EqualTo(SaveFileLoadResult.Loaded));
		Assert.That(outcome.Envelope.Seed, Is.EqualTo(7));
	}

	[Test]
	public void Load_WithNoSaveFileReturnsNoSaveFound()
	{
		SaveFileLoadOutcome outcome = RunSaveFileService.Load(_directory);

		Assert.That(outcome.Result, Is.EqualTo(SaveFileLoadResult.NoSaveFound));
		Assert.That(outcome.Envelope, Is.Null);
	}

	[Test]
	public void Save_RejectsNullEnvelope()
	{
		Action save = () => RunSaveFileService.Save(_directory, null);

		Assert.Throws<ArgumentNullException>(save);
	}

	[Test]
	public void SecondSave_RotatesTheFirstSaveIntoBackup()
	{
		RunSaveFileService.Save(_directory, CreateEnvelope(seed: 1));
		RunSaveFileService.Save(_directory, CreateEnvelope(seed: 2));

		string backupPath = Path.Combine(_directory, RunSaveFileService.BackupFileName);
		Assert.That(File.Exists(backupPath), Is.True);

		RunSaveLoadOutcome backup = RunSaveSerializer.FromJson(File.ReadAllText(backupPath));
		Assert.That(backup.Envelope.Seed, Is.EqualTo(1));

		SaveFileLoadOutcome current = RunSaveFileService.Load(_directory);
		Assert.That(current.Envelope.Seed, Is.EqualTo(2));
	}

	[Test]
	public void Load_FallsBackToBackupWhenCurrentSaveIsCorrupt()
	{
		RunSaveFileService.Save(_directory, CreateEnvelope(seed: 1));
		RunSaveFileService.Save(_directory, CreateEnvelope(seed: 2));

		string savePath = Path.Combine(_directory, RunSaveFileService.SaveFileName);
		File.WriteAllText(savePath, "not valid json {{{");

		SaveFileLoadOutcome outcome = RunSaveFileService.Load(_directory);

		Assert.That(outcome.Result, Is.EqualTo(SaveFileLoadResult.LoadedFromBackup));
		Assert.That(outcome.Envelope.Seed, Is.EqualTo(1));
		Assert.That(outcome.Error, Is.Not.Null.And.Not.Empty);
	}

	[Test]
	public void Load_ReturnsInvalidAndPreservesFileWhenCurrentAndBackupAreBothBad()
	{
		string savePath = Path.Combine(_directory, RunSaveFileService.SaveFileName);
		File.WriteAllText(savePath, "not valid json {{{");

		SaveFileLoadOutcome outcome = RunSaveFileService.Load(_directory);

		Assert.That(outcome.Result, Is.EqualTo(SaveFileLoadResult.Invalid));
		Assert.That(outcome.Envelope, Is.Null);
		Assert.That(outcome.Error, Is.Not.Null.And.Not.Empty);
		Assert.That(File.Exists(savePath), Is.True, "An invalid current save must not be deleted.");
	}

	[Test]
	public void Load_ReturnsInvalidWhenCurrentIsCorruptAndBackupIsAlsoCorrupt()
	{
		string savePath = Path.Combine(_directory, RunSaveFileService.SaveFileName);
		string backupPath = Path.Combine(_directory, RunSaveFileService.BackupFileName);
		File.WriteAllText(savePath, "not valid json {{{");
		File.WriteAllText(backupPath, "also not valid json {{{");

		SaveFileLoadOutcome outcome = RunSaveFileService.Load(_directory);

		Assert.That(outcome.Result, Is.EqualTo(SaveFileLoadResult.Invalid));
		Assert.That(File.Exists(savePath), Is.True);
		Assert.That(File.Exists(backupPath), Is.True);
	}

	private static RunSaveEnvelope CreateEnvelope(int seed)
	{
		DungeonMap map = new DungeonGenerator().Generate(
			new DungeonGenerationRequest(Width, Height, ZoneCount, seed)
		);
		ActorState player = new(new GridPosition(0, 0), maxHealth: 3, "core.player");
		GameState state = new(map, player);

		return RunSaveEnvelope.Capture(state);
	}
}
