// See https://aka.ms/new-console-template for more information


using Watch_airbag;

var fileChangeProcessor = new FileChangeProcessor();
Task.Run(() => fileChangeProcessor.StartProcessingFileChanges());

Console.WriteLine("listening for changes...");
Console.ReadLine();



