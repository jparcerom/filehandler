using System;
using System.IO;
using System.Linq;
using FileHandler.Models;
using FileHandler.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FileHandler.Tests;

[TestClass]
public sealed class MediaCleanupServiceTests
{
    [TestMethod]
    public void DeleteCopiedFiles_VerifiesCopiesAndRemovesEmptyDirectoriesDeepestFirst()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var nested = Path.Combine(source, "year", "month");
            var empty = Path.Combine(source, "empty");
            var destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(nested);
            Directory.CreateDirectory(empty);
            var sourceFile = Path.Combine(nested, "photo.jpg");
            File.WriteAllText(sourceFile, "image");

            var logPath = Path.Combine(root, "logs", "filehandler.log");
            var copyService = new MediaCopyService(logPath);
            var scan = copyService.Scan(source, new Progress<OperationProgress>());
            var copySummary = copyService.Copy(
                scan,
                copyImages: true,
                imageDestination: destination,
                copyVideos: false,
                videoDestination: null,
                progress: new Progress<OperationProgress>());

            var cleanup = new MediaCleanupService(logPath);
            var summary = cleanup.DeleteCopiedFiles(
                scan,
                copySummary.SuccessfulCopies,
                deleteImages: true,
                deleteVideos: false,
                new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.ImagesDeleted);
            Assert.AreEqual(3, summary.EmptyDirectoriesDeleted);
            Assert.IsFalse(File.Exists(sourceFile));
            Assert.IsTrue(File.Exists(Path.Combine(destination, "photo.jpg")));
            Assert.IsTrue(Directory.Exists(source));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void DeleteCopiedFiles_LeavesEmptyFoldersWhenBothTypesExistButOnlyOneIsSelected()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var imageFolder = Path.Combine(source, "images");
            var videoFolder = Path.Combine(source, "videos");
            var destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(imageFolder);
            Directory.CreateDirectory(videoFolder);
            File.WriteAllText(Path.Combine(imageFolder, "photo.jpg"), "image");
            File.WriteAllText(Path.Combine(videoFolder, "clip.mp4"), "video");

            var logPath = Path.Combine(root, "logs", "filehandler.log");
            var copyService = new MediaCopyService(logPath);
            var scan = copyService.Scan(source, new Progress<OperationProgress>());
            var copySummary = copyService.Copy(
                scan,
                copyImages: true,
                imageDestination: destination,
                copyVideos: true,
                videoDestination: destination,
                progress: new Progress<OperationProgress>());

            var cleanup = new MediaCleanupService(logPath);
            var summary = cleanup.DeleteCopiedFiles(
                scan,
                copySummary.SuccessfulCopies,
                deleteImages: true,
                deleteVideos: false,
                new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.ImagesDeleted);
            Assert.AreEqual(0, summary.VideosDeleted);
            Assert.AreEqual(0, summary.EmptyDirectoriesDeleted);
            Assert.IsTrue(Directory.Exists(imageFolder));
            Assert.IsTrue(File.Exists(Path.Combine(videoFolder, "clip.mp4")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void DeleteCopiedFiles_PreservesChangedAndUncopiedSourceFiles()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(source);
            var copiedSource = Path.Combine(source, "copied.jpg");
            var uncopiedSource = Path.Combine(source, "uncopied.jpg");
            File.WriteAllText(copiedSource, "original");
            File.WriteAllText(uncopiedSource, "not copied");

            var logPath = Path.Combine(root, "logs", "filehandler.log");
            var copyService = new MediaCopyService(logPath);
            var scan = copyService.Scan(source, new Progress<OperationProgress>());
            var copySummary = copyService.Copy(
                scan,
                copyImages: true,
                imageDestination: destination,
                copyVideos: false,
                videoDestination: null,
                progress: new Progress<OperationProgress>());
            File.WriteAllText(copiedSource, "changed after copy");

            var onlyCompletedCopy = copySummary.SuccessfulCopies
                .Where(copy => copy.SourcePath == copiedSource)
                .ToArray();
            var cleanup = new MediaCleanupService(logPath);
            var summary = cleanup.DeleteCopiedFiles(
                scan,
                onlyCompletedCopy,
                deleteImages: true,
                deleteVideos: false,
                new Progress<OperationProgress>());

            Assert.AreEqual(0, summary.ImagesDeleted);
            Assert.AreEqual(1, summary.FilesSkipped);
            Assert.IsTrue(File.Exists(copiedSource));
            Assert.IsTrue(File.Exists(uncopiedSource));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"FileHandler.CleanupTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
