using PhotoImport.Cli;
using PhotoImport.Devices;

namespace PhotoImport;

internal static class Program
{
    internal static int Run(string[] args, CancellationToken token, Func<IPhotoDeviceProvider> deviceProviderFactory, TextWriter output, TextWriter error)
    {
        if(args.FirstOrDefault()=="plan")return new DayPreviewCli(output,error).Run(args,token);
        var provider=deviceProviderFactory();
        try{return args.FirstOrDefault()=="exif"?new JpegExifCli(provider,output,error).Run(args,token):new ProbeCli(provider,output,error).Run(args,token);}
        finally
        {
            try{(provider as IDisposable)?.Dispose();}
            catch(IOException ex){error.WriteLine($"Device manager cleanup warning: {ex.Message}");}
        }
    }
    private static int Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += handler;
        try { return Run(args,cancellation.Token,()=>new WpdPhotoDeviceProvider(),Console.Out,Console.Error); }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }
}
