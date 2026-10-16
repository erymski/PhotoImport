using PhotoImport.Cli;
using PhotoImport.Devices;

namespace PhotoImport;

internal static class Program
{
    private static int Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new WpdPhotoDeviceProvider();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += handler;
        try { return args.FirstOrDefault() == "exif"
            ? new JpegExifCli(provider, Console.Out, Console.Error).Run(args, cancellation.Token)
            : new ProbeCli(provider, Console.Out, Console.Error).Run(args, cancellation.Token); }
        finally
        {
            Console.CancelKeyPress -= handler;
            try { provider.Dispose(); }
            catch (IOException ex) { Console.Error.WriteLine($"Device manager cleanup warning: {ex.Message}"); }
        }
    }
}
