using Flue.Application;
using Flue.Core.Abstractions;
using Flue.Infrastructure.Configuration;
using Flue.Infrastructure.FileSystem;
using Flue.Infrastructure.Generation;
using Flue.Infrastructure.Logic;
using Flue.Infrastructure.Parsing;
using Flue.Infrastructure.Routing;
using Flue.Infrastructure.Styling;
using Flue.Infrastructure.Terminal;

var paths = new FluePaths(Directory.GetCurrentDirectory());
paths.EnsureBaseDirectories();

IVueSfcParser sfcParser = new VueSfcParser();
ITemplateParser templateParser = new TemplateParser();
ITailwindConverter tailwindConverter = new TailwindConverter();
ILogicBridge logicBridge = new TypeScriptLogicBridge();
IVueRouterParser vueRouterParser = new VueRouterParser(paths);
IFlueRouterGenerator flueRouterGenerator = new FlutterRouterGenerator(paths, vueRouterParser);
var widgetRenderer = new DartWidgetRenderer(tailwindConverter);
IDartCodeGenerator dartCodeGenerator = new DartCodeGenerator(widgetRenderer);
IFlueCompiler compiler = new FlueCompiler(paths, sfcParser, templateParser, logicBridge, dartCodeGenerator);
var pubspecManager = new PubspecManager(paths);
await using var fileSystemService = new FileSystemService(paths, compiler, pubspecManager, flueRouterGenerator);
var terminalHandler = new TerminalHandler(fileSystemService, compiler, paths);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cts.Cancel();
};

await terminalHandler.RunAsync(cts.Token);
