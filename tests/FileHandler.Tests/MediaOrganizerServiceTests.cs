using System;
using System.IO;
using System.Linq;
using FileHandler.Models;
using FileHandler.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FileHandler.Tests;

[TestClass]
public sealed class MediaOrganizerServiceTests
{
    [TestMethod]
    public void ScanAndOrganize_MovesSelectedTypesToSeparateYearMonthDestinations()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(Path.Combine(source, "nested"));
            var image = Path.Combine(source, "nested", "photo.JPG");
            var video = Path.Combine(source, "clip.Mp4");
            var document = Path.Combine(source, "notes.txt");
            File.WriteAllText(image, "image");
            File.WriteAllText(video, "video");
            File.WriteAllText(document, "not media");
            SetDates(image, new DateTime(2020, 2, 3), new DateTime(2022, 6, 7));
            SetDates(video, new DateTime(2024, 11, 3), new DateTime(2021, 3, 7));

            var imageDestination = Path.Combine(root, "images");
            var videoDestination = Path.Combine(root, "videos");
            var service = new MediaOrganizerService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());

            Assert.AreEqual(1, scan.ImageFiles.Count);
            Assert.AreEqual(1, scan.VideoFiles.Count);
            var summary = service.Organize(
                scan,
                organizeImages: true,
                imageDestination,
                organizeVideos: true,
                videoDestination,
                new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.ImagesMoved);
            Assert.AreEqual(1, summary.VideosMoved);
            Assert.AreEqual(0, summary.Errors);
            Assert.IsTrue(File.Exists(Path.Combine(imageDestination, "2020", "02-Febrero", "photo.JPG")));
            Assert.IsTrue(File.Exists(Path.Combine(videoDestination, "2021", "03-Marzo", "clip.Mp4")));
            Assert.IsTrue(File.Exists(document));
            Assert.IsFalse(File.Exists(image));
            Assert.IsFalse(File.Exists(video));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Organize_ProcessesOnlyTheSelectedMediaType()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            var image = Path.Combine(source, "photo.jpg");
            var video = Path.Combine(source, "clip.mp4");
            File.WriteAllText(image, "image");
            File.WriteAllText(video, "video");

            var imageDestination = Path.Combine(root, "images");
            var videoDestination = Path.Combine(root, "videos");
            var service = new MediaOrganizerService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());
            var summary = service.Organize(
                scan,
                organizeImages: true,
                imageDestination,
                organizeVideos: false,
                videoDestination: null,
                new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.ImagesMoved);
            Assert.AreEqual(0, summary.VideosToOrganize);
            Assert.IsFalse(File.Exists(image));
            Assert.IsTrue(File.Exists(video));
            Assert.IsFalse(Directory.Exists(videoDestination));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Organize_AllowsBothTypesToShareOneDestination()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(Path.Combine(source, "nested"));
            var image = Path.Combine(source, "nested", "photo.jpg");
            var video = Path.Combine(source, "nested", "clip.mp4");
            File.WriteAllText(image, "image");
            File.WriteAllText(video, "video");
            SetDates(image, new DateTime(2022, 6, 3), new DateTime(2023, 7, 7));
            SetDates(video, new DateTime(2022, 6, 4), new DateTime(2023, 7, 7));

            var destination = Path.Combine(root, "organized");
            var service = new MediaOrganizerService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());
            var summary = service.Organize(
                scan,
                organizeImages: true,
                destination,
                organizeVideos: true,
                destination,
                new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.ImagesMoved);
            Assert.AreEqual(1, summary.VideosMoved);
            Assert.IsTrue(File.Exists(Path.Combine(destination, "2022", "06-Junio", "photo.jpg")));
            Assert.IsTrue(File.Exists(Path.Combine(destination, "2022", "06-Junio", "clip.mp4")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Organize_AddsSuffixForCollisionEvenWhenContentIsIdentical()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(Path.Combine(source, "nested"));
            var image = Path.Combine(source, "nested", "photo.jpg");
            File.WriteAllText(image, "same content");
            SetDates(image, new DateTime(2021, 4, 2), new DateTime(2022, 6, 7));

            var destination = Path.Combine(root, "destination");
            var monthDirectory = Path.Combine(destination, "2021", "04-Abril");
            Directory.CreateDirectory(monthDirectory);
            File.WriteAllText(Path.Combine(monthDirectory, "photo.jpg"), "same content");
            File.WriteAllText(Path.Combine(monthDirectory, "photo-1.jpg"), "another file");

            var service = new MediaOrganizerService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());
            var summary = service.Organize(
                scan,
                organizeImages: true,
                destination,
                organizeVideos: false,
                videoDestination: null,
                new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.CollisionRenames);
            Assert.AreEqual("same content", File.ReadAllText(Path.Combine(monthDirectory, "photo.jpg")));
            Assert.AreEqual("another file", File.ReadAllText(Path.Combine(monthDirectory, "photo-1.jpg")));
            Assert.AreEqual("same content", File.ReadAllText(Path.Combine(monthDirectory, "photo-2.jpg")));
            Assert.IsFalse(File.Exists(image));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Organize_LeavesFilesAlreadyInTheirDateFolderInPlace()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var destination = Path.Combine(root, "destination");
            var source = Path.Combine(destination, "2020", "02-Febrero");
            Directory.CreateDirectory(source);
            var image = Path.Combine(source, "photo.jpg");
            File.WriteAllText(image, "image");
            SetDates(image, new DateTime(2020, 2, 3), new DateTime(2022, 6, 7));

            var service = new MediaOrganizerService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());
            var summary = service.Organize(
                scan,
                organizeImages: true,
                destination,
                organizeVideos: false,
                videoDestination: null,
                new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.AlreadyOrganized);
            Assert.AreEqual(0, summary.ImagesMoved);
            Assert.IsTrue(File.Exists(image));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Organize_RejectsDestinationInsideSource()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "photo.jpg"), "image");

            var service = new MediaOrganizerService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());

            Assert.ThrowsException<ArgumentException>(() => service.Organize(
                scan,
                organizeImages: true,
                Path.Combine(source, "organized"),
                organizeVideos: false,
                videoDestination: null,
                new Progress<OperationProgress>()));
            Assert.IsFalse(Directory.Exists(Path.Combine(source, "organized")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Organize_WhenCrossingVolumes_VerifiesCopyBeforeDeletingSource()
    {
        var sourceRoot = CreateTemporaryDirectory();
        var destinationRoot = Path.Combine(AppContext.BaseDirectory, $"FileHandler.CrossVolume-{Guid.NewGuid():N}");
        try
        {
            if (string.Equals(
                    Path.GetPathRoot(sourceRoot),
                    Path.GetPathRoot(destinationRoot),
                    StringComparison.OrdinalIgnoreCase))
            {
                Assert.Inconclusive("Se necesitan dos volúmenes distintos para probar el movimiento seguro.");
            }

            Directory.CreateDirectory(destinationRoot);
            var sourceFile = Path.Combine(sourceRoot, "photo.jpg");
            File.WriteAllText(sourceFile, "verified across volumes");
            var logPath = Path.Combine(sourceRoot, "logs", "filehandler.log");
            var service = new MediaOrganizerService(logPath);
            var scan = service.Scan(sourceRoot, new Progress<OperationProgress>());

            var summary = service.Organize(
                scan,
                organizeImages: true,
                destinationRoot,
                organizeVideos: false,
                videoDestination: null,
                new Progress<OperationProgress>());

            var destinationFile = Directory
                .GetFiles(destinationRoot, "photo.jpg", SearchOption.AllDirectories)
                .Single();
            Assert.AreEqual(1, summary.ImagesMoved);
            Assert.AreEqual(0, summary.Errors);
            Assert.IsFalse(File.Exists(sourceFile));
            Assert.AreEqual("verified across volumes", File.ReadAllText(destinationFile));
            StringAssert.Contains(File.ReadAllText(logPath), "[COPIA VERIFICADA]");
        }
        finally
        {
            Directory.Delete(sourceRoot, recursive: true);
            if (Directory.Exists(destinationRoot))
            {
                Directory.Delete(destinationRoot, recursive: true);
            }
        }
    }

    private static void SetDates(string path, DateTime creation, DateTime modified)
    {
        File.SetCreationTime(path, creation);
        File.SetLastWriteTime(path, modified);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"FileHandler.MediaOrganizerTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
