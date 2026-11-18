using System;
using System.IO;
using FileHandler.Models;
using FileHandler.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FileHandler.Tests;

[TestClass]
public sealed class WindowsDuplicateServiceTests
{
    [TestMethod]
    public void ScanAndMove_MovesOnlyNumberedFilesMatchingAnUnnumberedOriginal()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "clip.mp4"), "original");
            File.WriteAllText(Path.Combine(source, "clip (1).mp4"), "original");
            File.WriteAllText(Path.Combine(source, "clip (2).mp4"), "original");
            File.WriteAllText(Path.Combine(source, "clip (3).mp4"), "different");
            File.WriteAllText(Path.Combine(source, "notes (x).txt"), "not numbered");

            var service = new WindowsDuplicateService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());

            Assert.AreEqual(3, scan.CandidateCount);
            Assert.AreEqual(2, scan.VerifiedDuplicates.Count);
            Assert.AreEqual(1, scan.UnverifiedCount);
            Assert.AreEqual(0, scan.FallbackOriginalCount);

            var summary = service.Move(scan, destination, new Progress<OperationProgress>());

            Assert.AreEqual(2, summary.Moved);
            Assert.IsTrue(File.Exists(Path.Combine(source, "clip.mp4")));
            Assert.IsTrue(File.Exists(Path.Combine(source, "clip (3).mp4")));
            Assert.IsTrue(File.Exists(Path.Combine(destination, "clip (1).mp4")));
            Assert.IsTrue(File.Exists(Path.Combine(destination, "clip (2).mp4")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ScanAndMove_UsesLowestNumberedFileWhenUnnumberedOriginalIsMissing()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "photo (2).jpg"), "reference");
            File.WriteAllText(Path.Combine(source, "photo (4).jpg"), "reference");
            File.WriteAllText(Path.Combine(source, "photo (5).jpg"), "different");

            var service = new WindowsDuplicateService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());

            Assert.AreEqual(1, scan.FallbackOriginalCount);
            Assert.AreEqual(1, scan.VerifiedDuplicates.Count);
            Assert.AreEqual(1, scan.UnverifiedCount);
            Assert.AreEqual(
                Path.Combine(source, "photo (2).jpg"),
                scan.VerifiedDuplicates[0].CanonicalPath);

            var summary = service.Move(scan, destination, new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.Moved);
            Assert.IsTrue(File.Exists(Path.Combine(source, "photo (2).jpg")));
            Assert.IsTrue(File.Exists(Path.Combine(source, "photo (5).jpg")));
            Assert.IsTrue(File.Exists(Path.Combine(destination, "photo (4).jpg")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Move_AddsSuffixWhenDestinationAlreadyContainsTheNumberedName()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(source, "photo.jpg"), "original");
            File.WriteAllText(Path.Combine(source, "photo (1).jpg"), "original");
            File.WriteAllText(Path.Combine(destination, "photo (1).jpg"), "existing destination");

            var service = new WindowsDuplicateService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());
            var summary = service.Move(scan, destination, new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.CollisionRenames);
            Assert.AreEqual(
                "existing destination",
                File.ReadAllText(Path.Combine(destination, "photo (1).jpg")));
            Assert.AreEqual(
                "original",
                File.ReadAllText(Path.Combine(destination, "photo (1)-1.jpg")));
            Assert.IsFalse(File.Exists(Path.Combine(source, "photo (1).jpg")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Move_RejectsDestinationInsideSource()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "clip.mp4"), "original");
            File.WriteAllText(Path.Combine(source, "clip (1).mp4"), "original");

            var service = new WindowsDuplicateService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());

            Assert.ThrowsException<ArgumentException>(() => service.Move(
                scan,
                Path.Combine(source, "output"),
                new Progress<OperationProgress>()));
            Assert.IsTrue(File.Exists(Path.Combine(source, "clip (1).mp4")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"FileHandler.DuplicateTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
