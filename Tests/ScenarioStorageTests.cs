using NUnit.Framework;
using System;
using System.IO;

[TestFixture]
public sealed class ScenarioStorageTests
{
	private string _userDataDir;

	[SetUp]
	public void SetUp()
	{
		_userDataDir = Path.Combine(Path.GetTempPath(), "TurnDungeonScenarioTests_" + Guid.NewGuid());
		Directory.CreateDirectory(_userDataDir);
	}

	[TearDown]
	public void TearDown()
	{
		if (Directory.Exists(_userDataDir))
			Directory.Delete(_userDataDir, recursive: true);
	}

	[Test]
	public void DirectoryFor_NeverResolvesToTheUserDataDirItself()
	{
		string directory = ScenarioStorage.DirectoryFor(_userDataDir, "goblin-ambush");

		Assert.That(directory, Is.Not.EqualTo(_userDataDir));
		Assert.That(directory, Does.StartWith(ScenarioStorage.RootDirectory(_userDataDir)));
	}

	[Test]
	public void ListScenarios_EmptyWhenNoScenariosDirectoryExists()
	{
		Assert.That(ScenarioStorage.ListScenarios(_userDataDir), Is.Empty);
	}

	[Test]
	public void ListScenarios_OnlyIncludesDirectoriesWithASaveFile()
	{
		string root = ScenarioStorage.RootDirectory(_userDataDir);
		Directory.CreateDirectory(Path.Combine(root, "empty-folder"));
		Directory.CreateDirectory(Path.Combine(root, "has-save"));
		File.WriteAllText(Path.Combine(root, "has-save", RunSaveFileService.SaveFileName), "{}");

		string[] scenarios = ScenarioStorage.ListScenarios(_userDataDir);

		Assert.That(scenarios, Is.EqualTo(new[] { "has-save" }));
	}

	[Test]
	public void ListScenarios_IsSortedOrdinally()
	{
		string root = ScenarioStorage.RootDirectory(_userDataDir);

		foreach (string name in new[] { "zebra", "apple", "mango" })
		{
			string directory = Path.Combine(root, name);
			Directory.CreateDirectory(directory);
			File.WriteAllText(Path.Combine(directory, RunSaveFileService.SaveFileName), "{}");
		}

		string[] scenarios = ScenarioStorage.ListScenarios(_userDataDir);

		Assert.That(scenarios, Is.EqualTo(new[] { "apple", "mango", "zebra" }));
	}

	[TestCase("")]
	[TestCase("   ")]
	public void TryValidateScenarioName_RejectsEmptyOrBlankNames(string rawName)
	{
		bool valid = ScenarioStorage.TryValidateScenarioName(rawName, out _, out string error);

		Assert.That(valid, Is.False);
		Assert.That(error, Is.Not.Null);
	}

	[Test]
	public void TryValidateScenarioName_RejectsPathTraversal()
	{
		bool valid = ScenarioStorage.TryValidateScenarioName("../../etc", out _, out string error);

		Assert.That(valid, Is.False);
		Assert.That(error, Is.Not.Null);
	}

	[Test]
	public void TryValidateScenarioName_RejectsPathSeparators()
	{
		bool valid = ScenarioStorage.TryValidateScenarioName("foo/bar", out _, out string error);

		Assert.That(valid, Is.False);
		Assert.That(error, Is.Not.Null);
	}

	[Test]
	public void TryValidateScenarioName_TrimsAndAcceptsAnOrdinaryName()
	{
		bool valid = ScenarioStorage.TryValidateScenarioName("  goblin ambush  ", out string name, out string error);

		Assert.That(valid, Is.True);
		Assert.That(name, Is.EqualTo("goblin ambush"));
		Assert.That(error, Is.Null);
	}
}
