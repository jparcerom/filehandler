using System;
using System.Globalization;
using System.IO;
using FileHandler.Models;
using FileHandler.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FileHandler.Tests;

[TestClass]
public sealed class DateOrganizerTests
{
    [TestMethod]
    public void Organize_MovesRecursiveFilesUsingTheEarlierTimestamp()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var nestedSource = Path.Combine(source, "nested");
            var destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(nestedSource);
            var sourceFile = Path.Combine(nestedSource, "video.mp4");
            File.WriteAllText(sourceFile, "video");
            File.SetCreationTime(sourceFile, new DateTime(2020, 2, 3, 4, 5, 6));
            File.SetLastWriteTime(sourceFile, new DateTime(2022, 6, 7, 8, 9, 10));

            var organizer = new DateOrganizer(Path.Combine(root, "logs", "filehandler.log"));
            var summary = organizer.Organize(source, destination, new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.Total);
            Assert.AreEqual(1, summary.Moved);
            Assert.AreEqual(0, summary.Skipped);
            Assert.AreEqual(0, summary.Errors);
            Assert.IsFalse(File.Exists(sourceFile));
            Assert.IsTrue(File.Exists(Path.Combine(destination, "2020", "02-Febrero", "video.mp4")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Organize_SkipsExistingDestinationFileWithoutOverwriting()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(source);
            var sourceFile = Path.Combine(source, "photo.jpg");
            File.WriteAllText(sourceFile, "original");
            File.SetCreationTime(sourceFile, new DateTime(2024, 3, 1, 0, 0, 0));
            File.SetLastWriteTime(sourceFile, new DateTime(2024, 4, 1, 0, 0, 0));

            var targetDirectory = Path.Combine(destination, "2024", "03-Marzo");
            Directory.CreateDirectory(targetDirectory);
            var targetFile = Path.Combine(targetDirectory, "photo.jpg");
            File.WriteAllText(targetFile, "existing");

            var organizer = new DateOrganizer(Path.Combine(root, "logs", "filehandler.log"));
            var summary = organizer.Organize(source, destination, new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.Skipped);
            Assert.AreEqual(0, summary.Moved);
            Assert.IsTrue(File.Exists(sourceFile));
            Assert.AreEqual("existing", File.ReadAllText(targetFile));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Organize_RejectsIdenticalSourceAndDestination()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var organizer = new DateOrganizer(Path.Combine(root, "logs", "filehandler.log"));

            Assert.ThrowsException<ArgumentException>(() =>
                organizer.Organize(root, root, new Progress<OperationProgress>()));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

    }

    [TestMethod]
    public void GetMonthDirectory_UsesSelectedLanguageAndKeepsNumericPrefix()
    {
        var date = new DateTime(2024, 3, 1);

        Assert.AreEqual(
            Path.Combine("destination", "2024", "03-Marzo"),
            DateOrganizationPath.GetMonthDirectory(
                "destination",
                date,
                CultureInfo.GetCultureInfo("es-ES")));
        Assert.AreEqual(
            Path.Combine("destination", "2024", "03-March"),
            DateOrganizationPath.GetMonthDirectory(
                "destination",
                date,
                CultureInfo.GetCultureInfo("en-US")));
    }

    [TestMethod]
    public void Organize_DoesNotIncludeDestinationWhenItIsInsideSource()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var destination = Path.Combine(source, "organized");
            Directory.CreateDirectory(destination);
            var sourceFile = Path.Combine(source, "video.mp4");
            var existingDestinationFile = Path.Combine(destination, "keep.txt");
            File.WriteAllText(sourceFile, "video");
            File.WriteAllText(existingDestinationFile, "keep");

            var organizer = new DateOrganizer(Path.Combine(root, "logs", "filehandler.log"));
            var summary = organizer.Organize(source, destination, new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.Total);
            Assert.AreEqual(1, summary.Moved);
            Assert.IsTrue(File.Exists(existingDestinationFile));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Organize_SupportsSourcePathsLongerThanMaxPath()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var nestedSource = Path.Combine(
                source,
                new string('a', 80),
                new string('b', 80),
                new string('c', 80));
            Directory.CreateDirectory(nestedSource);
            var sourceFile = Path.Combine(nestedSource, "photo.jpg");
            File.WriteAllText(sourceFile, "long path image");
            File.SetCreationTime(sourceFile, new DateTime(2020, 2, 3));
            File.SetLastWriteTime(sourceFile, new DateTime(2020, 2, 3));
            Assert.IsTrue(sourceFile.Length > 260);

            var destination = Path.Combine(root, "destination");
            var organizer = new DateOrganizer(Path.Combine(root, "logs", "filehandler.log"));
            var summary = organizer.Organize(source, destination, new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.Moved);
            Assert.AreEqual(0, summary.Errors);
            Assert.IsFalse(File.Exists(sourceFile));
            Assert.AreEqual(
                "long path image",
                File.ReadAllText(Path.Combine(destination, "2020", "02-Febrero", "photo.jpg")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Organize_ReportsLockedFilesAndKeepsTheSource()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            var sourceFile = Path.Combine(source, "photo.jpg");
            File.WriteAllText(sourceFile, "locked image");
            File.SetCreationTime(sourceFile, new DateTime(2020, 2, 3));
            File.SetLastWriteTime(sourceFile, new DateTime(2020, 2, 3));

            var logPath = Path.Combine(root, "logs", "filehandler.log");
            var organizer = new DateOrganizer(logPath);
            using (new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var summary = organizer.Organize(
                    source,
                    Path.Combine(root, "destination"),
                    new Progress<OperationProgress>());

                Assert.AreEqual(1, summary.Errors);
                Assert.AreEqual(0, summary.Moved);
                Assert.IsTrue(File.Exists(sourceFile));
            }

            StringAssert.Contains(File.ReadAllText(logPath), "[ERROR]");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"FileHandler.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
