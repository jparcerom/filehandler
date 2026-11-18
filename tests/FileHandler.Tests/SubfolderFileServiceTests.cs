using System;
using System.IO;
using FileHandler.Models;
using FileHandler.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FileHandler.Tests;

[TestClass]
public sealed class SubfolderFileServiceTests
{
    [TestMethod]
    public void ScanAndMove_MovesOnlySubfolderFilesToDestinationRoot()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(Path.Combine(source, "nested", "deeper"));
            File.WriteAllText(Path.Combine(source, "root.txt"), "keep at source root");
            File.WriteAllText(Path.Combine(source, "nested", "first.txt"), "first");
            File.WriteAllText(Path.Combine(source, "nested", "deeper", "second.txt"), "second");

            var service = new SubfolderFileService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());
            var summary = service.Move(scan, destination, new Progress<OperationProgress>());

            Assert.AreEqual(2, scan.Files.Count);
            Assert.AreEqual(2, summary.Moved);
            Assert.AreEqual(0, summary.Errors);
            Assert.IsTrue(File.Exists(Path.Combine(source, "root.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(destination, "first.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(destination, "second.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Move_AlwaysRenamesIncomingFilesWhenNamesCollide()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var firstSubfolder = Path.Combine(source, "first");
            var secondSubfolder = Path.Combine(source, "second");
            var destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(firstSubfolder);
            Directory.CreateDirectory(secondSubfolder);
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(firstSubfolder, "photo.jpg"), "identical");
            File.WriteAllText(Path.Combine(secondSubfolder, "photo.jpg"), "different");
            File.WriteAllText(Path.Combine(destination, "photo.jpg"), "identical");
            File.WriteAllText(Path.Combine(destination, "photo-1.jpg"), "reserved");

            var service = new SubfolderFileService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());
            var summary = service.Move(scan, destination, new Progress<OperationProgress>());

            Assert.AreEqual(2, summary.Moved);
            Assert.AreEqual(2, summary.CollisionRenames);
            Assert.AreEqual("identical", File.ReadAllText(Path.Combine(destination, "photo.jpg")));
            Assert.AreEqual("reserved", File.ReadAllText(Path.Combine(destination, "photo-1.jpg")));
            CollectionAssert.AreEquivalent(
                new[] { "identical", "different" },
                new[]
                {
                    File.ReadAllText(Path.Combine(destination, "photo-2.jpg")),
                    File.ReadAllText(Path.Combine(destination, "photo-3.jpg"))
                });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Move_WhenDestinationIsSourceRoot_ExtractsNestedFilesAndKeepsRootFiles()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(Path.Combine(source, "subfolder"));
            File.WriteAllText(Path.Combine(source, "root.txt"), "root");
            File.WriteAllText(Path.Combine(source, "subfolder", "nested.txt"), "nested");

            var service = new SubfolderFileService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());
            var summary = service.Move(scan, source, new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.Moved);
            Assert.IsTrue(File.Exists(Path.Combine(source, "root.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(source, "nested.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(source, "subfolder", "nested.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Move_WhenDestinationIsInsideSource_SkipsItsRootFilesAndExtractsItsNestedFiles()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var destination = Path.Combine(source, "destination");
            Directory.CreateDirectory(Path.Combine(destination, "nested"));
            Directory.CreateDirectory(Path.Combine(source, "other"));
            File.WriteAllText(Path.Combine(destination, "already-at-root.txt"), "keep");
            File.WriteAllText(Path.Combine(destination, "nested", "nested.txt"), "extract");
            File.WriteAllText(Path.Combine(source, "other", "other.txt"), "move");

            var service = new SubfolderFileService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());
            var summary = service.Move(scan, destination, new Progress<OperationProgress>());

            Assert.AreEqual(3, scan.Files.Count);
            Assert.AreEqual(2, summary.Moved);
            Assert.AreEqual(1, summary.AlreadyAtDestination);
            Assert.IsTrue(File.Exists(Path.Combine(destination, "already-at-root.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(destination, "nested.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(destination, "other.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(destination, "nested", "nested.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Move_ReportsLockedFilesAndKeepsThemAtTheSource()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var nested = Path.Combine(source, "nested");
            Directory.CreateDirectory(nested);
            var sourceFile = Path.Combine(nested, "locked.txt");
            File.WriteAllText(sourceFile, "locked");

            var service = new SubfolderFileService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());
            using (new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var summary = service.Move(
                    scan,
                    Path.Combine(root, "destination"),
                    new Progress<OperationProgress>());

                Assert.AreEqual(0, summary.Moved);
                Assert.AreEqual(1, summary.Errors);
                Assert.IsTrue(File.Exists(sourceFile));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"FileHandler.SubfolderTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
