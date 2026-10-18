using PhotoImport.Cli;
using PhotoImport.Devices;
using PhotoImport.Models;
using PhotoImport.Reporting;

namespace PhotoImport.Services;

public static class LivePreviewRunner
{
    // Owns both the opened session and provider; final evidence includes their cleanup failures.
    public static int Run(
        IPhotoDeviceProvider provider,
        LivePreviewOptions options,
        DayCheckpoint? checkpoint,
        PreviewBundle bundle,
        TextWriter output,
        TextWriter error,
        CancellationToken token
    )
    {
        var manifest = new LivePreviewManifest
        {
            RequestedRoots = options.Roots,
            MaxPhotos = options.MaxPhotos,
        };
        IPhotoDevice? session = null;
        DeviceInfo? device = null;
        ProbeReport? inventory = null;
        CaptureCollectionResult? collected = null;
        PreviewReport? preview = null;
        var fatalDevice = false;
        var invalidInput = false;
        var localIo = false;
        void Say(string text, bool failure = false)
        {
            try
            {
                (failure ? error : output).WriteLine(text);
            }
            catch (Exception ex) when (LivePreviewCli.IsIoError(ex))
            {
                localIo = true;
                manifest.Errors.Add("Console output failed: " + ex.Message);
            }
        }
        void Failure(string message)
        {
            manifest.Errors.Add(message);
            Say(message, true);
        }
        try
        {
            token.ThrowIfCancellationRequested();
            var devices = provider.Discover(token);
            var matches = devices.Where(d => d.Id == options.DeviceSelector).ToArray();
            if (matches.Length == 0)
                matches = devices
                    .Where(d =>
                        string.Equals(
                            d.FriendlyName,
                            options.DeviceSelector,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    .ToArray();
            if (matches.Length != 1)
                throw new IOException(
                    matches.Length == 0
                        ? "Selected device not found."
                        : "Ambiguous device name; use an exact ID."
                );
            device = matches[0];
            if (checkpoint is not null && checkpoint.DeviceId != device.Id)
                throw new PreviewInputException("Checkpoint device differs from selected device.");
            checkpoint ??= new(1, device.Id, options.CutoffDay!.Value, "completedDay", []);
            session = provider.Open(device, token);
            if (session is not IPhotoContentDevice content)
                throw new IOException("Backend lacks the provenance-bearing content capability.");
            var roots = DeviceFolderScope.Resolve(session, options.Roots, token);
            manifest.ResolvedRoots = roots.Select(r => r.Path).ToArray();
            inventory = MetadataProbe.Run(session, device, roots, token);
            Say("Preview roots: " + string.Join(", ", manifest.ResolvedRoots));
            Say(
                $"Inventory: {inventory.PhotoCount}; maximum photo attempts: {options.MaxPhotos}; maximum application bytes: {manifest.MaximumApplicationBytes}. Driver read-ahead may transfer more over USB."
            );
            fatalDevice = inventory.Errors.Any(e => e.Operation == "disconnect");
            if (!fatalDevice)
                collected = BoundedCaptureCollector.Run(
                    content,
                    inventory,
                    options.MaxPhotos,
                    token,
                    path => Say("Reading capture metadata: " + path)
                );
            else
                Failure("Device disconnected during inventory; no content attempts made.");
            fatalDevice |= collected?.Disconnected == true;
            if (collected?.Disconnected == true)
                Failure("Device disconnected during capture; further attempts stopped.");
        }
        catch (OperationCanceledException)
        {
            Failure("Preview cancelled.");
        }
        catch (PreviewInputException ex)
        {
            invalidInput = true;
            Failure(ex.Message);
        }
        catch (Exception ex)
            when (ex
                    is IOException
                        or UnauthorizedAccessException
                        or System.Runtime.InteropServices.COMException
                        or TypeInitializationException
            )
        {
            fatalDevice = true;
            Failure("Device preview failed: " + ex.Message);
        }
        finally
        {
            try
            {
                session?.Dispose();
            }
            catch (Exception ex)
                when (ex
                        is IOException
                            or UnauthorizedAccessException
                            or System.Runtime.InteropServices.COMException
                )
            {
                Failure("Session cleanup failed: " + ex.Message);
            }
            try
            {
                (provider as IDisposable)?.Dispose();
            }
            catch (Exception ex)
                when (ex
                        is IOException
                            or UnauthorizedAccessException
                            or System.Runtime.InteropServices.COMException
                )
            {
                Failure("Device manager cleanup failed: " + ex.Message);
            }
        }

        JpegExifReport? capture = collected?.Capture;
        if (inventory is not null && device is not null && checkpoint is not null)
        {
            capture ??= new() { Device = device, RequestedPaths = [] };
            capture.Errors.AddRange(manifest.Errors);
            if (token.IsCancellationRequested)
                capture.CompletionStatus = "cancelled";
            else if (fatalDevice || manifest.Errors.Count > 0)
                capture.CompletionStatus = "incomplete";
            preview = DayPreviewPlanner.Build(
                inventory,
                [
                    new(2, device, capture.CompletionStatus, capture.Files)
                    {
                        Errors = capture.Errors,
                    },
                ],
                checkpoint,
                token
            );
            try
            {
                PreviewReportWriter.WriteConsole(preview, output);
            }
            catch (Exception ex) when (LivePreviewCli.IsIoError(ex))
            {
                localIo = true;
                Failure("Preview console output failed: " + ex.Message);
            }
        }
        manifest.AttemptedCount = collected?.AttemptedCount ?? 0;
        manifest.ApplicationBytesRead = collected?.ApplicationBytesRead ?? 0;
        manifest.LimitReached = collected?.LimitReached ?? false;

        int Exit() =>
            token.IsCancellationRequested ? 130
            : localIo ? 5
            : fatalDevice ? 3
            : invalidInput ? 2
            : preview?.IsComplete == true && manifest.Errors.Count == 0 ? 0
            : 4;
        void UpdateStatus()
        {
            manifest.CompletionStatus =
                token.IsCancellationRequested ? "cancelled"
                : Exit() == 0 ? "complete"
                : "incomplete";
            if (token.IsCancellationRequested)
            {
                if (capture is not null)
                    capture.CompletionStatus = "cancelled";
                if (preview is not null)
                    preview.CompletionStatus = "cancelled";
            }
            else if (localIo && preview is not null)
                preview.CompletionStatus = "incomplete";
        }
        void Export(Action action)
        {
            UpdateStatus();
            try
            {
                action();
            }
            catch (Exception ex) when (LivePreviewCli.IsIoError(ex))
            {
                localIo = true;
                Failure("Report export failed: " + ex.Message);
            }
        }
        if (inventory is not null)
            Export(() => bundle.WriteInventory(inventory));
        if (capture is not null)
            Export(() => bundle.WriteCapture(capture));
        if (preview is not null)
            Export(() => bundle.WritePreview(preview));
        UpdateStatus();
        Export(() => bundle.WriteManifest(manifest));
        try
        {
            bundle.Dispose();
        }
        catch (Exception ex) when (LivePreviewCli.IsIoError(ex))
        {
            localIo = true;
            Failure("Report cleanup failed: " + ex.Message);
        }
        if (token.IsCancellationRequested || localIo)
            bundle.CorrectFinalStatus(
                manifest,
                capture,
                preview,
                token,
                message =>
                {
                    localIo = true;
                    Failure(message);
                }
            );
        return Exit();
    }
}
