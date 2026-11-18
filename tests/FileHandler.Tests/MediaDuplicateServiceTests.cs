using System;
using System.IO;
using System.Linq;
using FileHandler.Models;
using FileHandler.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FileHandler.Tests;

[TestClass]
public sealed class MediaDuplicateServiceTests
{
    [TestMethod]
    public void Scan_FindsIdenticalMediaByHashAndIgnoresNameOnlyMatches()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(Path.Combine(source, "nested"));
            Directory.CreateDirectory(Path.Combine(source, "same-name"));
            const string sharedContent = "same multimedia bytes";
            const string sameNameContent = "same name and content";
            File.WriteAllText(Path.Combine(source, "photo.jpg"), sharedContent);
            File.WriteAllText(Path.Combine(source, "renamed.mp4"), sharedContent);
            File.WriteAllText(Path.Combine(source, "nested", "photo.jpg"), "same multimedia byteX");
            File.WriteAllText(Path.Combine(source, "same-name.jpg"), sameNameContent);
            File.WriteAllText(Path.Combine(source, "same-name", "same-name.jpg"), sameNameContent);
            File.WriteAllText(Path.Combine(source, "ignored.txt"), sharedContent);

            var service = CreateService(root);
            var scan = service.Scan(source, new Progress<OperationProgress>());

            Assert.AreEqual(4, scan.ImageCount);
            Assert.AreEqual(1, scan.VideoCount);
            Assert.AreEqual(2, scan.DuplicateGroups.Count);
            var differentNamesGroup = scan.DuplicateGroups.Single(group =>
                group.Files.Any(file => file.Path.EndsWith("renamed.mp4", StringComparison.OrdinalIgnoreCase)));
            Assert.AreEqual(2, differentNamesGroup.Files.Count);
            Assert.IsFalse(differentNamesGroup.Files.Any(file => file.Path.EndsWith(
                Path.Combine("nested", "photo.jpg"),
                StringComparison.OrdinalIgnoreCase)));
            var sameNamesGroup = scan.DuplicateGroups.Single(group =>
                group.Files.Any(file => Path.GetFileName(file.Path) == "same-name.jpg"));
            Assert.AreEqual(2, sameNamesGroup.Files.Count);
            Assert.AreEqual(0, scan.VerificationErrors);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void SendSelectedToRecycleBin_DeletesOnlySelectedFilesAndKeepsOneCopy()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            var retainedPath = Path.Combine(source, "a.jpg");
            var selectedPath = Path.Combine(source, "b.jpg");
            var otherSelectedPath = Path.Combine(source, "c.jpg");
            File.WriteAllText(retainedPath, "identical image");
            File.WriteAllText(selectedPath, "identical image");
            File.WriteAllText(otherSelectedPath, "identical image");

            var sentPaths = new List<string>();
            var service = CreateService(root, path =>
            {
                sentPaths.Add(path);
                File.Delete(path);
            });
            var scan = service.Scan(source, new Progress<OperationProgress>());

            var summary = service.SendSelectedToRecycleBin(
                scan,
                new[] { selectedPath, otherSelectedPath },
                new Progress<OperationProgress>());

            Assert.AreEqual(2, summary.SentToRecycleBin.Count);
            CollectionAssert.AreEquivalent(new[] { selectedPath, otherSelectedPath }, sentPaths);
            Assert.IsTrue(File.Exists(retainedPath));
            Assert.IsFalse(File.Exists(selectedPath));
            Assert.IsFalse(File.Exists(otherSelectedPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void SendSelectedToRecycleBin_RejectsSelectionOfEveryFileInAGroup()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "a.jpg"), "identical image");
            File.WriteAllText(Path.Combine(source, "b.jpg"), "identical image");

            var service = CreateService(root);
            var scan = service.Scan(source, new Progress<OperationProgress>());
            var allPaths = scan.DuplicateGroups[0].Files.Select(file => file.Path).ToArray();

            Assert.ThrowsException<InvalidOperationException>(() => service.SendSelectedToRecycleBin(
                scan,
                allPaths,
                new Progress<OperationProgress>()));
            Assert.IsTrue(allPaths.All(File.Exists));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void SendSelectedToRecycleBin_LeavesAFileThatChangedAfterScanning()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            var retainedPath = Path.Combine(source, "a.jpg");
            var changedPath = Path.Combine(source, "b.jpg");
            File.WriteAllText(retainedPath, "same content");
            File.WriteAllText(changedPath, "same content");

            var deletedPaths = new List<string>();
            var service = CreateService(root, path =>
            {
                deletedPaths.Add(path);
                File.Delete(path);
            });
            var scan = service.Scan(source, new Progress<OperationProgress>());
            File.WriteAllText(changedPath, "new! content");

            var summary = service.SendSelectedToRecycleBin(
                scan,
                new[] { changedPath },
                new Progress<OperationProgress>());

            Assert.AreEqual(0, summary.SentToRecycleBin.Count);
            Assert.AreEqual(1, summary.ChangedSinceScan);
            Assert.AreEqual(0, deletedPaths.Count);
            Assert.IsTrue(File.Exists(retainedPath));
            Assert.IsTrue(File.Exists(changedPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static MediaDuplicateService CreateService(string root, Action<string>? sendToRecycleBin = null) =>
        new(Path.Combine(root, "logs", "filehandler.log"), sendToRecycleBin);

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"FileHandler.MediaDuplicateTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
